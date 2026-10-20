using DOL.GS.ServerProperties;

namespace DOL.GS.Agents
{
    /// <summary>
    /// The server properties of the Agents category. The server finds them in any class, so they live here with the rest of the Harness.
    /// </summary>
    public static class AgentProperties
    {
        // Read when the server starts.
        [ServerProperty("agents", "agent_mind_port", "The localhost port Agents' Minds connect to. Takes effect when the server restarts.", 10310)]
        public static int MIND_PORT;

        // Read on every Mind's hello.
        [ServerProperty("agents", "agent_mind_token", "The shared token a Mind must present to log its Agent in. Empty refuses every Mind.", "")]
        public static string MIND_TOKEN;
    }
}
