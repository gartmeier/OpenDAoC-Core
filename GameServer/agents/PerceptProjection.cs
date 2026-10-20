using System.Reflection;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Turns one call the server makes on the Agent's packet library into the Percept the Mind gets for it (ADR 0001).
    /// Runs on the game loop, because the call's arguments are live game objects. What it returns holds only plain values.
    /// </summary>
    public static class PerceptProjection
    {
        // Returns null for a call that tells the client nothing.
        public static Percept Project(MethodInfo method, object[] args)
        {
            // Property reads tell the client nothing.
            if (method.IsSpecialName)
                return null;

            return new BarePercept(method.Name);
        }
    }
}
