using System;
using System.Reflection;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;

namespace DOL.GS.Agents
{
    /// <summary>
    /// The Harness client's outbound packet library (ADR 0001): every `Send*` call the server makes to tell the Agent's client something arrives in <see cref="Invoke"/>.
    /// A runtime proxy rather than a hand-written <see cref="IPacketLib"/>, so that every call of the large and changing interface ends up in one place.
    /// Every call is recorded in the Agent's trace and told to its Mind as a Percept. The client reacts to what the Percept says, as a real client does to the packet, and to its Character quitting. It's called on the game loop and must never block.
    /// Where a real library also changes game state, such as the Dialog callback the response handler calls, it does the same.
    /// </summary>
    public class HarnessPacketLib : DispatchProxy
    {
        private HarnessClient _client;
        private AgentTrace _trace;

        public static IPacketLib Create(HarnessClient client, AgentTrace trace)
        {
            IPacketLib packetLib = Create<IPacketLib, HarnessPacketLib>();
            HarnessPacketLib harnessPacketLib = (HarnessPacketLib) packetLib;
            harnessPacketLib._client = client;
            harnessPacketLib._trace = trace;
            return packetLib;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            // Property reads tell the client nothing.
            if (!targetMethod.IsSpecialName)
                _trace.Record(_client.ClientState, targetMethod, args);

            if (targetMethod.Name is nameof(IPacketLib.SendCustomDialog))
                OpenCustomDialog((CustomDialogResponse) args[1]);

            Percept percept = _client.Mind.Perceive(_client, targetMethod, args);

            if (percept != null)
                _client.OnPerceived(percept);

            // As the real library does after it sends the packet: the response handler acts on an answer only for the inviter it names.
            if (targetMethod.Name is nameof(IPacketLib.SendGroupInviteCommand))
                _client.Player?.TempProperties.SetProperty(DialogResponseHandler.GROUP_INVITE_KEY, ((GamePlayer) args[0]).Name);
            else if (targetMethod.Name is nameof(IPacketLib.SendGuildInviteCommand))
                _client.Player?.TempProperties.SetProperty(DialogResponseHandler.GUILD_INVITE_KEY, ((GamePlayer) args[0]).Name);

            if (targetMethod.Name is nameof(IPacketLib.SendPlayerQuit))
                _client.OnPlayerQuit((bool) args[0]);

            // A real client's `PacketProcessor` releases every pooled packet it's handed once sent. Nothing else does, and the pool warns about it next tick.
            // Safe even when the packet was also handed to other clients: the pool only reuses it from the next tick on.
            if (targetMethod.Name is nameof(IPacketLib.SendUDP) && args[0] is GSUDPPacketOut udpPacket)
                udpPacket.ReleasePooledObject();
            else if (targetMethod.Name is nameof(IPacketLib.SendTCP) && args[0] is GSTCPPacketOut tcpPacket)
                tcpPacket.ReleasePooledObject();

            return targetMethod.Name switch
            {
                // Animation IDs the server reads back, as every real packet library defines them (`PacketLib168`).
                "get_" + nameof(IPacketLib.BowPrepare) => 0x1F4,
                "get_" + nameof(IPacketLib.BowShoot) => 0x1F7,
                "get_" + nameof(IPacketLib.OneDualWeaponHit) => 0x1F5,
                "get_" + nameof(IPacketLib.BothDualWeaponHit) => 0x1F6,
                // No line of sight check is started, as a real packet library does when the client isn't Playing.
                nameof(IPacketLib.SendLosCheckRequest) => false,
                // Every other member returns void today. Anything added later gets its type's default.
                _ => targetMethod.ReturnType.IsValueType && targetMethod.ReturnType != typeof(void) ? Activator.CreateInstance(targetMethod.ReturnType) : null
            };
        }

        // As `PacketLib168.SendCustomDialog` does before it sends the packet: declines the custom Dialog still open, and hands the player the new one's callback, which `DialogResponseHandler` calls with the answer.
        private void OpenCustomDialog(CustomDialogResponse callback)
        {
            GamePlayer player = _client.Player;

            if (player == null)
                return;

            lock (player)
            {
                player.CustomDialogCallback?.Invoke(player, 0x00);
                player.CustomDialogCallback = callback;
            }
        }
    }
}
