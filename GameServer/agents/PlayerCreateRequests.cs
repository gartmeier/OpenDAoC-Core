using System.Collections.Generic;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Which other players the Agent's client knows, and when it asks the server for one it doesn't.
    /// The server sends a player's create to those around it only when that player enters the world. A client that enters after it learns of it the way a real client does: it gets the player's positions, doesn't know who that is, and asks for the player's create (`PlayerCreationRequestHandler`).
    /// A player is known from its create until it's deleted (out of view) or removed (it left the world). Pure: it touches nothing in the game.
    /// </summary>
    public sealed class PlayerCreateRequests
    {
        // In milliseconds. How long the client waits for a create before it asks for the same player again, for example when the server doesn't send one because the Character can't see the player. Not from a capture.
        public const int REQUEST_INTERVAL = 5000;

        private readonly HashSet<int> _known = [];
        // When the client last asked for each player it doesn't know, by object ID.
        private readonly Dictionary<int, long> _asked = [];

        // Returns the session ID of the player to ask the server for, or null. `now` is the game loop time.
        public ushort? OnPerceived(Percept percept, long now)
        {
            switch (percept)
            {
                case PlayerCreatePercept create:
                {
                    _known.Add(create.ObjectId);
                    _asked.Remove(create.ObjectId);
                    return null;
                }
                case ObjectDeletePercept delete:
                {
                    _known.Remove(delete.ObjectId);
                    return null;
                }
                case ObjectRemovePercept remove:
                {
                    _known.Remove(remove.ObjectId);
                    return null;
                }
                case PlayerPositionPercept position:
                {
                    if (_known.Contains(position.ObjectId))
                        return null;

                    if (_asked.TryGetValue(position.ObjectId, out long asked) && now - asked < REQUEST_INTERVAL)
                        return null;

                    _asked[position.ObjectId] = now;
                    return (ushort) position.SessionId;
                }
                default:
                    return null;
            }
        }
    }
}
