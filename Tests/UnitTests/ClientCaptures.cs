using System.IO;
using System.Linq;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// The real 1.127 client captures under `Fixtures/Agents/v1127`. See the README next to them for what they contain.
    /// </summary>
    public static class ClientCaptures
    {
        public static readonly string[] Names = ["login-idle-quit-1", "login-idle-quit-2"];

        public static CapturedPacket[] Load(string capture)
        {
            return CapturedPacket.ParseLines(File.ReadLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Agents", "v1127", $"{capture}.tsv")));
        }

        // Every TCP packet with this code that arrived while the client was Playing.
        public static CapturedPacket[] WhilePlaying(string capture, eClientPackets code)
        {
            return Load(capture).Where(p => p.Transport is CaptureTransport.Tcp && p.ClientState is GameClient.eClientState.Playing && p.Code == (byte) code).ToArray();
        }
    }
}
