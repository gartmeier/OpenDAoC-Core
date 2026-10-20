using System;
using System.Buffers.Binary;
using System.Reflection;
using DOL.GS.PacketHandler;
using DOL.GS.ServerRules;
using DOL.Network;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Turns one call the server makes on the Agent's packet library into the Percept the Mind gets for it (ADR 0001).
    /// A projection gives exactly what the 1.127 packet library writes for the call. The 1.127 library is a chain of overrides from `PacketLib168` up, so each projection names the override it mirrors.
    /// Runs on the game loop, because the call's arguments are live game objects. What it returns holds only plain values.
    /// </summary>
    public static class PerceptProjection
    {
        // What `GSUDPPacketOut.Init` and `GSTCPPacketOut.Init` write before the packet's own fields: room for the size (and the UDP counter), then the code.
        private const int UDP_HEADER_LENGTH = 5;
        private const int TCP_HEADER_LENGTH = 3;

        // The fields `PlayerPositionUpdateHandler.CreateOutPak1127` writes.
        private const int PLAYER_POSITION_1127_LENGTH = 40;

        // Returns null for a call that tells the client nothing. `client` is the Agent's client, whose state decides what the packet library sends.
        public static Percept Project(GameClient client, MethodInfo method, object[] args)
        {
            // Property reads tell the client nothing.
            if (method.IsSpecialName)
                return null;

            return method.Name switch
            {
                nameof(IPacketLib.SendMessage) => Message(client, (string) args[0], (eChatType) args[1], (eChatLoc) args[2]),
                nameof(IPacketLib.SendPlayerCreate) => PlayerCreate(client, (GamePlayer) args[0]),
                nameof(IPacketLib.SendObjectDelete) => ObjectDelete(args[0]),
                nameof(IPacketLib.SendObjectRemove) => ObjectRemove((GameObject) args[0]),
                nameof(IPacketLib.SendPlayerPositionAndObjectID) => OwnPosition(client),
                nameof(IPacketLib.SendPlayerJump) => Jump(client, (bool) args[0]),
                nameof(IPacketLib.SendUpdateMaxSpeed) => MaxSpeed(client),
                nameof(IPacketLib.SendUDP) => RawPacket(method.Name, (PacketOut) args[0], UDP_HEADER_LENGTH),
                nameof(IPacketLib.SendTCP) => RawPacket(method.Name, (PacketOut) args[0], TCP_HEADER_LENGTH),
                _ => new BarePercept(method.Name)
            };
        }

        // Mirrors `PacketLib1127.SendMessage` and the `SendRawMessage` it calls.
        private static MessagePercept Message(GameClient client, string text, eChatType chatType, eChatLoc chatLocation)
        {
            // Chat types the client filtered out, and anything at the character screen, aren't sent.
            if (client.DisabledChatTypes.Contains(chatType) || client.ClientState is GameClient.eClientState.CharScreen)
                return null;

            // A send line is marked as one instead, so it's never a popup.
            bool popup = chatType is not eChatType.CT_Send && chatLocation is eChatLoc.CL_PopupWindow;

            return new MessagePercept(Encoded(text), chatType, popup ? eChatLoc.CL_PopupWindow : eChatLoc.CL_ChatWindow);
        }

        // Mirrors `PacketLib1124.SendPlayerCreate`, which 1.125 to 1.127 don't override.
        // The steed's NPC create, the guild ID and the guild banner that the library sends alongside are packets of their own, written inside the library. They never reach the Agent.
        private static PlayerCreatePercept PlayerCreate(GameClient client, GamePlayer player)
        {
            GamePlayer viewer = client.Player;

            if (player == null || viewer == null || player.CurrentRegion == null || player.CurrentZone == null || !player.IsVisibleTo(viewer))
                return null;

            IServerRules rules = GameServer.ServerRules;
            PlayerCreateHorse horse = null;

            if (player.IsOnHorse)
            {
                GamePlayer.ControlledHorse ridden = player.ActiveHorse;

                // The guild emblem's top bit goes into the barding's.
                if (ridden.BardingColor == 0 && ridden.Barding != 0 && player.Guild != null)
                    horse = new(ridden.ID, ridden.Barding | (player.Guild.Emblem & 0x010000) >> 9, (ushort) player.Guild.Emblem, ridden.Saddle, ridden.SaddleColor);
                else
                    horse = new(ridden.ID, ridden.Barding, ridden.BardingColor, ridden.Saddle, ridden.SaddleColor);
            }

            return new PlayerCreatePercept(
                player.X,
                player.Y,
                player.Z,
                player.Client.SessionID,
                player.ObjectID,
                player.Heading,
                player.Model,
                player.GetDisplayLevel(viewer),
                (PacketRealm) (rules.GetLivingRealm(viewer, player) & 0x03),
                !player.IsAlive,
                player.IsUnderwater,
                player.IsStealthed,
                player.IsWireframe,
                player.CharacterClass.ID == (int) eCharacterClass.Vampiir,
                player.GetFaceAttribute(eCharFacePart.EyeSize),
                player.GetFaceAttribute(eCharFacePart.LipSize),
                player.GetFaceAttribute(eCharFacePart.MoodType),
                player.GetFaceAttribute(eCharFacePart.EyeColor),
                player.GetFaceAttribute(eCharFacePart.HairColor),
                player.GetFaceAttribute(eCharFacePart.FaceType),
                player.GetFaceAttribute(eCharFacePart.HairStyle),
                Encoded(rules.GetPlayerName(viewer, player)),
                Encoded(rules.GetPlayerGuildName(viewer, player)),
                Encoded(rules.GetPlayerLastName(viewer, player)),
                Encoded(rules.GetPlayerPrefixName(viewer, player)),
                Encoded(rules.GetPlayerTitle(viewer, player)),
                horse);
        }

        // Mirrors `PacketLib168.SendObjectDelete`, both overloads. The packet's other field is always 1.
        private static ObjectDeletePercept ObjectDelete(object objectOrId)
        {
            return objectOrId switch
            {
                GameObject gameObject => new(gameObject.ObjectID),
                ushort objectId => new(objectId),
                _ => null
            };
        }

        // Mirrors `PacketLib168.SendObjectRemove`, which no later library overrides. `GameObject.RemoveFromWorld` calls it, for a player who quits among others.
        private static ObjectRemovePercept ObjectRemove(GameObject gameObject)
        {
            if (gameObject == null)
                return null;

            int objectType = gameObject switch
            {
                GamePlayer => 2,
                GameNPC npc => npc.Health > 0 ? 1 : 0,
                _ => 0
            };

            return new(gameObject.ObjectID, objectType);
        }

        // Mirrors `PacketLib1124.SendPlayerPositionAndObjectID`, which 1.125 to 1.127 don't override.
        private static OwnPositionPercept OwnPosition(GameClient client)
        {
            GamePlayer player = client.Player;
            Zone zone = player?.CurrentZone;

            if (zone == null)
                return null;

            return new OwnPositionPercept(
                player.X,
                player.Y,
                player.Z,
                player.ObjectID,
                player.Heading,
                zone.IsDungeon ? (ushort) (zone.XOffset / 0x2000) : 0,
                zone.IsDungeon ? (ushort) (zone.YOffset / 0x2000) : 0,
                player.CurrentRegion.Skin,
                zone.IsDivingEnabled,
                zone.IsDivingEnabled && player.IsUnderwater,
                player.CurrentRegion.IsHousing ? Encoded(GameServer.Instance.Configuration.ServerName) : null);
        }

        // Mirrors `PacketLib168.SendPlayerJump`, which no later library overrides. `headingOnly` sends 0 for the position.
        private static JumpPercept Jump(GameClient client, bool headingOnly)
        {
            GamePlayer player = client.Player;

            if (player == null)
                return null;

            return new JumpPercept(
                headingOnly ? 0 : (uint) player.X,
                headingOnly ? 0 : (uint) player.Y,
                headingOnly ? 0 : (ushort) player.Z,
                player.ObjectID,
                player.Heading,
                player.InHouse && player.CurrentHouse != null ? (ushort) player.CurrentHouse.HouseNumber : 0);
        }

        // Mirrors `PacketLib168.SendUpdateMaxSpeed`, which no later library overrides.
        private static MaxSpeedPercept MaxSpeed(GameClient client)
        {
            GamePlayer player = client.Player;

            if (player == null)
                return null;

            return new MaxSpeedPercept((ushort) player.movementComponent.MaxSpeedPercent, player.IsTurningDisabled, (byte) player.GetModified(eProperty.WaterSpeed));
        }

        // A packet the server built itself (ADR 0001). Only the 1.127 player position is decoded, mirroring `PlayerPositionUpdateHandler.CreateOutPak1127`.
        // The packet is shared with every other client it goes to, so it's only read.
        private static Percept RawPacket(string call, PacketOut packet, int headerLength)
        {
            if (packet == null)
                return null;

            if (call is nameof(IPacketLib.SendUDP) && packet.Code == (byte) eServerPackets.PlayerPosition && packet.Length == headerLength + PLAYER_POSITION_1127_LENGTH && packet.TryGetBuffer(out ArraySegment<byte> buffer))
                return PlayerPosition1127(buffer.AsSpan(headerLength, PLAYER_POSITION_1127_LENGTH));

            return new BarePacketPercept(call, packet.Code);
        }

        // The last two bytes are always 0.
        private static PlayerPositionPercept PlayerPosition1127(ReadOnlySpan<byte> fields)
        {
            ushort heading = BinaryPrimitives.ReadUInt16BigEndian(fields[30..]);
            byte health = fields[35];

            return new PlayerPositionPercept(
                X: BinaryPrimitives.ReadSingleLittleEndian(fields),
                Y: BinaryPrimitives.ReadSingleLittleEndian(fields[4..]),
                Z: BinaryPrimitives.ReadSingleLittleEndian(fields[8..]),
                Speed: BinaryPrimitives.ReadSingleLittleEndian(fields[12..]),
                FallSpeed: BinaryPrimitives.ReadSingleLittleEndian(fields[16..]),
                SessionId: BinaryPrimitives.ReadUInt16BigEndian(fields[20..]),
                ObjectId: BinaryPrimitives.ReadUInt16BigEndian(fields[22..]),
                ZoneId: BinaryPrimitives.ReadUInt16BigEndian(fields[24..]),
                StateFlags: fields[26],
                // fields[27] is always 0.
                SteedSeat: BinaryPrimitives.ReadUInt16BigEndian(fields[28..]),
                Heading: heading & 0xFFF,
                HeadingFlags: heading >> 12,
                ActionFlags: fields[32],
                Roleplay: fields[33] != 0,
                // fields[34] is always 0.
                HealthPercent: health & 0x7F,
                Attacking: (health & 0x80) != 0,
                ManaPercent: fields[36],
                EndurancePercent: fields[37]);
        }

        // A string as the packet encodes it, so characters it can't carry come out as the client gets them.
        private static string Encoded(string text)
        {
            return BaseServer.DefaultEncoding.GetString(BaseServer.DefaultEncoding.GetBytes(text ?? string.Empty));
        }
    }
}
