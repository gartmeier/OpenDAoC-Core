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

        private readonly ConcurrentQueue<ClientPacket> _inbound = new();
        private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ushort _sequence = FIRST_SEQUENCE;
        private WorldEntry _worldEntry;
        private Liveness _liveness;
        private WorldExit _worldExit;
        private AgentTrace _trace;
        private bool _mindLost;

        // `mind` is null for a client that `/agent login` logs in, which keeps itself alive without a Mind.
        public HarnessClient(MindLink mind = null) : base(null)
        {
            Mind = mind;
        }

        // The Agent's Mind connection, which is the client's network connection (ADR 0005). Told every call the server makes on the packet library.
        public MindLink Mind { get; }

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
        /// Queues the packet a real client sends for the Primitive, for the next client tick (ADR 0002). Called on the game loop.
        /// Returns why it wasn't queued, or null if it was. Whether the server then accepts it isn't known here (ADR 0004).
        /// </summary>
        public string Dispatch(Primitive primitive)
        {
            if (!IsInWorld)
                return $"{CharacterName} isn't in the world.";

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

        public void Enqueue(ClientPacket packet)
        {
            _inbound.Enqueue(packet);
        }

        // Stamped with a millisecond tick counter, like a real client's.
        public static ClientPacket Ping()
        {
            return ClientPackets1127.Ping((uint) Environment.TickCount);
        }

        // What a client standing still tells the server about its Character.
        public ClientPacket PositionUpdate()
        {
            GamePlayer player = Player;
            PlayerPositionUpdateHandler.StateFlags stateFlags = player.IsSitting ? PlayerPositionUpdateHandler.StateFlags.SITTING : 0;
            return ClientPackets1127.PositionUpdate(new Vector3(player.X, player.Y, player.Z), player.CurrentZone.ID, player.Heading, player.HealthPercent, SessionID, stateFlags, player.ActionFlags);
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
            if (Mind?.IsConnected is false && !_mindLost)
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
                    Mind?.Send(new EnteredMessage());
                    break;
                }
                case WorldEntry.Progress.GaveUp:
                {
                    // A real client giving up closes its connection. Short of Playing, the server takes that as a disconnect. World entry only gives up while Playing if the Character isn't in the world, and then a link death comes first.
                    Mind?.Refuse(WorldEntryFailure());
                    _worldEntry = null;
                    Disconnect();
                    return;
                }
            }

            // Only while a Mind is connected, as only a running client keeps its connection alive.
            if (_liveness != null && !_mindLost)
                StayAlive(_liveness.Tick(GameLoop.GameLoopTime));

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
                Mind?.Refuse(WorldEntryFailure());
            }

            // A normal close: the Agent has left the world.
            Mind?.Close();
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
