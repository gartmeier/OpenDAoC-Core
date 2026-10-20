using System;
using DOL.Database;

namespace DOL.GS.Agents
{
    /// <summary>
    /// What a Mind's hello checks before a Character enters the world as an Agent, and the start of its world entry.
    /// Refuses Characters on a Human's Account, and Characters whose Account is already logged in, as the server's own login check does.
    /// </summary>
    public static class AgentLogin
    {
        /// <summary>
        /// Returns why the Character can't log in as an Agent, a whole sentence for the Human.
        /// Otherwise returns null, with the Character's Harness client registered and entering the world, connected to `mind`. The client tells the Mind once it's in the world, or why it couldn't get there.
        /// </summary>
        public static string TryStart(string characterName, MindLink mind, out HarnessClient harnessClient)
        {
            harnessClient = null;
            DbCoreCharacter character = DOLDB<DbCoreCharacter>.SelectObject(DB.Column("Name").IsEqualTo(characterName));

            if (character == null)
                return $"No Character is named '{characterName}'.";

            DbAccount account = DOLDB<DbAccount>.FindObjectByKey(character.AccountName);

            if (account == null)
                return $"{character.Name}'s Account '{character.AccountName}' doesn't exist.";

            if (!AgentAccount.IsAgentAccount(account.Name))
                return $"{character.Name} belongs to '{account.Name}', which isn't an Agent Account (its name doesn't start with '{AgentAccount.PREFIX}').";

            // A client that connected this tick only shows up here from the next one. The registry covers a second login of the same Character in the meantime, and an Agent Account has only the one.
            // A client that has disconnected can still show up until the next tick, but isn't logged in any more.
            switch (ClientService.Instance.GetClient(IsLoggedIn, account.Name))
            {
                case HarnessClient onlineAgent:
                    return $"{onlineAgent.Player?.Name ?? character.Name} is already online as an Agent.";
                case GameClient online:
                    return $"'{account.Name}' is already logged in with a real client (Possession{(online.Player != null ? $", as {online.Player.Name}" : string.Empty)}).";
            }

            HarnessClient client = new(mind);

            if (!AgentRegistry.TryAdd(character.Name, client))
                return $"{character.Name} is already online as an Agent.";

            client.EnterWorld(account, character);
            harnessClient = client;
            return null;
        }

        private static bool IsLoggedIn(GameClient client, string accountName)
        {
            return client.ClientState is not GameClient.eClientState.Disconnected && client.Account.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
