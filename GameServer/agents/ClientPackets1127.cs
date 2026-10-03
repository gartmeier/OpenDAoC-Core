using System;
using System.Buffers.Binary;
using System.Numerics;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using DOL.Network;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Builds the inbound packets a real 1.127 game client sends, byte for byte as captured in `Tests/Fixtures/Agents/v1127` (see the README there).
    /// </summary>
    public static class ClientPackets1127
    {
        // Parameter is fixed per packet type (see the README).
        private const ushort NO_PARAMETER = 0x0000;
        private const ushort OVERVIEW_PARAMETER = 0xFFFE;
        private const ushort DEFAULT_PARAMETER = 0xFFFF; // Every other packet type.

        // What the client sent after the character index in both captures. Data offset 19 (0x71 here) is unknown and varies between captures.
        private static readonly byte[] REGION_LIST_TAIL = Convert.FromHexString("01664C415F401E1E00FFFFFFFF0F036D233471DE100000162200000D");

        // Data offsets 0-15: chat types the client filters out (1 = filtered), 16: the /effects setting, 17-19: unknown.
        private static readonly byte[] FILTER_SETTINGS_INITIAL = Convert.FromHexString("0000000000000001000000000000000100E44300");
        private static readonly byte[] FILTER_SETTINGS = Convert.FromHexString("0000000000000001000000000000000100A80401");

        // How the client numbers a Character: 10 slots per realm, starting at 0 for Albion. Account slots are `realm * 100 + slot within realm`.
        // `WorldInitRequestHandler` and `RegionListRequestHandler` map it back.
        public static byte CharacterIndex(int accountSlot)
        {
            return (byte) (accountSlot / 100 * 10 - 10 + accountSlot % 100);
        }

        // `slot` is 0 before a Character is chosen, then the character index (see `CharacterIndex`). The server reads nothing.
        public static ClientPacket CharacterSelect(byte slot)
        {
            return new((byte) eClientPackets.CharacterSelectRequest, NO_PARAMETER, [slot]);
        }

        // Asks for the overview of the Account's Characters in a realm. The server reads the realm.
        public static ClientPacket CharacterOverview(eRealm realm)
        {
            return new((byte) eClientPackets.CharacterOverviewRequest, OVERVIEW_PARAMETER, [(byte) realm]);
        }

        // Asks for the regions around the chosen Character. The server reads only the character index.
        public static ClientPacket RegionList(byte characterIndex)
        {
            return new((byte) eClientPackets.RegionListRequest, NO_PARAMETER, [characterIndex, .. REGION_LIST_TAIL]);
        }

        // `timestamp` is the client's millisecond tick counter. The server reads only that and echoes it in its ping reply.
        // A real client also cycles data offsets 1-2 and sets offset 11 on its first ping of world entry. Those stay 0 here.
        public static ClientPacket Ping(uint timestamp)
        {
            byte[] data = new byte[12];
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), timestamp);
            return new((byte) eClientPackets.PingRequest, NO_PARAMETER, data);
        }

        // Enters the world as the chosen Character. The server reads the character index and loads the Character asynchronously.
        public static ClientPacket WorldInit(byte characterIndex)
        {
            return new((byte) eClientPackets.WorldInitRequest, DEFAULT_PARAMETER, [characterIndex]);
        }

        // Sent once the world has loaded. The server reads nothing; both captures send 3.
        public static ClientPacket PlayerInit()
        {
            return new((byte) eClientPackets.PlayerInitRequest, DEFAULT_PARAMETER, [0x03]);
        }

        // Where the client's Character is and what it's doing, as read by `PlayerPositionUpdateHandler`. Speeds are 0: the Character stands still.
        // `heading` is the full 16 bits on the wire: the low 12 bits are the heading, the client sets flags in the high byte.
        public static ClientPacket PositionUpdate(
            Vector3 position,
            ushort zoneId,
            ushort heading,
            byte healthPercent,
            ushort sessionId,
            PlayerPositionUpdateHandler.StateFlags stateFlags,
            PlayerPositionUpdateHandler.ActionFlags actionFlags)
        {
            byte[] data = new byte[40];
            Span<byte> span = data;
            BinaryPrimitives.WriteSingleLittleEndian(span[0..], position.X);
            BinaryPrimitives.WriteSingleLittleEndian(span[4..], position.Y);
            BinaryPrimitives.WriteSingleLittleEndian(span[8..], position.Z);
            // 12-19: speed and fall speed.
            BinaryPrimitives.WriteUInt16BigEndian(span[20..], sessionId);
            // 22-23: object ID, 0 in both captures.
            BinaryPrimitives.WriteUInt16BigEndian(span[24..], zoneId);
            data[26] = (byte) stateFlags;
            // 27: unknown. 28-29: falling damage.
            BinaryPrimitives.WriteUInt16BigEndian(span[30..], heading);
            data[32] = (byte) actionFlags;
            // 33-34: unknown.
            data[35] = healthPercent;
            // 36-39: trailing, always 0.
            return new((byte) eClientPackets.PositionUpdate, DEFAULT_PARAMETER, data);
        }

        // Ends world entry; the client is Playing from here on. The server reads the flag, which the real client leaves at 0.
        public static ClientPacket GameOpen()
        {
            return new((byte) eClientPackets.GameOpenRequest, DEFAULT_PARAMETER, [0x00]);
        }

        // Chat and effect filters. The client sends the `initial` variant first in world entry and the other one twice right after; they differ only in unknown flags at data offsets 17-19.
        public static ClientPacket ClientFilterSettings(bool initial)
        {
            return new((byte) eClientPackets.ClientFilterSettings, DEFAULT_PARAMETER, initial ? FILTER_SETTINGS_INITIAL : FILTER_SETTINGS);
        }

        // A slash command as typed, with `&` in place of the `/` (e.g. `&quit`), zero-terminated after a leading 0.
        public static ClientPacket Command(string command)
        {
            return new((byte) eClientPackets.CommandHandler, DEFAULT_PARAMETER, [0x00, .. BaseServer.DefaultEncoding.GetBytes(command), 0x00]);
        }

        // Sent by the client at the end of world entry. Its first byte isn't a code the server acts on.
        public static ClientPacket ShowWarmapRequest()
        {
            return new((byte) eClientPackets.ShowWarmapRequest, DEFAULT_PARAMETER, [0x65, 0x00, 0x00, 0xBF]);
        }
    }
}
