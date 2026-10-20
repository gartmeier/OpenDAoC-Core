using System.Reflection;
using DOL.GS.PacketHandler;
using DOL.Network;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Turns one call the server makes on the Agent's packet library into the Percept the Mind gets for it (ADR 0001).
    /// A projection gives exactly what the 1.127 packet library writes for the call. The 1.127 library is a chain of overrides from `PacketLib168` up, so each projection names the override it mirrors.
    /// Runs on the game loop, because the call's arguments are live game objects. What it returns holds only plain values.
    /// </summary>
    public static class PerceptProjection
    {
        // Returns null for a call that tells the client nothing. `client` is the Agent's client, whose state decides what the packet library sends.
        public static Percept Project(GameClient client, MethodInfo method, object[] args)
        {
            // Property reads tell the client nothing.
            if (method.IsSpecialName)
                return null;

            return method.Name switch
            {
                nameof(IPacketLib.SendMessage) => Message(client, (string) args[0], (eChatType) args[1], (eChatLoc) args[2]),
                _ => new BarePercept(method.Name)
            };
        }

        // Mirrors `PacketLib1127.SendMessage` and the `SendRawMessage` it calls.
        private static MessagePercept Message(GameClient client, string text, eChatType chatType, eChatLoc chatLocation)
        {
            // Chat types the client filtered out, and anything at the character screen, aren't sent.
            if (client.DisabledChatTypes.Contains(chatType) || client.ClientState is GameClient.eClientState.CharScreen)
                return null;

            // A send line is marked as one instead, so it's never a popup.
            bool popup = chatType is not eChatType.CT_Send && chatLocation is eChatLoc.CL_PopupWindow;

            // The text as the packet encodes it, so characters it can't carry come out as the client gets them.
            string sent = BaseServer.DefaultEncoding.GetString(BaseServer.DefaultEncoding.GetBytes(text ?? string.Empty));

            return new MessagePercept(sent, chatType, popup ? eChatLoc.CL_PopupWindow : eChatLoc.CL_ChatWindow);
        }
    }
}
