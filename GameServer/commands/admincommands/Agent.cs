using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DOL.GS.Agents;
using DOL.Logging;

namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&agent",
        ePrivLevel.Admin,
        "Log an Agent's Character in and out of the world through the Harness",
        "/agent login <char> - Enter the world as the Agent's Character, without a game client",
        "/agent logout <char> - Quit the Agent's Character through the normal quit path",
        "/agent soak <char> <n> - Run n login/logout cycles and report the first unclean one",
        "/agent capture <account> - Record every packet a real client logged into <account> sends, until /agent capture off")]
    public class AgentCommand : AbstractCommandHandler, ICommandHandler
    {
        private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        // 1 while a soak run is going.
        private static int _soaking;

        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length == 1)
            {
                DisplaySyntax(client);
                return;
            }

            switch (args[1].ToLowerInvariant())
            {
                case "login":
                {
                    Login(client, args);
                    return;
                }
                case "logout":
                {
                    Logout(client, args);
                    return;
                }
                case "soak":
                {
                    RunSoak(client, args);
                    return;
                }
                case "capture":
                {
                    Capture(client, args);
                    return;
                }
                default:
                {
                    DisplaySyntax(client);
                    return;
                }
            }
        }

        // Reports success only once the Character is in the world. World entry loads it asynchronously, so this resumes on a later tick.
        private async void Login(GameClient client, string[] args)
        {
            if (args.Length != 3)
            {
                DisplaySyntax(client);
                return;
            }

            string refusal = AgentLogin.TryStart(args[2], out HarnessClient harnessClient, out Task enteredWorld);

            if (refusal != null)
            {
                DisplayMessage(client, refusal);
                return;
            }

            // Entering the world started the trace, so its path is known.
            string name = harnessClient.CharacterName;
            DisplayMessage(client, $"Logging {name} in through the Harness, tracing to {harnessClient.TracePath}...");

            try
            {
                await enteredWorld;
            }
            catch (WorldEntryFailedException e)
            {
                DisplayMessage(client, e.Message);
                await harnessClient.Disconnected;
                DisplayMessage(client, $"{name}'s Harness client has disconnected. The trace at {harnessClient.TracePath} shows what the server told it.");
                return;
            }

            DisplayMessage(client, $"{name} is in the world.");
        }

        // Reports success only once the server has disconnected the Harness client, after the quit delay. This resumes on a later tick.
        // Gives up waiting after a time limit, leaving the Character in the world.
        private async void Logout(GameClient client, string[] args)
        {
            if (args.Length != 3)
            {
                DisplaySyntax(client);
                return;
            }

            if (!AgentRegistry.TryGet(args[2], out HarnessClient harnessClient) || !harnessClient.IsInWorld)
            {
                DisplayMessage(client, $"{args[2]} isn't in the world as an Agent.");
                return;
            }

            string name = harnessClient.Player.Name;
            DisplayMessage(client, $"{name} is quitting...");

            try
            {
                await harnessClient.Quit();
            }
            catch (TimeoutException)
            {
                DisplayMessage(client, $"{name} hasn't quit within {HarnessClient.QUIT_TIME_LIMIT / 1000} s. Either the server refused the quit and {name} stays in the world, or combat keeps restarting the quit delay. The trace at {harnessClient.TracePath} shows which.");
                return;
            }

            DisplayMessage(client, $"{name} has quit.");
        }

        // Reports each clean cycle as it completes, and the result once the run stops. Each cycle takes about as long as the quit delay, so this resumes on later ticks.
        // One run at a time: a second one would log the same Character in twice, or count the first run's clients as its own leaks.
        private async void RunSoak(GameClient client, string[] args)
        {
            if (args.Length != 4 || !int.TryParse(args[3], out int cycles) || cycles < 1)
            {
                DisplaySyntax(client);
                return;
            }

            if (Interlocked.CompareExchange(ref _soaking, 1, 0) != 0)
            {
                DisplayMessage(client, "A soak run is already going. Wait for its result.");
                return;
            }

            string name = args[2];

            try
            {
                DisplayMessage(client, $"Soaking {name}: {cycles} login/logout cycles, one after another...");

                if (log.IsInfoEnabled)
                    log.Info($"Soak of {name} started: {cycles} cycles.");

                SoakResult result = await Soak.Run(cycles, async cycle =>
                {
                    string reason = await Soak.LoginLogout(name);

                    if (reason == null)
                        DisplayMessage(client, $"Cycle {cycle} of {cycles} was clean.");

                    return reason;
                }, Soak.MeasureServer);

                string message = result switch
                {
                    { Passed: true } => $"Soak of {name} passed: {cycles} clean cycles, and the server has as many clients and sessions as before.",
                    { FailedCycle: int failedCycle } => $"Soak of {name} failed at cycle {failedCycle} of {cycles}. {result.Reason}",
                    _ => $"Soak of {name} failed after {cycles} clean cycles. {result.Reason}"
                };

                DisplayMessage(client, message);

                if (log.IsInfoEnabled)
                    log.Info(message);
            }
            finally
            {
                Volatile.Write(ref _soaking, 0);
            }
        }

        private void Capture(GameClient client, string[] args)
        {
            if (args.Length != 3)
            {
                DisplaySyntax(client);
                return;
            }

            if (args[2].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                DisplayMessage(client, InboundPacketCapture.Stop() ? "Packet capture stopped." : "No packet capture was running.");
                return;
            }

            string path = InboundPacketCapture.Start(args[2]);
            DisplayMessage(client, $"Capturing packets from account '{args[2]}' to {path}");
        }
    }
}
