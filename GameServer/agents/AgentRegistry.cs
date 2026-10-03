using System;
using System.Collections.Generic;
using System.Threading;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Which Characters are online as Agents, by Character name. In memory only: a server restart forgets everything.
    /// </summary>
    public static class AgentRegistry
    {
        private static readonly Dictionary<string, HarnessClient> _clients = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Lock _lock = new();

        // Fails if the Character is already online as an Agent. A Harness client that has since disconnected no longer counts.
        public static bool TryAdd(string characterName, HarnessClient client)
        {
            lock (_lock)
            {
                if (_clients.TryGetValue(characterName, out HarnessClient existing) && existing.ClientState is not GameClient.eClientState.Disconnected)
                    return false;

                _clients[characterName] = client;
                return true;
            }
        }
    }
}
