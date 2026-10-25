using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.ServerRules;
using DOL.GS.Styles;
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

        // `PacketLib168.MAX_PACKET_LENGTH`, and the most lines `PacketLib168.WriteCustomTextWindowData` writes.
        private const int MAX_PACKET_LENGTH = 2048;
        private const int MAX_TEXT_LINES = 200;

        // `PacketLib189.MAX_NAME_LENGTH`, where an item's name is cut off.
        private const int MAX_ITEM_NAME_LENGTH = 55;

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
                nameof(IPacketLib.SendPlayerPositionAndObjectID) => OwnPosition(client),
                nameof(IPacketLib.SendPlayerJump) => Jump(client, (bool) args[0]),
                nameof(IPacketLib.SendUpdateMaxSpeed) => MaxSpeed(client),
                nameof(IPacketLib.SendUpdateIcons) => Effects(client),
                nameof(IPacketLib.SendStatusUpdate) => Status(client, args.Length > 0 ? (byte) args[0] : null),
                nameof(IPacketLib.SendUpdatePoints) => Points(client),
                nameof(IPacketLib.SendUpdatePlayer) => Player(client),
                nameof(IPacketLib.SendUpdatePlayerSkills) => Skills(client, (bool) args[0]),
                nameof(IPacketLib.SendPlayerDied) => Died((GamePlayer) args[0], (GameObject) args[1]),
                nameof(IPacketLib.SendPlayerRevive) => Revived((GamePlayer) args[0]),
                nameof(IPacketLib.SendInventorySlotsUpdate) => InventorySlots(client, eInventoryWindowType.Update, (List<eInventorySlot>) args[0]),
                nameof(IPacketLib.SendInventoryItemsUpdate) => InventoryItems(client, method, args),
                nameof(IPacketLib.SendEncumbrance) => Encumbrance(client),
                nameof(IPacketLib.SendUpdateMoney) => Money(client),
                nameof(IPacketLib.SendCustomTextWindow) => Examined((string) args[0], (IList<string>) args[1]),
                nameof(IPacketLib.SendMerchantWindow) => MerchantWindow(client, (MerchantTradeItems) args[0], (eMerchantWindowType) args[1]),
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

        // Mirrors `PacketLib1110.SendUpdateIcons`, which no later library overrides, for both overloads. The packet holds only the slots that changed since the last one, and the Percept the whole bar the client then shows.
        private static EffectsPercept Effects(GameClient client)
        {
            GamePlayer player = client.Player;

            if (player == null)
                return null;

            return new EffectsPercept([.. player.effectListComponent.GetSortedEffects(static effect => effect.EffectType is not eEffect.Pulse)
                .Where(static effect => effect.Icon != 0)
                .Select(static effect => new EffectEntry(Encoded(effect.Name), effect.HasPositiveEffect, SecondsLeft(effect)))]);
        }

        // The packet's whole seconds left, where 0 stands for no timer.
        private static int? SecondsLeft(ECSGameEffect effect)
        {
            ushort secondsLeft = (ushort) (effect.GetRemainingTimeForClient() / 1000);
            return secondsLeft > 0 ? secondsLeft : null;
        }

        // Mirrors `PacketLib190.SendStatusUpdate`, which no later library overrides. `sittingFlag` is the one the server passed, or null for `PacketLib168.SendStatusUpdate()`, which passes whether the Character sits.
        private static StatusPercept Status(GameClient client, byte? sittingFlag)
        {
            GamePlayer player = client.Player;

            if (player == null)
                return null;

            return new StatusPercept(
                HealthPercent: player.HealthPercent,
                ManaPercent: player.ManaPercent,
                Sitting: sittingFlag is byte flag ? flag != 0 : player.IsSitting,
                EndurancePercent: player.EndurancePercent,
                ConcentrationPercent: player.ConcentrationPercent,
                MaxMana: (ushort) player.MaxMana,
                MaxEndurance: (ushort) player.MaxEndurance,
                MaxConcentration: (ushort) player.MaxConcentration,
                MaxHealth: (ushort) player.MaxHealth,
                Health: (ushort) player.Health,
                Endurance: (ushort) player.Endurance,
                Mana: (ushort) player.Mana,
                Concentration: (ushort) player.Concentration);
        }

        // Mirrors `PacketLib190.SendUpdatePoints`, which no later library overrides.
        private static PointsPercept Points(GameClient client)
        {
            GamePlayer player = client.Player;

            if (player == null)
                return null;

            return new PointsPercept(
                RealmPoints: (uint) player.RealmPoints,
                LevelPermill: player.LevelPermill,
                SpecPoints: (ushort) player.SkillSpecialtyPoints,
                BountyPoints: (uint) player.BountyPoints,
                RealmSpecPoints: (ushort) player.RealmSpecialtyPoints,
                ChampionLevelPermill: player.ChampionLevelPermill,
                Experience: player.Experience,
                ExperienceForNextLevel: player.ExperienceForNextLevel);
        }

        // Mirrors `PacketLib179.SendUpdatePlayer`, which no later library overrides. The packet splits the maximum health over two bytes.
        private static PlayerPercept Player(GameClient client)
        {
            GamePlayer player = client.Player;

            if (player == null)
                return null;

            return new PlayerPercept(player.GetDisplayLevel(player), Encoded(player.Name), Encoded(player.CharacterClass.Name), Encoded(player.CharacterClass.BaseName), player.MaxHealth & 0xFFFF);
        }

        // Mirrors `PacketLib1112.SendUpdatePlayerSkills`, which no later library overrides. Like it, it asks the Character for its skills, with `updateInternalCache` as passed:
        // the server keeps the list it last sent, to read the client's skill numbers against, and the real library never runs for the Agent.
        private static SkillsPercept Skills(GameClient client, bool updateInternalCache)
        {
            GamePlayer player = client.Player;

            if (player == null)
                return null;

            // The library sends each style's tooltip alongside, naming follow-ups among every style the Character's specializations have up to the highest level, as `SpellDelve.GetStyleString` does.
            List<Style> allStyles = [.. player.GetSpecList().SelectMany(spec => spec.PretendStylesForLiving(player, GamePlayer.MAX_LEVEL))];
            return new SkillsPercept(SkillEntries(player.GetAllUsableSkills(updateInternalCache), allStyles));
        }

        // Each skill as the packet lists it: a style with the specialization level it needs and its specialization, which the packet carries as an index (none for 0), anything else with its own level.
        // A style also has the details its tooltip shows, with follow-ups and opening styles found among `allStyles`.
        public static SkillEntry[] SkillEntries(IEnumerable<(Skill Skill, Skill Related)> skills, IReadOnlyCollection<Style> allStyles)
        {
            return skills.Select(entry => entry.Skill is Style style
                ? new SkillEntry(Encoded(style.Name), style.SpecLevelRequirement, SkillPage.Styles, GlobalConstants.GetSpecToInternalIndex(style.Spec) != 0 ? Encoded(style.Spec) : null, Details(style, allStyles))
                : new SkillEntry(Encoded(entry.Skill.Name), entry.Skill.Level, (SkillPage) entry.Skill.SkillType)).ToArray();
        }

        // Mirrors `SpellDelve.GetStyleString`, without the icon and the proc, which is a spell's delve of its own.
        private static StyleDetails Details(Style style, IReadOnlyCollection<Style> allStyles)
        {
            bool afterStyle = style.OpeningRequirementType is Style.eOpening.Offensive && style.AttackResultRequirement is Style.eAttackResultRequirement.Style;
            string openingStyle = afterStyle ? allStyles.FirstOrDefault(other => other.ID == style.OpeningRequirementValue)?.Name : null;
            string[] followupStyles = [.. allStyles
                .Where(other => other.OpeningRequirementType is Style.eOpening.Offensive && other.AttackResultRequirement is Style.eAttackResultRequirement.Style && other.OpeningRequirementValue == style.ID)
                .Select(static other => Encoded(other.Name))];

            return new StyleDetails(
                style.OpeningRequirementType,
                style.AttackResultRequirement,
                style.OpeningRequirementType is Style.eOpening.Positional ? (Style.eOpeningPosition) style.OpeningRequirementValue : null,
                openingStyle == null ? null : Encoded(openingStyle),
                followupStyles,
                style.EnduranceCost,
                style.BonusToHit,
                style.BonusToDefense,
                (float) (style.GrowthRate * 200),
                style.StealthRequirement,
                style.WeaponTypeRequirement > 0 ? Encoded(style.GetRequiredWeaponName()) : null);
        }

        // Mirrors `PacketLib168.SendPlayerDied`, which no later library overrides.
        private static DiedPercept Died(GamePlayer killed, GameObject killer)
        {
            return killed == null ? null : new DiedPercept(killed.ObjectID, killer?.ObjectID ?? 0);
        }

        // Mirrors `PacketLib168.SendPlayerRevive`, which no later library overrides.
        private static RevivedPercept Revived(GamePlayer revived)
        {
            return revived == null ? null : new RevivedPercept(revived.ObjectID);
        }

        // Mirrors the three `SendInventoryItemsUpdate` overloads: `PacketLib168`'s two with a list of items, which send the slots the items are in, and `PacketLib189`'s with the slots the client numbers them by.
        // The library splits a long update over several packets. The Mind gets it as one.
        private static InventoryPercept InventoryItems(GameClient client, MethodInfo method, object[] args)
        {
            ParameterInfo[] parameters = method.GetParameters();

            if (parameters[0].ParameterType == typeof(IDictionary<int, DbInventoryItem>))
                return client.Player == null ? null : InventoryPercept((eInventoryWindowType) args[1], ((IDictionary<int, DbInventoryItem>) args[0] ?? new Dictionary<int, DbInventoryItem>()).Select(static entry => (entry.Key, entry.Value)));

            (eInventoryWindowType window, List<DbInventoryItem> items) = parameters.Length == 1 ? (eInventoryWindowType.Update, (List<DbInventoryItem>) args[0]) : ((eInventoryWindowType) args[0], (List<DbInventoryItem>) args[1]);
            return InventorySlots(client, window, items?.Where(static item => item != null).Select(static item => (eInventorySlot) item.SlotPosition).ToList() ?? []);
        }

        // Mirrors `PacketLib189.SendInventorySlotsUpdateRange`, which every inventory update by slot ends in: each slot with whatever the Character has in it now.
        private static InventoryPercept InventorySlots(GameClient client, eInventoryWindowType window, List<eInventorySlot> slots)
        {
            GamePlayer player = client.Player;

            if (player == null)
                return null;

            return InventoryPercept(window, (slots ?? []).Select(slot =>
            {
                // A consignment merchant's slots go as the housing ones they stand for.
                int clientSlot = slot is >= eInventorySlot.Consignment_First and <= eInventorySlot.Consignment_Last ? slot - eInventorySlot.Consignment_First + (int) eInventorySlot.HousingInventory_First : (int) slot;
                return (clientSlot, player.Inventory.GetItem(slot));
            }));
        }

        // The packet carries each slot in a byte.
        private static InventoryPercept InventoryPercept(eInventoryWindowType window, IEnumerable<(int Slot, DbInventoryItem Item)> slots)
        {
            return new(window, slots.Select(static slot => new InventorySlot((byte) slot.Slot, slot.Item == null ? null : InventoryItem(slot.Item))).ToArray());
        }

        // Mirrors `PacketLib1124.WriteItemData`, which 1.125 to 1.127 don't override. An empty slot is 24 bytes of 0.
        public static InventoryItem InventoryItem(DbInventoryItem item)
        {
            // Two value fields whose meaning depends on the object type.
            (int value1, int value2) = (eObjectType) item.Object_Type switch
            {
                eObjectType.GenericItem => (item.Count & 0xFF, item.Count >> 8 & 0xFF),
                eObjectType.Arrow or eObjectType.Bolt or eObjectType.Poison => (item.Count, item.SPD_ABS),
                eObjectType.Thrown => (item.DPS_AF, item.Count),
                eObjectType.Instrument => (item.DPS_AF == 2 ? 0 : item.DPS_AF, 0),
                eObjectType.Shield => (item.Type_Damage, item.DPS_AF),
                eObjectType.AlchemyTincture or eObjectType.SpellcraftGem => (0, 0),
                eObjectType.HouseWallObject or eObjectType.HouseFloorObject or eObjectType.GardenObject => (0, item.SPD_ABS),
                _ => (item.DPS_AF, item.SPD_ABS)
            };
            (value1, value2) = ((byte) value1, (byte) value2);

            // As the client reads them back.
            (int dpsAf, int speedAbs, int count) = (eObjectType) item.Object_Type switch
            {
                eObjectType.GenericItem => (0, 0, value1 | value2 << 8),
                eObjectType.Arrow or eObjectType.Bolt or eObjectType.Poison => (0, value2, value1),
                eObjectType.Thrown => (value1, 0, value2),
                // The first is the shield's size, which the damage type carries too.
                eObjectType.Shield => (value2, 0, 1),
                _ => (value1, value2, 1)
            };

            int hand = item.Object_Type == (int) eObjectType.GardenObject ? 0 : (byte) (item.Hand << 6) >> 6;
            byte damageAndObjectType = (byte) ((item.Type_Damage > 3 ? 0 : item.Type_Damage << 6) | item.Object_Type);
            List<string> spellNames = [];

            if ((eObjectType) item.Object_Type is not eObjectType.AlchemyTincture)
            {
                foreach (int spellId in new[] { item.SpellID, item.SpellID1 })
                {
                    if (spellId > 0 && SkillBase.GetSpellByID(spellId) is Spell spell)
                        spellNames.Add(Encoded(spell.Name));
                }
            }

            string name = item.Name;

            if (item.Count > 1)
                name = item.Count + " " + name;

            if (item.SellPrice > 0)
                name += ServerProperties.Properties.CONSIGNMENT_USE_BP ? $"[{item.SellPrice} BP]" : $"[{GS.Money.GetString(item.SellPrice)}]";

            name = Encoded(name);

            if (name.Length > MAX_ITEM_NAME_LENGTH)
                name = name[..MAX_ITEM_NAME_LENGTH];

            return new InventoryItem(
                Name: name,
                Level: (byte) item.Level,
                DpsAf: dpsAf,
                SpeedAbs: speedAbs,
                Count: count,
                Hand: hand,
                DamageType: damageAndObjectType >> 6,
                ObjectType: damageAndObjectType & 0x3F,
                Weight: (ushort) item.Weight,
                Condition: item.ConditionPercent,
                Durability: item.DurabilityPercent,
                Quality: (byte) item.Quality,
                Bonus: (byte) item.Bonus,
                BonusLevel: (byte) item.BonusLevel,
                Model: (ushort) item.Model,
                Color: item.Emblem != 0 ? (ushort) item.Emblem : (ushort) item.Color,
                Effect: (ushort) item.Effect,
                SpellNames: spellNames.ToArray());
        }

        // Mirrors `PacketLib168.SendEncumbrance`, which no later library overrides. Both are in pounds.
        private static EncumbrancePercept Encumbrance(GameClient client)
        {
            GamePlayer player = client.Player;
            return player == null ? null : new EncumbrancePercept((ushort) player.MaxCarryingCapacity, (ushort) player.Inventory.InventoryWeight);
        }

        // Mirrors `PacketLib168.SendUpdateMoney`, which no later library overrides.
        private static MoneyPercept Money(GameClient client)
        {
            GamePlayer player = client.Player;
            return player == null ? null : new MoneyPercept((byte) player.Copper, (byte) player.Silver, (ushort) player.Gold, (ushort) player.Mithril, (ushort) player.Platinum);
        }

        // Mirrors `PacketLib181.SendCustomTextWindow`, which no later library overrides, and the `PacketLib168.WriteCustomTextWindowData` it calls.
        // The packet ends where the library runs out of room in it, so the text does too.
        private static ExaminedPercept Examined(string caption, IList<string> text)
        {
            if (text == null)
                return null;

            caption = Encoded(caption);

            if (caption.Length > byte.MaxValue)
                caption = caption[..byte.MaxValue];

            // The header, two flags and the caption's length and characters.
            int position = TCP_HEADER_LENGTH + 2 + 1 + caption.Length;
            List<string> lines = [];

            foreach (string line in text)
            {
                if (line == null)
                    continue;

                // A line's number, its length, one character and the packet's trailing 0.
                if (position + 4 > MAX_PACKET_LENGTH)
                    break;

                position++;
                string rest = Encoded(line);
                bool full = false;

                while (rest.Length > byte.MaxValue)
                {
                    string part = rest[..byte.MaxValue];

                    if (position + part.Length + 2 > MAX_PACKET_LENGTH)
                    {
                        full = true;
                        break;
                    }

                    lines.Add(part);
                    position += 1 + part.Length;
                    rest = rest[byte.MaxValue..];

                    if (lines.Count >= MAX_TEXT_LINES || position + Math.Min(byte.MaxValue, rest.Length) + 2 >= MAX_PACKET_LENGTH)
                        return new ExaminedPercept(caption, lines.ToArray());

                    position++;
                }

                if (position + rest.Length + 2 > MAX_PACKET_LENGTH)
                {
                    rest = rest[..Math.Max(Math.Min(1, rest.Length), MAX_PACKET_LENGTH - position - 2)];
                    full = true;
                }

                lines.Add(rest);
                position += 1 + rest.Length;

                if (full || lines.Count >= MAX_TEXT_LINES)
                    break;
            }

            return new ExaminedPercept(caption, lines.ToArray());
        }

        // Mirrors `PacketLib1125.SendMerchantWindow`, which 1.126 and 1.127 don't override: one packet per page that has items.
        private static MerchantWindowPercept MerchantWindow(GameClient client, MerchantTradeItems tradeItems, eMerchantWindowType window)
        {
            GamePlayer player = client.Player;

            if (player == null)
                return null;

            List<MerchantItem> items = [];

            for (int page = 0; tradeItems != null && page < MerchantTradeItems.MAX_PAGES_IN_TRADEWINDOWS; page++)
            {
                System.Collections.IDictionary itemsInPage = tradeItems.GetItemsInPage(page);

                if (itemsInPage == null || itemsInPage.Count == 0)
                    continue;

                List<MerchantItem> pageItems = [];

                for (int place = 0; place < MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS; place++)
                {
                    if (!itemsInPage.Contains(place))
                        continue;

                    // The library gives up on the rest of the window, this page included, at an item it can't find.
                    if (itemsInPage[place] is not DbItemTemplate item)
                        return new MerchantWindowPercept(window, items.ToArray());

                    pageItems.Add(MerchantItem(player, page, place, item));
                }

                items.AddRange(pageItems);
            }

            return new MerchantWindowPercept(window, items.ToArray());
        }

        private static MerchantItem MerchantItem(GamePlayer player, int page, int place, DbItemTemplate item)
        {
            // The first value field, then the second, which the packet carries later in 16 bits.
            (int value1, int value2) = (eObjectType) item.Object_Type switch
            {
                eObjectType.Arrow or eObjectType.Bolt or eObjectType.Poison or eObjectType.GenericItem => (item.PackSize, item.PackSize * item.Weight),
                eObjectType.Thrown => (item.DPS_AF, item.PackSize),
                eObjectType.Shield => (item.Type_Damage, item.Weight),
                eObjectType.GardenObject => (0, item.Weight),
                _ => (item.DPS_AF, item.Weight)
            };
            (value1, value2) = ((byte) value1, (ushort) value2);

            // As the client reads them back. A shield's first value is its size, which the damage type carries too.
            (int dpsAf, int count, int weight) = (eObjectType) item.Object_Type switch
            {
                eObjectType.Arrow or eObjectType.Bolt or eObjectType.Poison or eObjectType.GenericItem => (0, value1, value2),
                eObjectType.Thrown => (value1, value2, 0),
                eObjectType.Shield or eObjectType.GardenObject => (0, 1, value2),
                _ => (value1, 1, value2)
            };

            byte damageAndObjectType = (byte) (item.Type_Damage << 6 | item.Object_Type);

            return new MerchantItem(
                Page: (byte) page,
                Place: (byte) place,
                Level: (byte) item.Level,
                DpsAf: dpsAf,
                SpeedAbs: (byte) item.SPD_ABS,
                Count: count,
                Hand: item.Object_Type == (int) eObjectType.GardenObject ? 0 : (byte) (item.Hand << 6) >> 6,
                DamageType: damageAndObjectType >> 6,
                ObjectType: damageAndObjectType & 0x3F,
                Usable: player.HasAbilityToUseItem(item),
                Weight: weight,
                Price: (uint) item.Price,
                Model: (ushort) item.Model,
                Name: Encoded(item.Name));
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
