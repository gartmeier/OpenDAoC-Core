using System;
using System.IO;
using DOL.GS.Agents;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_InboundPacketCapture
    {
        [Test]
        public void Add_ShouldRecordOnlyTheTargetAccountsPackets()
        {
            StringWriter writer = new();
            using InboundPacketCapture capture = new("agent1", writer);

            capture.Add("agent1", GameClient.eClientState.CharScreen, CaptureTransport.Tcp, Packet(0x10));
            capture.Add("human", GameClient.eClientState.Playing, CaptureTransport.Tcp, Packet(0x11));
            capture.Add(null, GameClient.eClientState.Connecting, CaptureTransport.Tcp, Packet(0x12));
            capture.Add("AGENT1", GameClient.eClientState.Playing, CaptureTransport.Udp, Packet(0xA9));

            CapturedPacket[] packets = CapturedPacket.ParseLines(writer.ToString().Split(writer.NewLine));
            Assert.That(packets, Has.Length.EqualTo(2));
            Assert.That(packets[0].Code, Is.EqualTo(0x10));
            Assert.That(packets[0].ClientState, Is.EqualTo(GameClient.eClientState.CharScreen));
            Assert.That(packets[1].Code, Is.EqualTo(0xA9));
            Assert.That(packets[1].Transport, Is.EqualTo(CaptureTransport.Udp));
        }

        [Test]
        public void Add_ShouldRecordTheGivenBytesExactly()
        {
            StringWriter writer = new();
            using InboundPacketCapture capture = new("agent1", writer);
            byte[] packet = Packet(0x10);
            byte[] receiveBuffer = [0xEE, 0xEE, .. packet, 0xEE];

            capture.Add("agent1", GameClient.eClientState.CharScreen, CaptureTransport.Tcp, receiveBuffer.AsSpan(2, packet.Length));

            CapturedPacket[] packets = CapturedPacket.ParseLines(writer.ToString().Split(writer.NewLine));
            Assert.That(packets[0].Bytes, Is.EqualTo(packet));
        }

        [Test]
        public void Add_ShouldBeIgnored_AfterTheCaptureIsDisposed()
        {
            StringWriter writer = new();
            InboundPacketCapture capture = new("agent1", writer);
            capture.Dispose();

            Assert.DoesNotThrow(() => capture.Add("agent1", GameClient.eClientState.Playing, CaptureTransport.Tcp, Packet(0x10)));
        }

        private static byte[] Packet(byte code)
        {
            // Header, one data byte and a checksum. The capture doesn't validate packets, so the checksum can be anything.
            return [0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, code, 0x42, 0x00, 0x00];
        }
    }
}
