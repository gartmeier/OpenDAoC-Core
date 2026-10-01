using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DOL.GS.PacketHandler;

namespace DOL.GS.Agents
{
    public enum CaptureTransport
    {
        Tcp,
        Udp
    }

    /// <summary>
    /// One inbound packet as a real game client sent it, recorded by <see cref="InboundPacketCapture"/>.
    /// <see cref="Bytes"/> is the whole packet as it arrived on the wire: header, data and checksum.
    /// </summary>
    public sealed record CapturedPacket(long ElapsedMilliseconds, CaptureTransport Transport, GameClient.eClientState ClientState, byte[] Bytes)
    {
        public const char COMMENT_PREFIX = '#';
        private const char SEPARATOR = '\t';

        // Header layout, as read by `GSPacketIn.Load`: data size, sequence, session ID, parameter, code (low byte). A two-byte checksum follows the data.
        public ushort Sequence => ReadUInt16(2);
        public ushort SessionId => ReadUInt16(4);
        public ushort Parameter => ReadUInt16(6);
        public byte Code => Bytes[9];
        public byte[] Data => Bytes[10..^2];
        public bool HasValidChecksum => ReadUInt16(Bytes.Length - 2) == PacketProcessor.CalculateChecksum(Bytes, 0, Bytes.Length - 2);

        // One packet per line: elapsed milliseconds, transport, client state, packet code (for reading only), wire bytes in hex.
        public string Format()
        {
            return string.Join(SEPARATOR,
                ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture),
                Transport.ToString(),
                ClientState.ToString(),
                $"0x{Code:X2}",
                Convert.ToHexString(Bytes));
        }

        public static CapturedPacket Parse(string line)
        {
            string[] fields = line.Split(SEPARATOR);

            return new(
                long.Parse(fields[0], CultureInfo.InvariantCulture),
                Enum.Parse<CaptureTransport>(fields[1]),
                Enum.Parse<GameClient.eClientState>(fields[2]),
                Convert.FromHexString(fields[4]));
        }

        // Lines starting with `#` are comments.
        public static CapturedPacket[] ParseLines(IEnumerable<string> lines)
        {
            return lines.Where(static line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith(COMMENT_PREFIX)).Select(Parse).ToArray();
        }

        private ushort ReadUInt16(int offset)
        {
            return (ushort) ((Bytes[offset] << 8) | Bytes[offset + 1]);
        }
    }
}
