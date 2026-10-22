using System;
using System.Buffers.Binary;
using System.Linq;
using System.Numerics;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// Golden-byte tests: every packet the Harness builds for world entry, idling and moving matches what a real 1.127 client sent in the captures under `Fixtures/Agents/v1127`.
    /// Sequence and session ID are taken from the captured packet. Wire byte 8 (the code's high byte) is leftover client memory, so it and the checksum aren't compared.
    /// Data offsets the README lists as varying are masked per packet.
    /// </summary>
    [TestFixture]
    public class UT_ClientPackets1127
    {
        private static readonly string[] Captures = ClientCaptures.Names;

        // Those with heading updates.
        private static readonly string[] HeadingUpdateCaptures = [.. ClientCaptures.Names, ClientCaptures.MOVEMENT];

        [TestCaseSource(nameof(Captures))]
        public void CharacterSelect_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.CharacterSelect(0x00), FromWorldEntry(capture, eClientPackets.CharacterSelectRequest, 0));
            AssertMatches(ClientPackets1127.CharacterSelect(0x14), FromWorldEntry(capture, eClientPackets.CharacterSelectRequest, 1));
        }

        [TestCaseSource(nameof(Captures))]
        public void CharacterOverview_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.CharacterOverview(eRealm.Hibernia), FromWorldEntry(capture, eClientPackets.CharacterOverviewRequest, 0));
        }

        [TestCaseSource(nameof(Captures))]
        public void RegionList_ShouldMatchCapture(string capture)
        {
            // Data offset 19 is unknown and varies.
            AssertMatches(ClientPackets1127.RegionList(ClientPackets1127.CharacterIndex(300)), FromWorldEntry(capture, eClientPackets.RegionListRequest, 0), 19);
        }

        [TestCaseSource(nameof(Captures))]
        public void Ping_ShouldMatchCapture(string capture)
        {
            foreach (int occurrence in new[] { 0, 1 })
            {
                CapturedPacket captured = FromWorldEntry(capture, eClientPackets.PingRequest, occurrence);
                uint timestamp = BinaryPrimitives.ReadUInt32BigEndian(captured.Data.AsSpan(4));

                // Data offsets 1-2 follow a cycle and 11 flags the first ping of world entry. The server reads neither.
                AssertMatches(ClientPackets1127.Ping(timestamp), captured, 1, 2, 11);
            }
        }

        [TestCaseSource(nameof(Captures))]
        public void IdlePing_ShouldMatchCapture(string capture)
        {
            CapturedPacket[] pings = ClientCaptures.WhilePlaying(capture, eClientPackets.PingRequest);
            Assert.That(pings, Is.Not.Empty);

            foreach (CapturedPacket captured in pings)
            {
                uint timestamp = BinaryPrimitives.ReadUInt32BigEndian(captured.Data.AsSpan(4));

                // Data offsets 1-2 follow a cycle the server doesn't read. Offset 11 is 0 once world entry is over.
                AssertMatches(ClientPackets1127.Ping(timestamp), captured, 1, 2);
            }
        }

        [TestCaseSource(nameof(Captures))]
        public void WorldInit_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.WorldInit(ClientPackets1127.CharacterIndex(300)), FromWorldEntry(capture, eClientPackets.WorldInitRequest, 0));
        }

        [TestCaseSource(nameof(Captures))]
        public void PlayerInit_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.PlayerInit(), FromWorldEntry(capture, eClientPackets.PlayerInitRequest, 0));
        }

        [TestCaseSource(nameof(Captures))]
        public void GameOpen_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.GameOpen(), FromWorldEntry(capture, eClientPackets.GameOpenRequest, 0));
        }

        [TestCaseSource(nameof(Captures))]
        public void PositionUpdate_ShouldMatchCapture(string capture)
        {
            // The Character's saved spot, standing still. The high byte of the heading carries flags.
            Vector3 position = new(296516, 641415, 4848);
            const ushort zoneId = 202;

            CapturedPacket beforeGameOpen = FromWorldEntry(capture, eClientPackets.PositionUpdate, 0);
            AssertMatches(ClientPackets1127.PositionUpdate(position, zoneId, 0x19DF, 0, 67, beforeGameOpen.SessionId, 0, 0), beforeGameOpen);

            CapturedPacket afterGameOpen = FromWorldEntry(capture, eClientPackets.PositionUpdate, 1);
            AssertMatches(ClientPackets1127.PositionUpdate(position, zoneId, 0x09DF, 0, 100, afterGameOpen.SessionId, 0, PlayerPositionUpdateHandler.ActionFlags.TORCH), afterGameOpen);
        }

        [Test]
        public void RunningPositionUpdate_ShouldMatchCapture()
        {
            // At full speed in the first run, heading 0x5F3 with the client's 0x1000 flag. The full packet is in the README.
            CapturedPacket captured = ClientCaptures.WhilePlaying(ClientCaptures.MOVEMENT, eClientPackets.PositionUpdate).Single(static p => p.Sequence == 0x0042);
            Vector3 position = new(296492.4375f, 642525.25f, 4848);

            AssertMatches(ClientPackets1127.PositionUpdate(position, 202, 0x15F3, 191, 100, captured.SessionId, 0, PlayerPositionUpdateHandler.ActionFlags.TORCH), captured);
        }

        [Test]
        public void EveryRunningPositionUpdate_ShouldMatchCapture()
        {
            // Every update with a speed, ramping up, at full speed, steering and stopping. Only the position, speed and heading change while running, so those are taken from the captured packet.
            CapturedPacket[] running = ClientCaptures.WhilePlaying(ClientCaptures.MOVEMENT, eClientPackets.PositionUpdate).Where(static p => ClientCaptures.Speed(p) > 0).ToArray();
            Assert.That(running, Has.Length.GreaterThan(20));

            foreach (CapturedPacket captured in running)
            {
                byte[] data = captured.Data;
                Vector3 position = new(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(0)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(8)));
                ushort heading = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(30));

                AssertMatches(ClientPackets1127.PositionUpdate(position, 202, heading, ClientCaptures.Speed(captured), 100, captured.SessionId, 0, PlayerPositionUpdateHandler.ActionFlags.TORCH), captured);
            }
        }

        // Once the Human targeted the NPC, every position update carries its object ID and the target-in-view flags.
        [Test]
        public void PositionUpdateWithATarget_ShouldMatchCapture()
        {
            CapturedPacket[] targeting = ClientCaptures.WhilePlaying(ClientCaptures.MOVEMENT, eClientPackets.PositionUpdate).Where(static p => BinaryPrimitives.ReadUInt16BigEndian(p.Data.AsSpan(22)) != 0).ToArray();
            Assert.That(targeting, Is.Not.Empty);

            foreach (CapturedPacket captured in targeting)
            {
                byte[] data = captured.Data;
                Vector3 position = new(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(0)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(8)));
                ushort heading = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(30));
                PlayerPositionUpdateHandler.ActionFlags flags = PlayerPositionUpdateHandler.ActionFlags.TORCH | PlayerPositionUpdateHandler.ActionFlags.TARGET_IN_VIEW;

                AssertMatches(ClientPackets1127.PositionUpdate(position, 202, heading, ClientCaptures.Speed(captured), 100, captured.SessionId, 0, flags, 0x1F0B), captured);
            }
        }

        [Test]
        public void Target_ShouldMatchCapture()
        {
            // The Human clicked the NPC, whose object ID was 0x1F0B. The full packet is in the README.
            CapturedPacket captured = ClientCaptures.WhilePlaying(ClientCaptures.MOVEMENT, eClientPackets.PlayerTarget).Single();

            AssertMatches(ClientPackets1127.Target(0x1F0B), captured);
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void AttackRequest_ShouldBeReadByTheHandler_AsTheHumanPressingTheButton(bool start, int startByte)
        {
            GSPacketIn packet = Loaded(ClientPackets1127.AttackRequest(start));

            Assert.That(packet.Code, Is.EqualTo((ushort) eClientPackets.PlayerAttackRequest));
            Assert.That(packet.ReadByte(), Is.EqualTo(startByte), "start");
            Assert.That(packet.ReadByte(), Is.EqualTo(0), "a user action");
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void SitRequest_ShouldBeReadByTheHandler(bool sit, int status)
        {
            GSPacketIn packet = Loaded(ClientPackets1127.SitRequest(sit));

            Assert.That(packet.Code, Is.EqualTo((ushort) eClientPackets.PlayerSitRequest));
            Assert.That(packet.ReadByte(), Is.EqualTo(status));
        }

        [Test]
        public void HeadingUpdate_ShouldCarryTheTarget_WhereTheHandlerSkipsIt()
        {
            ClientPacket built = ClientPackets1127.HeadingUpdate(0x06D6, 100, 0x0101, PlayerPositionUpdateHandler.ActionFlags.TORCH, 0x1F0B);

            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(built.Data.AsSpan(2)), Is.EqualTo(0x1F0B));
            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(built.Data.AsSpan(4)), Is.EqualTo(0x06D6));
        }

        [Test]
        public void HeadingUpdate_ShouldMatchCapture()
        {
            // The first of the turn on the spot. The full packet is in the README.
            CapturedPacket captured = ClientCaptures.WhilePlaying(ClientCaptures.MOVEMENT, eClientPackets.PlayerHeadingUpdate).First();

            AssertMatches(ClientPackets1127.HeadingUpdate(0x06D6, 100, captured.SessionId, PlayerPositionUpdateHandler.ActionFlags.TORCH), captured);
        }

        [TestCaseSource(nameof(HeadingUpdateCaptures))]
        public void EveryHeadingUpdate_ShouldMatchCapture(string capture)
        {
            // Only the heading varies, so it's taken from the captured packet.
            CapturedPacket[] headingUpdates = ClientCaptures.WhilePlaying(capture, eClientPackets.PlayerHeadingUpdate);
            Assert.That(headingUpdates, Is.Not.Empty);

            foreach (CapturedPacket captured in headingUpdates)
            {
                ushort heading = BinaryPrimitives.ReadUInt16BigEndian(captured.Data.AsSpan(4));
                AssertMatches(ClientPackets1127.HeadingUpdate(heading, 100, captured.SessionId, PlayerPositionUpdateHandler.ActionFlags.TORCH), captured);
            }
        }

        [TestCaseSource(nameof(Captures))]
        public void ClientFilterSettings_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.ClientFilterSettings(initial: true), FromWorldEntry(capture, eClientPackets.ClientFilterSettings, 0));
            AssertMatches(ClientPackets1127.ClientFilterSettings(initial: false), FromWorldEntry(capture, eClientPackets.ClientFilterSettings, 1));
            AssertMatches(ClientPackets1127.ClientFilterSettings(initial: false), FromWorldEntry(capture, eClientPackets.ClientFilterSettings, 2));
        }

        [TestCaseSource(nameof(Captures))]
        public void Command_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.Command("&gc info 1"), FromWorldEntry(capture, eClientPackets.CommandHandler, 0));
        }

        [TestCaseSource(nameof(Captures))]
        public void Quit_ShouldMatchCapture(string capture)
        {
            // The captured Human typed the `/q` alias, the Harness sends `/quit`. Only the command text differs.
            CapturedPacket captured = ClientCaptures.WhilePlaying(capture, eClientPackets.CommandHandler).Last();
            Assert.That(captured.Data, Is.EqualTo("\0&q\0"u8.ToArray()), "captured command");

            ClientPacket quit = ClientPackets1127.Quit();
            Assert.That(quit.Data, Is.EqualTo("\0&quit\0"u8.ToArray()), "command");
            AssertMatches(quit with { Data = captured.Data }, captured);
        }

        [Test]
        public void Say_ShouldMatchCapture()
        {
            // The Human typed `hello` without a slash. It's the first command in the capture.
            CapturedPacket captured = ClientCaptures.WhilePlaying(ClientCaptures.SAY, eClientPackets.CommandHandler).First();
            Assert.That(captured.Data, Is.EqualTo("\0&s hello\0"u8.ToArray()), "captured command");

            AssertMatches(ClientPackets1127.Say("hello"), captured);
        }

        [TestCaseSource(nameof(Captures))]
        public void ShowWarmapRequest_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.ShowWarmapRequest(), FromWorldEntry(capture, eClientPackets.ShowWarmapRequest, 0));
        }

        // The Human's client logged in next to Greeen, whose session ID was 4.
        [Test]
        public void CreatePlayerRequest_ShouldMatchCapture()
        {
            CapturedPacket captured = ClientCaptures.WhilePlaying(ClientCaptures.LOGIN_NEXT_TO_AGENT, eClientPackets.CreatePlayerRequest).Single();

            AssertMatches(ClientPackets1127.CreatePlayerRequest(4), captured);
        }

        [Test]
        public void CreatePlayerRequest_ShouldCarryTheSessionIdAsTheHandlerReadsIt()
        {
            GSPacketIn packet = Loaded(ClientPackets1127.CreatePlayerRequest(0x1234));

            Assert.That(packet.Code, Is.EqualTo((ushort) eClientPackets.CreatePlayerRequest));
            Assert.That(packet.ReadShortLowEndian(), Is.EqualTo(0x1234));
        }

        [TestCase(100, 0x00)]
        [TestCase(109, 0x09)]
        [TestCase(200, 0x0A)]
        [TestCase(300, 0x14)]
        [TestCase(309, 0x1D)]
        public void CharacterIndex_ShouldNumberSlotsAcrossRealms(int accountSlot, int expected)
        {
            Assert.That(ClientPackets1127.CharacterIndex(accountSlot), Is.EqualTo(expected));
        }

        internal static void AssertMatches(ClientPacket built, CapturedPacket captured, params int[] maskedDataOffsets)
        {
            CapturedPacket wire = new(0, CaptureTransport.Tcp, captured.ClientState, built.ToWire(captured.Sequence, captured.SessionId));

            Assert.That(wire.HasValidChecksum, Is.True, "checksum");
            Assert.That(wire.Bytes[..8], Is.EqualTo(captured.Bytes[..8]), "header");
            Assert.That(wire.Code, Is.EqualTo(captured.Code), "code");
            Assert.That(Masked(wire.Data, maskedDataOffsets), Is.EqualTo(Masked(captured.Data, maskedDataOffsets)), "data");
        }

        // The packet as a handler gets it, ready to read its data.
        private static GSPacketIn Loaded(ClientPacket built)
        {
            byte[] wire = built.ToWire(7, 0x0101);
            GSPacketIn packet = new();
            packet.Load(wire, 0, wire.Length);
            return packet;
        }

        private static byte[] Masked(byte[] data, int[] offsets)
        {
            byte[] masked = (byte[]) data.Clone();

            foreach (int offset in offsets)
            {
                if (offset < masked.Length)
                    masked[offset] = 0;
            }

            return masked;
        }

        // The nth packet with this code in the world-entry part of a capture: from the first packet at the character screen until the client sends its first ping while Playing.
        private static CapturedPacket FromWorldEntry(string capture, eClientPackets code, int occurrence)
        {
            CapturedPacket[] packets = ClientCaptures.Load(capture);
            int start = Array.FindIndex(packets, static p => p.ClientState is GameClient.eClientState.CharScreen);
            int end = Array.FindIndex(packets, static p => p.ClientState is GameClient.eClientState.Playing && p.Code == (byte) eClientPackets.PingRequest);
            return packets[start..end].Where(p => p.Code == (byte) code).ElementAt(occurrence);
        }
    }
}
