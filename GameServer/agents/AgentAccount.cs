using System;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Agent Accounts are ordinary Accounts, told apart from Humans' by their name: an `agent` prefix, e.g. `agent1`. The server rejects underscores in account names.
    /// </summary>
    public static class AgentAccount
    {
        public const string PREFIX = "agent";

        // Case-insensitive, as the server's login check treats account names.
        public static bool IsAgentAccount(string accountName)
        {
            return accountName != null && accountName.StartsWith(PREFIX, StringComparison.OrdinalIgnoreCase);
        }
    }
}
