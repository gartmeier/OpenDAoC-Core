using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_CapturedPacket
    {
        [Test]
        public void Parse_ShouldReturnWhatFormatWrote()
        {
            CapturedPacket packet = new(1234, CaptureTransport.Tcp, GameClient.eClientState.CharScreen, WirePacket(0x10, [0x01, 0x02, 0x03]));

            CapturedPacket parsed = CapturedPacket.Parse(packet.Format());

            Assert.That(parsed.ElapsedMilliseconds, Is.EqualTo(1234));
            Assert.That(parsed.Transport, Is.EqualTo(CaptureTransport.Tcp));
            Assert.That(parsed.ClientState, Is.EqualTo(GameClient.eClientState.CharScreen));
            Assert.That(parsed.Bytes, Is.EqualTo(packet.Bytes));
        }

        [Test]
        public void HeaderFields_ShouldBeReadFromWireBytes()
        {
            CapturedPacket packet = new(0, CaptureTransport.Tcp, GameClient.eClientState.CharScreen, WirePacket(0xA3, [0xAA, 0xBB], sequence: 0x0102, sessionId: 0x0304, parameter: 0x0506));

            Assert.That(packet.Code, Is.EqualTo(0xA3));
            Assert.That(packet.Sequence, Is.EqualTo(0x0102));
            Assert.That(packet.SessionId, Is.EqualTo(0x0304));
            Assert.That(packet.Parameter, Is.EqualTo(0x0506));
            Assert.That(packet.Data, Is.EqualTo(new byte[] { 0xAA, 0xBB }));
        }

        [Test]
        public void HasValidChecksum_ShouldBeFalse_WhenABodyByteChanged()
        {
            byte[] bytes = WirePacket(0xA3, [0xAA, 0xBB]);
            CapturedPacket intact = new(0, CaptureTransport.Tcp, GameClient.eClientState.Playing, bytes);
            byte[] tamperedBytes = (byte[]) bytes.Clone();
            tamperedBytes[10] ^= 0xFF;
            CapturedPacket tampered = new(0, CaptureTransport.Tcp, GameClient.eClientState.Playing, tamperedBytes);

            Assert.That(intact.HasValidChecksum, Is.True);
            Assert.That(tampered.HasValidChecksum, Is.False);
        }

        [Test]
        public void ParseLines_ShouldSkipCommentsAndBlankLines()
        {
            CapturedPacket first = new(10, CaptureTransport.Tcp, GameClient.eClientState.CharScreen, WirePacket(0x10, [0x01]));
            CapturedPacket second = new(20, CaptureTransport.Udp, GameClient.eClientState.Playing, WirePacket(0xA9, [0x02]));
            string[] lines = ["# captured from a 1.127 client", first.Format(), "", "  ", second.Format()];

            CapturedPacket[] parsed = CapturedPacket.ParseLines(lines);

            Assert.That(parsed, Has.Length.EqualTo(2));
            Assert.That(parsed[0].Bytes, Is.EqualTo(first.Bytes));
            Assert.That(parsed[1].Bytes, Is.EqualTo(second.Bytes));
        }

        private static byte[] WirePacket(byte code, byte[] data, ushort sequence = 0, ushort sessionId = 0, ushort parameter = 0)
        {
            byte[] bytes = new byte[data.Length + GSPacketIn.HDR_SIZE];
            bytes[0] = (byte) (data.Length >> 8);
            bytes[1] = (byte) data.Length;
            bytes[2] = (byte) (sequence >> 8);
            bytes[3] = (byte) sequence;
            bytes[4] = (byte) (sessionId >> 8);
            bytes[5] = (byte) sessionId;
            bytes[6] = (byte) (parameter >> 8);
            bytes[7] = (byte) parameter;
            bytes[9] = code;
            data.CopyTo(bytes, 10);
            ushort checksum = PacketProcessor.CalculateChecksum(bytes, 0, bytes.Length - 2);
            bytes[^2] = (byte) (checksum >> 8);
            bytes[^1] = (byte) checksum;
            return bytes;
        }
    }
}
