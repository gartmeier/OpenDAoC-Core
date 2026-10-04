using System;
using System.Threading.Tasks;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Runs an Agent's login/logout cycles one after another, the way `/agent soak` does: milestone 1's repeatable acceptance test.
    /// Leaks are found by comparing the server's clients and sessions before and after the run, so a Human connecting or disconnecting meanwhile shows up as one.
    /// </summary>
    public static class Soak
    {
        // What the server has allocated for its clients.
        public readonly record struct Usage(int Clients, int Sessions);

        /// <summary>
        /// Runs `cycles` cycles, numbered from 1, each one only after the last has completed.
        /// A cycle returns null if it was clean, and otherwise why it wasn't.
        /// </summary>
        public static async Task<SoakResult> Run(int cycles, Func<int, Task<string>> cycle, Func<Usage> measure)
        {
            Usage before = measure();

            for (int i = 1; i <= cycles; i++)
            {
                string reason;

                try
                {
                    reason = await cycle(i);
                }
                catch (Exception e)
                {
                    reason = $"The cycle threw {e.GetType().Name}: {e.Message}";
                }

                if (reason != null)
                    return new(i, reason);
            }

            // Only checked after a clean run: an unclean cycle can leave its client connected on purpose.
            Usage after = measure();

            if (after == before)
                return new(null, null);

            string clients = Difference(after.Clients - before.Clients, "client");
            string sessions = Difference(after.Sessions - before.Sessions, "session");
            string difference = clients != null && sessions != null ? $"{clients} and {sessions}" : clients ?? sessions;
            return new(null, $"The server has {difference} than before the run ({before.Clients} clients and {before.Sessions} sessions before, {after.Clients} and {after.Sessions} after). Humans connecting or disconnecting during the run count too.");
        }

        /// <summary>
        /// Logs the Character in through the Harness and out again straight away, as `/agent login` and `/agent logout` do. Takes about as long as the quit delay.
        /// Returns null if the Character reached the world and then fully disconnected without ever going linkdead. Otherwise returns why not.
        /// A cycle that fails leaves its Harness client to clean up after itself, as `/agent login` and `/agent logout` do.
        /// </summary>
        public static async Task<string> LoginLogout(string characterName)
        {
            string refusal = AgentLogin.TryStart(characterName, out HarnessClient client, out Task enteredWorld);

            if (refusal != null)
                return refusal;

            try
            {
                await enteredWorld;
            }
            catch (WorldEntryFailedException e)
            {
                return $"{e.Message} The trace at {client.TracePath} shows what the server told it.";
            }

            GamePlayer player = client.Player;
            bool quit = true;

            try
            {
                await client.Quit();
            }
            catch (TimeoutException)
            {
                quit = false;
            }

            // A link death the server recovered from still ends in a disconnect, but isn't clean. One can also outlast the quit time limit.
            if (client.LinkDeathTime != 0)
                return $"{player.Name} went linkdead{(quit ? string.Empty : $" and hasn't quit within {HarnessClient.QUIT_TIME_LIMIT / 1000} s")}. The trace at {client.TracePath} shows what the server told it.";

            if (!quit)
                return $"{player.Name} hasn't quit within {HarnessClient.QUIT_TIME_LIMIT / 1000} s. The trace at {client.TracePath} shows why.";

            // The client disconnects from the character screen, where the server's disconnect leaves the Character alone. The quit path should have removed it already.
            if (player.ObjectState is GameObject.eObjectState.Active)
                return $"{player.Name}'s Harness client has disconnected, but {player.Name} is still in the world.";

            // The session ID was released before the disconnect completed. The run's session count shows whether it was.
            return null;
        }

        // Every client the server has, Humans' included, and the session IDs they hold.
        public static Usage MeasureServer()
        {
            return new(ClientService.Instance.ClientCount, GameServer.Instance.SessionCount);
        }

        // E.g. "2 more sessions", or null for none.
        private static string Difference(int difference, string noun)
        {
            if (difference == 0)
                return null;

            int count = Math.Abs(difference);
            return $"{count} {(difference > 0 ? "more" : "fewer")} {noun}{(count == 1 ? string.Empty : "s")}";
        }
    }

    // `Reason` is null if the run passed. `FailedCycle` is null if every cycle was clean and the run failed afterwards.
    public sealed record SoakResult(int? FailedCycle, string Reason)
    {
        public bool Passed => Reason == null;
    }
}
