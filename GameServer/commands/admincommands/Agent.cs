using System;
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
