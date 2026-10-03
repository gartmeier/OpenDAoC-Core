using static DOL.GS.GameClient;

namespace DOL.GS.Agents
{
    /// <summary>
    /// What a real 1.127 client does once the server says its Character has quit, as captured in `Tests/Fixtures/Agents/v1127` (see the README there).
    /// After a `/quit` it goes back to the character screen with its connection still open, and a Human exiting the client from there closes the connection.
    /// The Harness does both at once. Closing from the character screen, not while Playing, is what keeps the server from treating it as a link death.
    /// When the server puts the client out of the game entirely, it closes the connection straight away. That wasn't captured.
    /// Left out: 0xAC and 0xB8, which have no handler.
    /// </summary>
    internal sealed class WorldExit
    {
        private readonly ClientPacket[] _packets;
        private bool _sent;

        // `totalOut` as the server's player-quit call says it.
        public WorldExit(bool totalOut, eRealm realm)
        {
            _packets = totalOut ? [] : [ClientPackets1127.CharacterSelect(0x00), ClientPackets1127.CharacterOverview(realm)];
        }

        // Called on every client tick before the queued packets are processed. Returns true once the client can close its connection.
        public bool Tick(HarnessClient client)
        {
            if (!_sent)
            {
                foreach (ClientPacket packet in _packets)
                    client.Enqueue(packet);

                _sent = true;
                return _packets.Length == 0;
            }

            return client.ClientState is eClientState.CharScreen;
        }
    }
}
