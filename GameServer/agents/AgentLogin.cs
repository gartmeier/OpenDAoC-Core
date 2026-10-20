using System;
using DOL.Database;

namespace DOL.GS.Agents
{
    /// <summary>
    /// What a Mind's hello checks before a Character enters the world as an Agent, and the start of its world entry.
    /// Refuses Characters on a Human's Account, and Characters whose Account is logged in with a real client, as the server's own login check does.
    /// A Character already online as an Agent is taken over by the new Mind while its Mind is live (ADR 0005). Otherwise it's linkdead or leaving, and the new Mind is told to retry.
    /// </summary>
    public static class AgentLogin
    {
        /// <summary>
        /// Called on the game loop. The outcome says whether the Character is entering the world with a new Harness client, was taken over by `mind`, or can't log in, and why.
        /// Unless refused, the client is now connected to `mind` and tells it once the Character is in the world, or why it couldn't get there.
        /// </summary>
        public static LoginOutcome TryStart(string characterName, MindLink mind)
        {
            DbCoreCharacter character = DOLDB<DbCoreCharacter>.SelectObject(DB.Column("Name").IsEqualTo(characterName));

            if (character == null)
                return new LoginOutcome.Refused($"No Character is named '{characterName}'.", false);

            DbAccount account = DOLDB<DbAccount>.FindObjectByKey(character.AccountName);

            if (account == null)
                return new LoginOutcome.Refused($"{character.Name}'s Account '{character.AccountName}' doesn't exist.", false);

            if (!AgentAccount.IsAgentAccount(account.Name))
                return new LoginOutcome.Refused($"{character.Name} belongs to '{account.Name}', which isn't an Agent Account (its name doesn't start with '{AgentAccount.PREFIX}').", false);

            // The registry knows a client from the moment it starts entering the world. The client service only from the next tick.
            if (AgentRegistry.TryGet(character.Name, out HarnessClient online) && online.ClientState is not GameClient.eClientState.Disconnected)
            {
                string refusal = online.TakeOver(mind);
                return refusal == null ? new LoginOutcome.TookOver(online) : new LoginOutcome.Refused(refusal, true);
            }

            // A client that has disconnected can still show up until the next tick, but isn't logged in any more.
            switch (ClientService.Instance.GetClient(IsLoggedIn, account.Name))
            {
                case HarnessClient onlineAgent:
                    return new LoginOutcome.Refused($"'{account.Name}' is already online as an Agent, as {onlineAgent.Player?.Name ?? onlineAgent.CharacterName}.", false);
                case GameClient realClient:
                    return new LoginOutcome.Refused($"'{account.Name}' is already logged in with a real client (Possession{(realClient.Player != null ? $", as {realClient.Player.Name}" : string.Empty)}).", false);
            }

            HarnessClient client = new(mind);

            // The check above saw no client for the Character, and the registry is only added to on the game loop.
            if (!AgentRegistry.TryAdd(character.Name, client))
                return new LoginOutcome.Refused($"{character.Name} is already online as an Agent.", true);

            client.EnterWorld(account, character);
            return new LoginOutcome.Entering(client);
        }

        private static bool IsLoggedIn(GameClient client, string accountName)
        {
            return client.ClientState is not GameClient.eClientState.Disconnected && client.Account.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// How a Mind's hello turned out.
    /// </summary>
    public abstract record LoginOutcome
    {
        // The Character is entering the world with a new Harness client.
        public sealed record Entering(HarnessClient Client) : LoginOutcome;

        // The Character was online with a live Mind, and its Harness client is now connected to the new one instead. The Character stays in the world.
        public sealed record TookOver(HarnessClient Client) : LoginOutcome;

        // `Reason` is a whole sentence for the Human. `Retry` says whether the same hello can succeed later, once the server has removed a linkdead or leaving Character.
        public sealed record Refused(string Reason, bool Retry) : LoginOutcome;
    }
}
