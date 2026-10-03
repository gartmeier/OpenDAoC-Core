using System;
using System.IO;
using System.Linq;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// Checks the real 1.127 client captures under `Fixtures/Agents/v1127` that the Harness's packet builders are tested against.
    /// See the README next to them for what they contain.
    /// </summary>
    [TestFixture]
    public class UT_ClientCaptureFixtures
    {
        private static readonly string[] Captures = ["login-idle-quit-1", "login-idle-quit-2"];

        // From character select to the first position update in the world, as documented in the README.
        // 0x1D, 0x42 and 0x43 have no handler, so no name.
        private static readonly byte[] WorldEntrySequence =
        [
            (byte) eClientPackets.CharacterSelectRequest,
            (byte) eClientPackets.CharacterOverviewRequest,
            (byte) eClientPackets.RegionListRequest,
            (byte) eClientPackets.PingRequest,
            (byte) eClientPackets.CharacterSelectRequest,
            (byte) eClientPackets.WorldInitRequest,
            (byte) eClientPackets.PlayerInitRequest,
            (byte) eClientPackets.PingRequest,
            (byte) eClientPackets.PositionUpdate,
            (byte) eClientPackets.GameOpenRequest,
            (byte) eClientPackets.ClientFilterSettings,
            (byte) eClientPackets.CreateObjectRequest,
            (byte) eClientPackets.CommandHandler,
            0x1D,
            0x1D,
            (byte) eClientPackets.ClientFilterSettings,
            (byte) eClientPackets.ClientFilterSettings,
            0x1D,
            0x42,
            0x43,
            (byte) eClientPackets.ShowWarmapRequest,
            (byte) eClientPackets.PositionUpdate
        ];

        // Player init and the ping that follows it arrive in either order.
        private static readonly Range EitherOrder = 6..8;

        [TestCaseSource(nameof(Captures))]
        public void EveryPacket_ShouldHaveAValidChecksum(string capture)
        {
            CapturedPacket[] packets = Load(capture);

            Assert.That(packets, Is.Not.Empty);
            Assert.That(packets.Where(static p => !p.HasValidChecksum), Is.Empty);
        }

        [TestCaseSource(nameof(Captures))]
        public void WorldEntry_ShouldFollowTheDocumentedSequence(string capture)
        {
            byte[] codes = WorldEntry(Load(capture)).Select(static p => p.Code).ToArray();

            Assert.That(codes[..EitherOrder.Start], Is.EqualTo(WorldEntrySequence[..EitherOrder.Start]));
            Assert.That(codes[EitherOrder], Is.EquivalentTo(WorldEntrySequence[EitherOrder]));
            Assert.That(codes[EitherOrder.End..], Is.EqualTo(WorldEntrySequence[EitherOrder.End..]));
        }

        private static CapturedPacket[] Load(string capture)
        {
            return CapturedPacket.ParseLines(File.ReadLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Agents", "v1127", $"{capture}.tsv")));
        }

        private static CapturedPacket[] WorldEntry(CapturedPacket[] packets)
        {
            int start = Array.FindIndex(packets, static p => p.ClientState is GameClient.eClientState.CharScreen);
            return packets[start..(start + WorldEntrySequence.Length)];
        }
    }
}
