using System;
using System.Reflection;
using DOL.GS.PacketHandler;

namespace DOL.GS.Agents
{
    /// <summary>
    /// The Harness client's outbound packet library (ADR 0001): every `Send*` call the server makes to tell the Agent's client something arrives in <see cref="Invoke"/>.
    /// A runtime proxy rather than a hand-written <see cref="IPacketLib"/>, so that every call of the large and changing interface ends up in one place.
    /// Every call is recorded in the Agent's trace and told to its Mind as a Percept, and the client reacts to its Character quitting. It's called on the game loop and must never block.
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

            _client.Mind.Perceive(_client, targetMethod, args);

            if (targetMethod.Name is nameof(IPacketLib.SendPlayerQuit))
                _client.OnPlayerQuit((bool) args[0]);

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
    }
}
