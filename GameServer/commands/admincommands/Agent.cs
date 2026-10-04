using System;
using System.Threading.Tasks;
using DOL.GS.Agents;

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
