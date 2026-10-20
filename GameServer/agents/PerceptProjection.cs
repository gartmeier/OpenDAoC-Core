using System;
using System.Buffers.Binary;
using System.Reflection;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.ServerRules;
using DOL.Language;
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
                nameof(IPacketLib.SendNPCCreate) => NpcCreate(client, (GameNPC) args[0]),
                nameof(IPacketLib.SendObjectUpdate) => ObjectUpdate(client, (GameObject) args[0]),
                nameof(IPacketLib.SendObjectDelete) => ObjectDelete(args[0]),
                nameof(IPacketLib.SendObjectRemove) => ObjectRemove((GameObject) args[0]),
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

        // Mirrors `PacketLib1124.SendNPCCreate`, which 1.125 to 1.127 don't override.
        // A moving object (a boat, a siege weapon, a mine) gets the library's moving object create instead, which has no projection yet. The rider's own steed and, on a PvP server, a pet's guild ID are packets of their own, written inside the library. They never reach the Agent.
        private static Percept NpcCreate(GameClient client, GameNPC npc)
        {
            GamePlayer viewer = client.Player;

            if (npc == null || viewer == null || !npc.IsVisibleTo(viewer))
                return null;

            // Mines aren't shown to enemy players.
            if (npc is GameMine mine && GameServer.ServerRules.IsAllowedToAttack(mine.Owner, viewer, true))
                return null;

            if (npc is GameMovingObject)
                return new BarePercept(nameof(IPacketLib.SendMovingObjectCreate));

            short speed = 0;
            ushort speedZ = 0;

            if (npc.IsMoving && !npc.IsAtDestination)
            {
                speed = npc.CurrentSpeed;
                speedZ = (ushort) npc.movementComponent.Velocity.Z;
            }

            byte level = npc.GetDisplayLevel(viewer);
            bool gm = client.Account.PrivLevel > 1;
            eQuestIndicator questIndicator = npc.GetQuestIndicator(viewer);
            string add = string.Empty;

            // A GM sees the hiding flags as name suffixes instead.
            if ((npc.Flags & GameNPC.eFlags.CANTTARGET) != 0 && gm)
                add += "-DOR";

            if ((npc.Flags & GameNPC.eFlags.DONTSHOWNAME) != 0 && gm)
                add += "-NON";

            string name = npc.Name ?? string.Empty;
            string guildName = npc.GuildName ?? string.Empty;

            if (LanguageMgr.GetTranslation(client, npc) is DbLanguageGameNpc translation)
            {
                if (!string.IsNullOrEmpty(translation.Name))
                    name = translation.Name;

                if (!string.IsNullOrEmpty(translation.GuildName))
                    guildName = translation.GuildName;
            }

            int maxNameLength = 47 - add.Length - 2;

            if (name.Length > maxNameLength)
                name = name[..maxNameLength];

            if (add.Length > 0)
                name = $"[{name}]{add}";

            if (guildName.Length > 47)
                guildName = guildName[..47];

            return new NpcCreatePercept(
                X: (int) (uint) npc.X,
                Y: (int) (uint) npc.Y,
                Z: (ushort) npc.Z,
                ObjectId: npc.ObjectID,
                Speed: (ushort) speed,
                ZSpeed: speedZ,
                Heading: npc.Heading,
                Model: npc.Model,
                Size: npc.Size,
                Level: level & 0x7F,
                Statue: (npc.Flags & GameNPC.eFlags.STATUE) != 0,
                Realm: (PacketRealm) (GameServer.ServerRules.GetLivingRealm(viewer, npc) & 0x03),
                Ghost: (npc.Flags & GameNPC.eFlags.GHOST) != 0,
                Equipped: npc.Inventory != null,
                Torch: (npc.Flags & GameNPC.eFlags.TORCH) != 0,
                Peaceful: (npc.Flags & GameNPC.eFlags.PEACE) != 0,
                Flying: (npc.Flags & GameNPC.eFlags.FLYING) != 0,
                Pet: npc.Brain is IControlledBrain,
                Untargetable: (npc.Flags & GameNPC.eFlags.CANTTARGET) != 0 && !gm,
                NameHidden: (npc.Flags & GameNPC.eFlags.DONTSHOWNAME) != 0 && !gm,
                Stealthed: npc.IsStealthed,
                QuestIndicator: questIndicator switch
                {
                    eQuestIndicator.Available or eQuestIndicator.Finish or eQuestIndicator.Lesson or eQuestIndicator.Lore or eQuestIndicator.Pending => questIndicator,
                    _ => eQuestIndicator.None
                },
                Name: Encoded(name),
                GuildName: Encoded(guildName));
        }

        // Mirrors `PacketLib168.SendObjectUpdate`, which no later library overrides. Sent for NPCs, and for other objects the server updates the same way.
        // The packet carries positions relative to the zone they're in, and the zone's skin ID. They're turned back into global ones with that zone's offsets (ADR 0006), so the truncation to 16 bits the packet does stays in.
        private static ObjectUpdatePercept ObjectUpdate(GameClient client, GameObject gameObject)
        {
            GamePlayer viewer = client.Player;

            if (gameObject == null || viewer == null || !viewer.IsVisibleTo(gameObject))
                return null;

            Zone zone = gameObject.CurrentZone;

            if (zone == null)
                return null;

            ushort xOffsetInZone;
            ushort yOffsetInZone;
            ushort z;
            ushort speed = 0;
            ushort heading;
            int targetId = 0;
            byte flags = 0;
            ObjectUpdateDestination destination = null;

            if (gameObject is not GameNPC npc)
            {
                xOffsetInZone = (ushort) (gameObject.X - zone.XOffset);
                yOffsetInZone = (ushort) (gameObject.Y - zone.YOffset);
                z = (ushort) gameObject.Z;
                heading = gameObject.Heading;
            }
            else
            {
                xOffsetInZone = (ushort) (npc.movementComponent.PositionForClient.X - zone.XOffset);
                yOffsetInZone = (ushort) (npc.movementComponent.PositionForClient.Y - zone.YOffset);
                z = (ushort) npc.movementComponent.PositionForClient.Z;
                flags = (byte) (GameServer.ServerRules.GetLivingRealm(viewer, npc) << 6);

                if (client.Account.PrivLevel < 2)
                {
                    if ((npc.Flags & GameNPC.eFlags.CANTTARGET) != 0)
                        flags |= 0x01;

                    if ((npc.Flags & GameNPC.eFlags.DONTSHOWNAME) != 0)
                        flags |= 0x02;
                }

                if ((npc.Flags & GameNPC.eFlags.STATUE) != 0)
                    flags |= 0x01;

                if (npc.IsUnderwater)
                    flags |= 0x10;

                if ((npc.Flags & GameNPC.eFlags.FLYING) != 0 || npc.movementComponent.ShouldForceFlyingFlag)
                    flags |= 0x20;

                if (!npc.IsMoving || npc.IsAtDestination)
                    heading = npc.Heading;
                else
                {
                    speed = (ushort) npc.movementComponent.HorizontalVelocityForClient;
                    int zSpeed = (int) (npc.movementComponent.Velocity.Z / 4);

                    if (zSpeed < 0)
                    {
                        zSpeed = -zSpeed;
                        speed |= 0x8000;
                    }

                    speed |= (ushort) ((zSpeed & 0x70) << 8);
                    heading = (ushort) (((zSpeed & 0xF) << 12) | npc.Heading);

                    if (npc.IsDestinationValid)
                    {
                        Zone targetZone = npc.CurrentRegion.GetZone((int) npc.movementComponent.DestinationForClient.X, (int) npc.movementComponent.DestinationForClient.Y);

                        if (targetZone != null)
                        {
                            destination = new(
                                (ushort) (npc.movementComponent.DestinationForClient.X - targetZone.XOffset) + targetZone.XOffset,
                                (ushort) (npc.movementComponent.DestinationForClient.Y - targetZone.YOffset) + targetZone.YOffset,
                                (ushort) npc.movementComponent.DestinationForClient.Z,
                                targetZone.ZoneSkinID & 0x1FF);
                        }
                    }
                }

                // The real target only while the NPC stands within reach of it, a placeholder while it attacks from anywhere else.
                if (npc.IsAttacking && !npc.IsTurningDisabled)
                {
                    GameObject target = npc.TargetObject;

                    if (target?.ObjectState is GameObject.eObjectState.Active && npc.CurrentSpeed == 0 && npc.IsWithinRadius(target, npc.attackComponent.AttackRange))
                        targetId = target.ObjectID;
                    else
                        targetId = 65535;
                }
            }

            // The vertical speed is split over the top bits of the speed and heading fields, with its sign at the very top of the speed's.
            int verticalSpeed = (speed >> 8 & 0x70) | heading >> 12;

            return new ObjectUpdatePercept(
                X: xOffsetInZone + zone.XOffset,
                Y: yOffsetInZone + zone.YOffset,
                Z: z,
                ObjectId: gameObject.ObjectID,
                Speed: speed & 0x0FFF,
                ZSpeed: (speed & 0x8000) != 0 ? -verticalSpeed : verticalSpeed,
                Heading: heading & 0x0FFF,
                TargetId: targetId,
                HealthPercent: gameObject is GameLiving living ? living.HealthPercent : 0,
                Realm: (PacketRealm) (flags >> 6),
                Untargetable: (flags & 0x01) != 0,
                NameHidden: (flags & 0x02) != 0,
                Underwater: (flags & 0x10) != 0,
                Flying: (flags & 0x20) != 0,
                ZoneId: zone.ZoneSkinID & 0x1FF,
                Destination: destination);
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
