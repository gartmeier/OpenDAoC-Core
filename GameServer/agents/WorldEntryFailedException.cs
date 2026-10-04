using System;

namespace DOL.GS.Agents
{
    /// <summary>
    /// An Agent's Character didn't reach the world. The message is a whole sentence for the Human, naming the Character and saying why.
    /// </summary>
    public sealed class WorldEntryFailedException : Exception
    {
        public WorldEntryFailedException(string message) : base(message) { }
    }
}
