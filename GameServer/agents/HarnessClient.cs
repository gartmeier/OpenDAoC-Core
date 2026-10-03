using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using DOL.Database;
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
        private ushort _sequence = FIRST_SEQUENCE;
        private WorldEntry _worldEntry;

        public HarnessClient() : base(null) { }

        /// <summary>
        /// Connects as the Account's client, already past the network handshake, version check and password check, and enters the world as the Character.
        /// Completes once the Character is in the world.
        /// </summary>
        public Task EnterWorld(DbAccount account, DbCoreCharacter character)
        {
            Account = account;
            Version = eClientVersion.Version1127;
            Out = HarnessPacketLib.Create();
            PacketProcessor = new(this);
            ClientState = eClientState.Connecting;
            _worldEntry = new(character);
            GameServer.Instance.Connect(this);
            return _worldEntry.Completion;
        }

        public void Enqueue(ClientPacket packet)
        {
            _inbound.Enqueue(packet);
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
            if (_worldEntry?.Tick(this) is true)
                _worldEntry = null;

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

        // Everything the server sends went to `Out` instead of being queued for a socket.
        public override void SendPendingPackets() { }

        protected override void CloseSocket() { }
    }
}
