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

        // The target packet's flags when the captured Human clicked an NPC in plain view.
        private const ushort TARGET_FLAGS = 0xE000;

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

        // Where the client's Character is and what it's doing, as read by `PlayerPositionUpdateHandler`.
        // `speed` is 0 standing and the current speed running: the client ramps it up to its maximum over about 0.9 s and down to 0 when it stops.
        // `heading` is the full 16 bits on the wire: the low 12 bits are the heading, the client sets flags in the high nibble.
        // `targetId` is the object ID of what the client has targeted, 0 for nothing. The server skips it.
        // `healthPercent` has its top bit set while the client is attacking, as captured. The server skips it.
        public static ClientPacket PositionUpdate(
            Vector3 position,
            ushort zoneId,
            ushort heading,
            float speed,
            byte healthPercent,
            ushort sessionId,
            PlayerPositionUpdateHandler.StateFlags stateFlags,
            PlayerPositionUpdateHandler.ActionFlags actionFlags,
            ushort targetId = 0)
        {
            byte[] data = new byte[40];
            Span<byte> span = data;
            BinaryPrimitives.WriteSingleLittleEndian(span[0..], position.X);
            BinaryPrimitives.WriteSingleLittleEndian(span[4..], position.Y);
            BinaryPrimitives.WriteSingleLittleEndian(span[8..], position.Z);
            BinaryPrimitives.WriteSingleLittleEndian(span[12..], speed);
            // 16-19: Z speed, 0 in every capture, all on flat ground.
            BinaryPrimitives.WriteUInt16BigEndian(span[20..], sessionId);
            BinaryPrimitives.WriteUInt16BigEndian(span[22..], targetId);
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

        // What the client sends while its Character turns on the spot: the heading reached so far. Read by `PlayerHeadingUpdateHandler`.
        // `heading` is the low 12 bits only: the client never sets the position update's flags here.
        // `targetId` is the object ID of what the client has targeted, 0 for nothing, and `healthPercent` has its top bit set while attacking, as in the position update. The server skips both.
        public static ClientPacket HeadingUpdate(ushort heading, byte healthPercent, ushort sessionId, PlayerPositionUpdateHandler.ActionFlags actionFlags, ushort targetId = 0)
        {
            byte[] data = new byte[14];
            Span<byte> span = data;
            BinaryPrimitives.WriteUInt16BigEndian(span[0..], sessionId);
            BinaryPrimitives.WriteUInt16BigEndian(span[2..], targetId);
            BinaryPrimitives.WriteUInt16BigEndian(span[4..], heading);
            // 6: unknown. 8: 0, the server reads a steed seat. 9: 0, the server reads the state flags shifted right by 2.
            data[7] = (byte) actionFlags;
            data[10] = healthPercent;
            // 11-13: trailing, always 0.
            return new((byte) eClientPackets.PlayerHeadingUpdate, DEFAULT_PARAMETER, data);
        }

        // Asks the server for a player the client got a position for but doesn't know, by its session ID. Read by `PlayerCreationRequestHandler`, which answers with the player's create.
        // The session ID goes low byte first, as in the create object request, then 2 bytes that are 0 in the capture.
        public static ClientPacket CreatePlayerRequest(ushort sessionId)
        {
            byte[] data = new byte[4];
            BinaryPrimitives.WriteUInt16LittleEndian(data, sessionId);
            return new((byte) eClientPackets.CreatePlayerRequest, NO_PARAMETER, data);
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

        // Starts the server's quit path. The captures hold the `&q` alias, which the client sends in the same layout.
        public static ClientPacket Quit()
        {
            return Command("&quit");
        }

        // Says `text` to those nearby, as a chat line typed without a slash: the client sends it as the `/say` command's `&s` alias.
        public static ClientPacket Say(string text)
        {
            return Command($"&s {text}");
        }

        // Selects the object, as a click on it does. The flags are as captured: examine (0x8000), which makes the server describe the object, and both line-of-sight bits (0x6000).
        // A real client works out line of sight itself. The Harness always claims it, so the server's own checks are the only ones.
        public static ClientPacket Target(ushort objectId)
        {
            return Target(objectId, TARGET_FLAGS);
        }

        // Selects nothing, as Esc does, or as the client does by itself when what it attacked dies. The flags are then only the attack mode bit (0x0001), set while attacking.
        public static ClientPacket ClearTarget(bool attacking)
        {
            return Target(0, attacking ? (ushort) 0x0001 : (ushort) 0x0000);
        }

        // Turns auto-attack on or off, as the Human pressing the attack key does, or off as the client does by itself when what it attacked dies (`byClient`).
        // Read by `PlayerAttackRequestHandler`: whether to start, then 0 for the Human or 1 for the client. The captured client stopping by itself left memory in the last two bytes, which the server doesn't read.
        public static ClientPacket AttackRequest(bool start, bool byClient = false)
        {
            return new((byte) eClientPackets.PlayerAttackRequest, DEFAULT_PARAMETER, [start ? (byte) 1 : (byte) 0, byClient ? (byte) 1 : (byte) 0, 0x00, 0x00]);
        }

        // Sits down. A 1.127 client sends `/sit` as the command, not as the sit request.
        public static ClientPacket Sit()
        {
            return Command("&sit");
        }

        // Stands up, as `/stand` does.
        public static ClientPacket Stand()
        {
            return Command("&stand");
        }

        private static ClientPacket Target(ushort objectId, ushort flags)
        {
            byte[] data = new byte[4];
            BinaryPrimitives.WriteUInt16BigEndian(data, objectId);
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(2), flags);
            return new((byte) eClientPackets.PlayerTarget, DEFAULT_PARAMETER, data);
        }

        // Sent by the client at the end of world entry. Its first byte isn't a code the server acts on.
        public static ClientPacket ShowWarmapRequest()
        {
            return new((byte) eClientPackets.ShowWarmapRequest, DEFAULT_PARAMETER, [0x65, 0x00, 0x00, 0xBF]);
        }
    }
}
