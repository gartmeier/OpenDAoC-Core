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
    /// Outbound packets go to <see cref="HarnessPacketLib"/>.
    /// </summary>
    public sealed class HarnessClient : GameClient
    {
        private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        // A real client's version check and login request are packets 1 and 2. The Harness skips both.
        private const ushort FIRST_SEQUENCE = 3;

        private readonly ConcurrentQueue<ClientPacket> _inbound = new();
        private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ushort _sequence = FIRST_SEQUENCE;
        private WorldEntry _worldEntry;
        private Liveness _liveness;
        private WorldExit _worldExit;

        public HarnessClient() : base(null) { }

        // Playing, with the Character added to the world.
        public bool IsInWorld => ClientState is eClientState.Playing && Player?.ObjectState is GameObject.eObjectState.Active;

        // Completes once the server has disconnected the client, its Character saved and removed and its session released.
        public Task Disconnected => _disconnected.Task;

        /// <summary>
        /// Connects as the Account's client, already past the network handshake, version check and password check, and enters the world as the Character.
        /// Completes once the Character is in the world.
        /// </summary>
        public Task EnterWorld(DbAccount account, DbCoreCharacter character)
        {
            Account = account;
            Version = eClientVersion.Version1127;
            // What the real 1.127 client reports in its version and crypt-key request, which the Harness skips.
            ClientType = eClientType.LabyrinthOfTheMinotaur;
            ClientAddons = eClientAddons.bit4 | eClientAddons.NewNewFrontiers;
            Out = HarnessPacketLib.Create(this);
            PacketProcessor = new(this);
            ClientState = eClientState.Connecting;
            _worldEntry = new(character);
            GameEventMgr.AddHandler(this, GameClientEvent.Disconnected, OnDisconnected);
            GameServer.Instance.Connect(this);
            return _worldEntry.Completion;
        }

        // Types `/quit`. The server's quit path takes it from there, quit delay included, and ends with `OnPlayerQuit`.
        public void RequestQuit()
        {
            Enqueue(ClientPackets1127.Quit());
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

            if (_worldEntry?.Tick(this) is true)
            {
                _worldEntry = null;
                _liveness = new(GameLoop.GameLoopTime);
            }

            if (_liveness != null)
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
            _disconnected.TrySetResult();
        }
    }
}
