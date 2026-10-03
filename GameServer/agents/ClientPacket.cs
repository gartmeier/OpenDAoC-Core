using DOL.GS.PacketHandler;

namespace DOL.GS.Agents
{
    /// <summary>
    /// One inbound packet the Harness sends in place of a game client, before it's framed for the wire.
    /// </summary>
    public sealed record ClientPacket(byte Code, ushort Parameter, byte[] Data)
    {
        // Wire layout, as read by `GSPacketIn.Load`: data size, sequence, session ID, parameter, code, data, checksum. All big-endian.
        // The code's high byte holds leftover memory in a real client and the server ignores it, so it's always 0 here.
        public byte[] ToWire(ushort sequence, ushort sessionId)
        {
            byte[] bytes = new byte[Data.Length + GSPacketIn.HDR_SIZE];
            WriteUInt16(bytes, 0, (ushort) Data.Length);
            WriteUInt16(bytes, 2, sequence);
            WriteUInt16(bytes, 4, sessionId);
            WriteUInt16(bytes, 6, Parameter);
            bytes[9] = Code;
            Data.CopyTo(bytes, 10);
            WriteUInt16(bytes, bytes.Length - 2, PacketProcessor.CalculateChecksum(bytes, 0, bytes.Length - 2));
            return bytes;
        }

        private static void WriteUInt16(byte[] bytes, int offset, ushort value)
        {
            bytes[offset] = (byte) (value >> 8);
            bytes[offset + 1] = (byte) value;
        }
    }
}
