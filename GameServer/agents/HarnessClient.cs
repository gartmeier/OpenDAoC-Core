using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using DOL.Database;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using DOL.Logging;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Stands in for the game client of an Agent's Character: a client without a socket.
    /// Inbound packets come from a queue instead of the network and go through the normal packet processor, at the same point in the client tick as a Human's.
    /// Outbound packets go to <see cref="HarnessPacketLib"/>, which records them in the Agent's trace and tells the Agent's Mind.
    /// </summary>
    public sealed class HarnessClient : GameClient
    {
        private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        // In milliseconds. The longest quit delay the server applies (`GamePlayer.QuitTimer`), with a margin. Combat during the delay starts it over, so a quit can still take longer.
        public const int QUIT_TIME_LIMIT = 90000;

        // A real client's version check and login request are packets 1 and 2. The Harness skips both.
        private const ushort FIRST_SEQUENCE = 3;

        // Headings are the low 12 bits of a heading update, 4096 to a full turn.
        private const int MAX_HEADING = 4095;

        private readonly ConcurrentQueue<ClientPacket> _inbound = new();
        private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ushort _sequence = FIRST_SEQUENCE;
        private WorldEntry _worldEntry;
        private Liveness _liveness;
        private WorldExit _worldExit;
        private readonly HarnessMovement _movement;
        // The heading of a heading update still in the inbound queue, which the server hasn't set on the Character yet.
        private ushort? _queuedHeading;
        private AgentTrace _trace;
        private bool _mindLost;

        public HarnessClient(MindLink mind) : base(null)
        {
            Mind = mind;
            _movement = new(this);
        }

        // The Agent's Mind connection, which is the client's network connection (ADR 0005). Told every call the server makes on the packet library.
        // Changes only on the game loop, when another Mind takes over.
        public MindLink Mind { get; private set; }

        // Playing, with the Character added to the world.
        public bool IsInWorld => ClientState is eClientState.Playing && Player?.ObjectState is GameObject.eObjectState.Active;

        // Completes once the server has disconnected the client, its Character saved and removed and its session released.
        public Task Disconnected => _disconnected.Task;

        // Where this login's trace is written, once the client has started entering the world.
        public string TracePath => _trace?.FilePath;

        // The Character it's entering the world as, known before the server has loaded it.
        public string CharacterName { get; private set; }

        /// <summary>
        /// Connects as the Account's client, already past the network handshake, version check and password check, and enters the world as the Character.
        /// Completes once the Character is in the world. Throws a <see cref="WorldEntryFailedException"/> if it doesn't get there within <see cref="WorldEntry.TIME_LIMIT"/>, or the server disconnects the client first.
        /// After a time limit failure the client disconnects itself once the server isn't loading its Character any more, so <see cref="Disconnected"/> completes either way.
        /// </summary>
        public Task EnterWorld(DbAccount account, DbCoreCharacter character)
        {
            Account = account;
            CharacterName = character.Name;
            Version = eClientVersion.Version1127;
            // What the real 1.127 client reports in its version and crypt-key request, which the Harness skips.
            ClientType = eClientType.LabyrinthOfTheMinotaur;
            ClientAddons = eClientAddons.bit4 | eClientAddons.NewNewFrontiers;
            _trace = AgentTrace.Start(character.Name);
            Out = HarnessPacketLib.Create(this, _trace);
            PacketProcessor = new(this);
            ClientState = eClientState.Connecting;
            _worldEntry = new(character, GameLoop.GameLoopTime);
            GameEventMgr.AddHandler(this, GameClientEvent.Disconnected, OnDisconnected);
            GameServer.Instance.Connect(this);
            return _worldEntry.Completion;
        }

        /// <summary>
        /// Types `/quit`. The server's quit path takes it from there, quit delay included, and ends with `OnPlayerQuit`.
        /// Completes once the server has disconnected the client. Throws a <see cref="TimeoutException"/> if it hasn't within <see cref="QUIT_TIME_LIMIT"/>, for example because the server refused the quit.
        /// Nothing is undone then: a refused quit leaves the Character in the world, and a quit delay still running ends in a quit. The trace shows which.
        /// </summary>
        public Task Quit()
        {
            Enqueue(ClientPackets1127.Quit());
            return Disconnected.WaitAsync(TimeSpan.FromMilliseconds(QUIT_TIME_LIMIT));
        }

        /// <summary>
        /// Connects the client to a new Mind in place of its live one, without a logout: the Character stays where it is, and liveness carries on through the new connection.
        /// The old Mind is told it was replaced, and its connection closes. The new one hears `entered` straight away if the Character is in the world, or once world entry completes.
        /// Returns why the new Mind can't take over, or null once it has. A client whose Mind is lost, which goes or has gone linkdead, or which is leaving the world, can't be taken over: it's gone soon, and the Character can log in again then.
        /// Called on the game loop.
        /// </summary>
        public string TakeOver(MindLink mind)
        {
            string name = Player?.Name ?? CharacterName;

            if (!Mind.IsConnected || _mindLost || ClientState is eClientState.Linkdead || Player?.IsLinkDeathTimerRunning is true)
                return $"{name} is linkdead: its last Mind's connection was lost. The server removes the Character once its link-death window has run out, and a new Mind can log in after that.";

            if (_worldExit != null || ClientState is eClientState.Disconnected)
                return $"{name} is leaving the world. A new Mind can log in once the server has disconnected it.";

            MindLink old = Mind;
            Mind = mind;
            old.Replace();

            // Before any call the server makes from here on, so the new Mind hears it in order.
            if (_worldEntry == null && IsInWorld)
                mind.Send(new EnteredMessage());

            return null;
        }

        /// <summary>
        /// Queues the packet a real client sends for the Primitive, for the next client tick (ADR 0002). `from` is the Mind that sent it. Called on the game loop.
        /// Returns why it wasn't queued, or null if it was. Whether the server then accepts it isn't known here (ADR 0004).
        /// </summary>
        public string Dispatch(Primitive primitive, MindLink from)
        {
            // A Mind can send Primitives before it hears that it was replaced.
            if (from != Mind)
                return $"Another Mind has taken over {CharacterName}.";

            if (!IsInWorld)
                return $"{CharacterName} isn't in the world.";

            // Movement is the client's own: it sends the position updates while it runs.
            switch (primitive)
            {
                case MoveToPrimitive moveTo:
                {
                    _movement.MoveTo(new(moveTo.X, moveTo.Y, moveTo.Z), GameLoop.GameLoopTime);
                    return null;
                }
                case StopPrimitive:
                {
                    if (_movement.Stop(GameLoop.GameLoopTime))
                        _liveness?.OnPositionUpdate(GameLoop.GameLoopTime);

                    return null;
                }
                case FacePrimitive face:
                {
                    if (face.Heading is < 0 or > MAX_HEADING)
                        return $"A heading is 0 to {MAX_HEADING}, not {face.Heading}.";

                    // A running client sends no heading update, as captured: the run's own heading, in its next position update, overtakes the face at once.
                    if (_movement.IsRunning)
                        return null;

                    // One heading update with the final heading, as a client turned by the mouse can send: the server and other clients see only the heading each one carries.
                    // Position updates keep their idle cadence, as while the captured client turns.
                    _queuedHeading = (ushort) face.Heading;
                    Enqueue(HeadingUpdate(_queuedHeading.Value));
                    return null;
                }
            }

            ClientPacket packet = primitive switch
            {
                QuitPrimitive => ClientPackets1127.Quit(),
                SayPrimitive say => ClientPackets1127.Say(say.Text),
                _ => null
            };

            if (packet == null)
                return $"The Harness has no Primitive {primitive?.GetType().Name ?? "null"}.";

            Enqueue(packet);
            return null;
        }

        // The server told the client its Character has quit. Called on the game loop, before the server has finished removing and saving the Character.
        public void OnPlayerQuit(bool totalOut)
        {
            _worldExit ??= new(totalOut, Player?.Realm ?? eRealm.None);
        }

        // What the client does with what the server told it. Called on the game loop, after the Mind was told.
        public void OnPerceived(Percept percept)
        {
            if (percept is MaxSpeedPercept maxSpeed)
                _movement.OnMaxSpeed(maxSpeed, GameLoop.GameLoopTime);
        }

        public void Enqueue(ClientPacket packet)
        {
            _inbound.Enqueue(packet);
        }

        // Stamped with a millisecond tick counter, like a real client's.
        public static ClientPacket Ping()
        {
            return ClientPackets1127.Ping((uint) Environment.TickCount);
        }

        // What a client standing still tells the server about its Character. It faces the way it last turned to, even if the server hasn't read that heading update yet.
        public ClientPacket PositionUpdate()
        {
            GamePlayer player = Player;
            PlayerPositionUpdateHandler.StateFlags stateFlags = player.IsSitting ? PlayerPositionUpdateHandler.StateFlags.SITTING : 0;
            return ClientPackets1127.PositionUpdate(new Vector3(player.X, player.Y, player.Z), player.CurrentZone.ID, _queuedHeading ?? player.Heading, 0, player.HealthPercent, SessionID, stateFlags, player.ActionFlags);
        }

        // What a client turning on the spot tells the server, once it faces `heading` (0 to 4095).
        public ClientPacket HeadingUpdate(ushort heading)
        {
            GamePlayer player = Player;
            return ClientPackets1127.HeadingUpdate(heading, player.HealthPercent, SessionID, player.ActionFlags);
        }

        // Called by the client service on the game loop, where a real client's received bytes are processed.
        public override void Receive()
        {
            // Where a real client's closed connection is noticed.
            if (_worldExit?.Tick(this) is true)
            {
                _worldExit = null;
                Disconnect();
                return;
            }

            // A lost Mind is a closed connection (ADR 0005). While Playing, the server takes that as a link death.
            if (!Mind.IsConnected && !_mindLost)
            {
                _mindLost = true;

                if (_worldEntry == null)
                {
                    Disconnect();
                    return;
                }

                // World entry gives up the connection itself, once the server isn't loading the Character any more.
                _worldEntry.Fail("before its Mind disconnected");
            }

            switch (_worldEntry?.Tick(this, GameLoop.GameLoopTime))
            {
                case WorldEntry.Progress.Entered:
                {
                    _worldEntry = null;
                    _liveness = new(GameLoop.GameLoopTime);
                    // Before any call the server makes from here on, so the Mind hears it in order.
                    Mind.Send(new EnteredMessage());
                    break;
                }
                case WorldEntry.Progress.GaveUp:
                {
                    // A real client giving up closes its connection. Short of Playing, the server takes that as a disconnect. World entry only gives up while Playing if the Character isn't in the world, and then a link death comes first.
                    Mind.Refuse(WorldEntryFailure());
                    _worldEntry = null;
                    Disconnect();
                    return;
                }
            }

            // Only while a Mind is connected, as only a running client keeps its connection alive and moves its Character (ADR 0005).
            // A running Character's position updates stand in for the idle ones.
            if (_liveness != null && !_mindLost)
            {
                if (IsInWorld && _movement.Tick(GameLoop.GameLoopTime))
                    _liveness.OnPositionUpdate(GameLoop.GameLoopTime);

                StayAlive(_liveness.Tick(GameLoop.GameLoopTime));
            }

            while (_inbound.TryDequeue(out ClientPacket clientPacket))
            {
                byte[] bytes = clientPacket.ToWire(_sequence++, SessionID);
                GSPacketIn packet = PooledObjectFactory.GetForTick<GSPacketIn>().Init();
                packet.Load(bytes, 0, bytes.Length);

                try
                {
                    PacketProcessor.ProcessInboundPacket(packet);
                }
                catch (Exception e)
                {
                    if (log.IsErrorEnabled)
                        log.Error(e);
                }
                finally
                {
                    packet.ReleasePooledObject();
                }
            }

            // The server has read every heading update, so the Character's heading is the client's again.
            _queuedHeading = null;
        }

        // What an idle client sends so the server doesn't time it out or link-dead it. A position update repeats where the Character is, so it needs the Character in the world.
        private void StayAlive(Liveness.Due due)
        {
            if ((due & Liveness.Due.Ping) != 0)
                Enqueue(Ping());

            if ((due & Liveness.Due.PositionUpdate) != 0 && IsInWorld)
                Enqueue(PositionUpdate());
        }

        // Everything the server sends went to `Out` instead of being queued for a socket.
        public override void SendPendingPackets() { }

        protected override void CloseSocket() { }

        private void OnDisconnected(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveAllHandlersForObject(this);
            // For example when the Character couldn't be added to the world.
            if (_worldEntry != null)
            {
                _worldEntry.Fail("before the server disconnected its Harness client");
                Mind.Refuse(WorldEntryFailure());
            }

            // A normal close: the Agent has left the world. However the client disconnected, `/agent logout` included, the Mind's connection ends here.
            Mind.Close();
            _trace.Dispose();
            _disconnected.TrySetResult();
        }

        // Why world entry failed, once it has.
        private string WorldEntryFailure()
        {
            return _worldEntry.Completion.Exception?.InnerException?.Message;
        }
    }
}
