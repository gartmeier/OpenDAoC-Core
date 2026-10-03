using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Which Characters are online as Agents, by Character name. In memory only: a server restart forgets everything.
    /// </summary>
    public static class AgentRegistry
    {
        private static readonly Dictionary<string, HarnessClient> _clients = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Lock _lock = new();

        // Fails if the Character is already online as an Agent.
        // The entry is released once the client disconnects, however that comes about. Until then, a client that has disconnected already doesn't count.
        public static bool TryAdd(string characterName, HarnessClient client)
        {
            lock (_lock)
            {
                if (_clients.TryGetValue(characterName, out HarnessClient existing) && existing.ClientState is not GameClient.eClientState.Disconnected)
                    return false;

                _clients[characterName] = client;
            }

            client.Disconnected.ContinueWith(_ => Remove(characterName, client), TaskScheduler.Default);
            return true;
        }

        public static bool TryGet(string characterName, out HarnessClient client)
        {
            lock (_lock)
            {
                return _clients.TryGetValue(characterName, out client);
            }
        }

        // Leaves the entry alone if the Character has since logged in again with another client.
        private static void Remove(string characterName, HarnessClient client)
        {
            lock (_lock)
            {
                if (_clients.TryGetValue(characterName, out HarnessClient existing) && existing == client)
                    _clients.Remove(characterName);
            }
        }
    }
}
