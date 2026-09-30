namespace DOL.GS.Commands
{
    [CmdAttribute(
        "&agent",
        ePrivLevel.Admin,
        "Log an Agent's Character in and out of the world through the Harness",
        "/agent login <char> - Enter the world as the Agent's Character, without a game client",
        "/agent logout <char> - Quit the Agent's Character through the normal quit path",
        "/agent soak <char> <n> - Run n login/logout cycles and report the first unclean one")]
    public class AgentCommand : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length == 1)
            {
                DisplaySyntax(client);
                return;
            } 
        }
    }
}