using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using DOL.GS.Agents;

namespace DOL.GS.Commands
{
    // Agents enter the world only through their Mind (ADR 0005). This is the admin's way to remove one, and to record what a real client sends.
    [CmdAttribute(
        "&agent",
        ePrivLevel.Admin,
        "Remove an Agent from the world, or record a real client's packets",
        "/agent logout <char> - Quit the Agent's Character through the normal quit path, which ends its Mind's connection",
        "/agent capture <account> - Record every packet a real client logged into <account> sends, until /agent capture off",
        "/agent wiki <realm> <folder> - Write the realm's Wiki for Agents into <folder>: its zones with their Mobs and NPCs, its merchants with their wares, and its classes with their Spells and Styles")]
    public class AgentCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            // The server console runs commands on its own thread. Run them on the game loop, as an in-game admin's are, so that what they await resumes there too.
            if (SynchronizationContext.Current is not GameServiceSynchronizationContext)
            {
                ClientService.Instance.Post(static state => state.command.OnCommand(state.client, state.args), (command: this, client, args));
                return;
            }

            if (args.Length == 1)
            {
                DisplaySyntax(client);
                return;
            }

            switch (args[1].ToLowerInvariant())
            {
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
                case "wiki":
                {
                    WriteWiki(client, args);
                    return;
                }
                default:
                {
                    DisplaySyntax(client);
                    return;
                }
            }
        }

        // Reports success only once the server has disconnected the Harness client, after the quit delay. This resumes on a later tick.
        // The Harness client closes its Mind's connection when it disconnects, as it does however it disconnects, so the Mind exits too.
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

            DisplayMessage(client, $"{name} has quit, and the server is closing its Mind's connection.");
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

        // The Wiki comes from the world the server has loaded, so it shows what an Agent will meet (ADR 0006).
        private void WriteWiki(GameClient client, string[] args)
        {
            if (args.Length != 4 || !Enum.TryParse(args[2], true, out eRealm realm) || realm is not (eRealm.Albion or eRealm.Midgard or eRealm.Hibernia))
            {
                DisplaySyntax(client);
                return;
            }

            string folder = Path.GetFullPath(args[3]);
            List<WikiZone> zones = Wiki.FromWorld(realm);
            List<WikiClass> classes = Wiki.ClassesFromServer(realm);
            IReadOnlyDictionary<string, string> pages = Wiki.Pages(realm.ToString(), zones, classes);
            Wiki.Write(folder, pages);
            DisplayMessage(client, $"Wrote {pages.Count} pages of {realm}'s Wiki, for {zones.Count} zones and {classes.Count} classes, to {folder}.");
        }
    }
}
