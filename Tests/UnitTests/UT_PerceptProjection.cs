using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using DOL.AI;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.Agents;
using DOL.GS.Keeps;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using DOL.GS.ServerRules;
using DOL.GS.Spells;
using DOL.GS.Styles;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_PerceptProjection
    {
        private static readonly MethodInfo SendUpdateWeaponAndArmorStats = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUpdateWeaponAndArmorStats));
        private static readonly MethodInfo SendUpdateMoney = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUpdateMoney));
        private static readonly MethodInfo SendInventorySlotsUpdate = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendInventorySlotsUpdate));
        private static readonly MethodInfo SendInventoryItemsUpdate = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendInventoryItemsUpdate), [typeof(List<DbInventoryItem>)]);
        private static readonly MethodInfo SendInventoryItemsUpdateWithWindow = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendInventoryItemsUpdate), [typeof(eInventoryWindowType), typeof(List<DbInventoryItem>)]);
        private static readonly MethodInfo SendInventoryItemsUpdateWithSlots = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendInventoryItemsUpdate), [typeof(IDictionary<int, DbInventoryItem>), typeof(eInventoryWindowType)]);
        private static readonly MethodInfo SendEncumbrance = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendEncumbrance));
        private static readonly MethodInfo SendCustomTextWindow = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendCustomTextWindow));
        private static readonly MethodInfo SendCustomDialog = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendCustomDialog));
        private static readonly MethodInfo SendDialogBox = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendDialogBox));
        private static readonly MethodInfo SendGroupInviteCommand = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendGroupInviteCommand));
        private static readonly MethodInfo SendGuildInviteCommand = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendGuildInviteCommand));
        private static readonly MethodInfo SendGuildLeaveCommand = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendGuildLeaveCommand));
        private static readonly MethodInfo SendQuestSubscribeCommand = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendQuestSubscribeCommand));
        private static readonly MethodInfo SendQuestAbortCommand = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendQuestAbortCommand));
        private static readonly MethodInfo SendCharStatsUpdate =typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendCharStatsUpdate));
        private static readonly MethodInfo SendMerchantWindow = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendMerchantWindow));
        private static readonly MethodInfo SendUpdatePoints = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUpdatePoints));
        private static readonly MethodInfo SendMessage = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendMessage));
        private static readonly MethodInfo GetBowPrepare = typeof(IPacketLib).GetProperty(nameof(IPacketLib.BowPrepare)).GetMethod;
        private static readonly MethodInfo SendPlayerCreate = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendPlayerCreate));
        private static readonly MethodInfo SendObjectDeleteObject = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendObjectDelete), [typeof(GameObject)]);
        private static readonly MethodInfo SendObjectDeleteId = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendObjectDelete), [typeof(ushort)]);
        private static readonly MethodInfo SendNPCCreate = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendNPCCreate));
        private static readonly MethodInfo SendObjectCreate = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendObjectCreate));
        private static readonly MethodInfo SendObjectUpdate = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendObjectUpdate));
        private static readonly MethodInfo SendObjectRemove = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendObjectRemove));
        private static readonly MethodInfo SendPlayerPositionAndObjectID = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendPlayerPositionAndObjectID));
        private static readonly MethodInfo SendUpdateMaxSpeed = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUpdateMaxSpeed));
        private static readonly MethodInfo SendUpdateIcons = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUpdateIcons), [typeof(int).MakeByRefType(), typeof(bool)]);
        private static readonly MethodInfo SendUpdateIconsOld = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUpdateIcons), [typeof(System.Collections.IList), typeof(int).MakeByRefType()]);
        private static readonly MethodInfo SendPlayerJump = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendPlayerJump));
        private static readonly MethodInfo SendStatusUpdate = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendStatusUpdate), []);
        private static readonly MethodInfo SendStatusUpdateWithFlag = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendStatusUpdate), [typeof(byte)]);
        private static readonly MethodInfo SendUpdatePlayer = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUpdatePlayer));
        private static readonly MethodInfo SendPlayerDied = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendPlayerDied));
        private static readonly MethodInfo SendPlayerRevive = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendPlayerRevive));
        private static readonly MethodInfo SendUDP =typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUDP));
        private static readonly MethodInfo SendTCP = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendTCP));

        private Action _restoreServer;
        private TestRules _rules;

        [SetUp]
        public void SetUp()
        {
            IServerRules rules = DispatchProxy.Create<IServerRules, TestRules>();
            _rules = (TestRules) rules;
            _restoreServer = WorldDoubles.InstallServer(rules);
        }

        [TearDown]
        public void TearDown()
        {
            _restoreServer();
        }

        [Test]
        public void Project_ShouldGiveABarePerceptWithOnlyTheCallsName_ForACallWithoutAProjection()
        {
            Percept percept = PerceptProjection.Project(Playing(), SendUpdateWeaponAndArmorStats, []);

            Assert.That(percept, Is.EqualTo(new BarePercept("SendUpdateWeaponAndArmorStats")));
        }

        [Test]
        public void Project_ShouldGiveNothing_ForAPropertyRead()
        {
            Assert.That(PerceptProjection.Project(Playing(), GetBowPrepare, []), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveTheMessagesTextChatTypeAndLocation_ForTheMessageCall()
        {
            Percept percept = PerceptProjection.Project(Playing(), SendMessage, ["Vue says, \"hello\"", eChatType.CT_Say, eChatLoc.CL_ChatWindow]);

            Assert.That(percept, Is.EqualTo(new MessagePercept("Vue says, \"hello\"", eChatType.CT_Say, eChatLoc.CL_ChatWindow)));
        }

        [Test]
        public void Project_ShouldGiveAPopup_WhenThePacketMarksOne()
        {
            Percept percept = PerceptProjection.Project(Playing(), SendMessage, ["Welcome!", eChatType.CT_System, eChatLoc.CL_PopupWindow]);

            Assert.That(percept, Is.EqualTo(new MessagePercept("Welcome!", eChatType.CT_System, eChatLoc.CL_PopupWindow)));
        }

        // The 1.127 packet marks only popups. The client picks the chat or system window by chat type.
        [Test]
        public void Project_ShouldGiveTheChatWindow_ForTheSystemWindow()
        {
            Percept percept = PerceptProjection.Project(Playing(), SendMessage, ["You must say something...", eChatType.CT_System, eChatLoc.CL_SystemWindow]);

            Assert.That(percept, Is.EqualTo(new MessagePercept("You must say something...", eChatType.CT_System, eChatLoc.CL_ChatWindow)));
        }

        // A send line's packet carries the send marker instead of the popup marker.
        [Test]
        public void Project_ShouldGiveTheChatWindow_ForASendLineToAPopup()
        {
            Percept percept = PerceptProjection.Project(Playing(), SendMessage, ["Vue sends, \"hi\"", eChatType.CT_Send, eChatLoc.CL_PopupWindow]);

            Assert.That(percept, Is.EqualTo(new MessagePercept("Vue sends, \"hi\"", eChatType.CT_Send, eChatLoc.CL_ChatWindow)));
        }

        [Test]
        public void Project_ShouldGiveTheTextAsThePacketEncodesIt()
        {
            Percept percept = PerceptProjection.Project(Playing(), SendMessage, ["Café 5€ 日本", eChatType.CT_Say, eChatLoc.CL_ChatWindow]);

            Assert.That(percept, Is.EqualTo(new MessagePercept("Café 5€ ??", eChatType.CT_Say, eChatLoc.CL_ChatWindow)));
        }

        // The client filtered the chat type out, so the packet library sends nothing.
        [Test]
        public void Project_ShouldGiveNothing_ForAMessageOfADisabledChatType()
        {
            GameClient client = Playing();
            client.DisabledChatTypes.Add(eChatType.CT_OthersCombat);

            Assert.That(PerceptProjection.Project(client, SendMessage, ["Vue hits the rat.", eChatType.CT_OthersCombat, eChatLoc.CL_SystemWindow]), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveNothing_ForAMessageAtTheCharacterScreen()
        {
            GameClient client = Playing();
            client.ClientState = GameClient.eClientState.CharScreen;

            Assert.That(PerceptProjection.Project(client, SendMessage, ["Hello", eChatType.CT_System, eChatLoc.CL_SystemWindow]), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveWhatThePacketCarries_ForThePlayerCreateCall()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestPlayer vue = WorldDoubles.Player(WorldDoubles.Client(0x0304), "Vue");
            vue.Region = zone.ZoneRegion;
            (vue.X, vue.Y, vue.Z) = (531234, 478901, 2345);
            vue.ObjectID = 0x0123;
            vue.TestHeading = 2048;
            vue.TestModel = 0x02BE;
            vue.DisplayLevel = 23;
            _rules.LivingRealm = (byte) eRealm.Midgard;

            foreach (eCharFacePart part in Enum.GetValues<eCharFacePart>())
                vue.SetFace(part, (byte) (0x10 + (int) part));

            Percept percept = PerceptProjection.Project(agent, SendPlayerCreate, [vue]);

            Assert.That(percept, Is.EqualTo(new PlayerCreatePercept(
                531234, 478901, 2345, 0x0304, 0x0123, 2048, 0x02BE, 23, PacketRealm.Midgard,
                false, false, false, false, false,
                0x10, 0x11, 0x16, 0x12, 0x13, 0x14, 0x15,
                "Vue", "Vue's guild", "Vue's last name", "Vue's prefix", "Vue's title", null)));
        }

        [Test]
        public void Project_ShouldGiveThePlayersStateFlags_ForThePlayerCreateCall()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestPlayer vue = PlayerIn(zone, "Vue");
            vue.Alive = false;
            vue.Underwater = true;
            vue.Stealthed = true;
            vue.Wireframe = true;
            vue.ClassId = (int) eCharacterClass.Vampiir;

            PlayerCreatePercept percept = (PlayerCreatePercept) PerceptProjection.Project(agent, SendPlayerCreate, [vue]);

            Assert.That((percept.Dead, percept.Swimming, percept.Stealthed, percept.Wireframe, percept.Vampiir), Is.EqualTo((true, true, true, true, true)));
        }

        // The packet has two bits for the realm.
        [Test]
        public void Project_ShouldGiveTheRealmsLowTwoBits_ForThePlayerCreateCall()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            _rules.LivingRealm = 0x07;

            PlayerCreatePercept percept = (PlayerCreatePercept) PerceptProjection.Project(agent, SendPlayerCreate, [PlayerIn(zone, "Vue")]);

            Assert.That(percept.Realm, Is.EqualTo(PacketRealm.Hibernia));
        }

        [Test]
        public void Project_ShouldGiveNothing_ForAPlayerCreateTheAgentCantSee()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestPlayer vue = PlayerIn(zone, "Vue");
            vue.Visible = false;

            Assert.That(PerceptProjection.Project(agent, SendPlayerCreate, [vue]), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveNothing_ForAPlayerCreateOutsideAnyZone()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestPlayer vue = PlayerIn(zone, "Vue");
            vue.X = 0;

            Assert.That(PerceptProjection.Project(agent, SendPlayerCreate, [vue]), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveNothing_ForAPlayerCreateBeforeTheAgentHasACharacter()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);

            Assert.That(PerceptProjection.Project(Playing(), SendPlayerCreate, [PlayerIn(zone, "Vue")]), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveWhatThePacketCarries_ForTheNpcCreateCall()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc rat = NpcIn(zone, "rat");
            rat.Place(531234, 478901, 2345);
            rat.ObjectID = 0x0456;
            rat.TestHeading = 3072;
            rat.TestModel = 0x0190;
            rat.Size = 40;
            rat.DisplayLevel = 3;
            rat.GuildName = "Vermin";
            _rules.LivingRealm = (byte) eRealm.None;

            Percept percept = PerceptProjection.Project(agent, SendNPCCreate, [rat]);

            Assert.That(percept, Is.EqualTo(new NpcCreatePercept(
                531234, 478901, 2345, 0x0456, 0, 0, 3072, 0x0190, 40, 3,
                false, PacketRealm.None, false, false, false, false, false, false, false, false, false,
                eQuestIndicator.None, "rat", "Vermin")));
        }

        [Test]
        public void Project_ShouldGiveTheNpcsFlags_ForTheNpcCreateCall()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc guard = NpcIn(zone, "guard");
            guard.Flags = GameNPC.eFlags.STATUE | GameNPC.eFlags.GHOST | GameNPC.eFlags.TORCH | GameNPC.eFlags.PEACE | GameNPC.eFlags.FLYING | GameNPC.eFlags.CANTTARGET | GameNPC.eFlags.DONTSHOWNAME | GameNPC.eFlags.STEALTH;
            guard.HasInventory = true;
            guard.OwnBrain = (ABrain) RuntimeHelpers.GetUninitializedObject(typeof(ControlledMobBrain));
            guard.DisplayLevel = 50;
            _rules.LivingRealm = (byte) eRealm.Albion;

            NpcCreatePercept percept = (NpcCreatePercept) PerceptProjection.Project(agent, SendNPCCreate, [guard]);

            Assert.That(
                (percept.Level, percept.Statue, percept.Realm, percept.Ghost, percept.Equipped, percept.Torch, percept.Peaceful, percept.Flying, percept.Pet, percept.Untargetable, percept.NameHidden, percept.Stealthed, percept.Name),
                Is.EqualTo((50, true, PacketRealm.Albion, true, true, true, true, true, true, true, true, true, "guard")));
        }

        // A GM's client shows the hiding flags as name suffixes instead.
        [Test]
        public void Project_ShouldGiveTheHidingFlagsInTheName_ForTheNpcCreateCallToAGm()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            agent.Account.PrivLevel = 3;
            TestNpc guard = NpcIn(zone, "guard");
            guard.Flags = GameNPC.eFlags.CANTTARGET | GameNPC.eFlags.DONTSHOWNAME;

            NpcCreatePercept percept = (NpcCreatePercept) PerceptProjection.Project(agent, SendNPCCreate, [guard]);

            Assert.That((percept.Untargetable, percept.NameHidden, percept.Name), Is.EqualTo((false, false, "[guard]-DOR-NON")));
        }

        [Test]
        public void Project_ShouldCutTheNamesToWhatThePacketHoldsForThem_ForTheNpcCreateCall()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc npc = NpcIn(zone, new string('n', 60));
            npc.GuildName = new string('g', 60);

            NpcCreatePercept percept = (NpcCreatePercept) PerceptProjection.Project(agent, SendNPCCreate, [npc]);

            Assert.That((percept.Name, percept.GuildName), Is.EqualTo((new string('n', 45), new string('g', 47))));
        }

        [TestCase(eQuestIndicator.Available)]
        [TestCase(eQuestIndicator.Finish)]
        [TestCase(eQuestIndicator.Lesson)]
        [TestCase(eQuestIndicator.Lore)]
        [TestCase(eQuestIndicator.Pending)]
        public void Project_ShouldGiveTheQuestIndicator_ForTheNpcCreateCall(eQuestIndicator indicator)
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc npc = NpcIn(zone, "Elvar");
            npc.QuestIndicator = indicator;

            NpcCreatePercept percept = (NpcCreatePercept) PerceptProjection.Project(agent, SendNPCCreate, [npc]);

            Assert.That(percept.QuestIndicator, Is.EqualTo(indicator));
        }

        // The packet has no bits for anything else.
        [Test]
        public void Project_ShouldGiveNoQuestIndicator_ForOneThePacketCantCarry()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc npc = NpcIn(zone, "Elvar");
            npc.QuestIndicator = eQuestIndicator.Available | eQuestIndicator.Lore;

            NpcCreatePercept percept = (NpcCreatePercept) PerceptProjection.Project(agent, SendNPCCreate, [npc]);

            Assert.That(percept.QuestIndicator, Is.EqualTo(eQuestIndicator.None));
        }

        [Test]
        public void Project_ShouldGiveTheSpeed_ForTheNpcCreateCallOnAMovingNpc()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc rat = NpcIn(zone, "rat");
            rat.Move(120, new Vector3(0, 120, 30), 120, new Vector3(rat.X, rat.Y + 1000, rat.Z + 250));

            NpcCreatePercept percept = (NpcCreatePercept) PerceptProjection.Project(agent, SendNPCCreate, [rat]);

            Assert.That((percept.Speed, percept.ZSpeed), Is.EqualTo((120, 30)));
        }

        [Test]
        public void Project_ShouldGiveNothing_ForAnNpcCreateTheAgentCantSee()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc rat = NpcIn(zone, "rat");
            rat.Visible = false;

            Assert.That(PerceptProjection.Project(agent, SendNPCCreate, [rat]), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveNothing_ForAnNpcCreateBeforeTheAgentHasACharacter()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);

            Assert.That(PerceptProjection.Project(Playing(), SendNPCCreate, [NpcIn(zone, "rat")]), Is.Null);
        }

        // The library sends a moving object create for it instead.
        [Test]
        public void Project_ShouldGiveABarePerceptNamingTheMovingObjectCreate_ForTheNpcCreateCallOnAMovingObject()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            GameMine mine = Mine(zone);
            _rules.AllowedToAttack = false;

            Assert.That(PerceptProjection.Project(agent, SendNPCCreate, [mine]), Is.EqualTo(new BarePercept("SendMovingObjectCreate")));
        }

        [Test]
        public void Project_ShouldGiveNothing_ForTheNpcCreateCallOnAnEnemysMine()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            GameMine mine = Mine(zone);
            _rules.AllowedToAttack = true;

            Assert.That(PerceptProjection.Project(agent, SendNPCCreate, [mine]), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveWhatThePacketCarries_ForTheObjectCreateCallOnAGrave()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            ((TestPlayer) agent.Player).HideGraves = false;
            GameGravestone grave = WorldObjectIn(zone, new GameGravestone(), "Greeen's Grave");
            (grave.X, grave.Y, grave.Z) = (531234, 478901, 2345);
            grave.ObjectID = 0x0789;
            grave.Heading = 1024;
            grave.Model = 637;
            grave.Realm = eRealm.Hibernia;

            Percept percept = PerceptProjection.Project(agent, SendObjectCreate, [grave]);

            Assert.That(percept, Is.EqualTo(new ObjectCreatePercept(531234, 478901, 2345, 0x0789, 1024, 637, PacketRealm.Hibernia, 0, false, false, false, "Greeen's Grave")));
        }

        [Test]
        public void Project_ShouldGiveTheDoorId_ForTheObjectCreateCallOnADoor()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            GameDoor door = WorldObjectIn(zone, new GameDoor(), "door");
            door.DoorId = 101000123;

            ObjectCreatePercept percept = (ObjectCreatePercept) PerceptProjection.Project(agent, SendObjectCreate, [door]);

            Assert.That((percept.Name, percept.DoorId), Is.EqualTo(("door", 101000123)));
        }

        // With the option that hides other players' Graves on.
        [Test]
        public void Project_ShouldGiveNothing_ForAnObjectCreateTheAgentCantSee()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            ((TestPlayer) agent.Player).HideGraves = true;
            GameGravestone grave = WorldObjectIn(zone, new GameGravestone(), "Vue's Grave");
            grave.OwnerID = "Vue";

            Assert.That(PerceptProjection.Project(agent, SendObjectCreate, [grave]), Is.Null);
        }

        [Test]
        public void Project_ShouldCutTheNameToWhatThePacketHoldsForIt_ForTheObjectCreateCall()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            GameStaticItem statue = WorldObjectIn(zone, new GameStaticItem(), new string('s', 60));

            ObjectCreatePercept percept = (ObjectCreatePercept) PerceptProjection.Project(agent, SendObjectCreate, [statue]);

            Assert.That(percept.Name, Is.EqualTo(new string('s', 48)));
        }

        [Test]
        public void Project_ShouldGiveTheObjectsFlags_ForTheObjectCreateCall()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            GameKeepBanner banner = WorldObjectIn(zone, new GameKeepBanner(), "banner");
            banner.Emblem = 0x01ABCD;

            ObjectCreatePercept percept = (ObjectCreatePercept) PerceptProjection.Project(agent, SendObjectCreate, [banner]);

            Assert.That((percept.Emblem, percept.KeepBanner, percept.Yours), Is.EqualTo((0x01ABCD, true, false)));
        }

        [Test]
        public void Project_ShouldMarkLootTheAgentMayPickUp_ForTheObjectCreateCall()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            GameMoney money = WorldObjectIn(zone, new GameMoney(100), "bag of coins");
            money.AddOwner(agent.Player);

            ObjectCreatePercept percept = (ObjectCreatePercept) PerceptProjection.Project(agent, SendObjectCreate, [money]);

            Assert.That((percept.Yours, percept.KeepBanner), Is.EqualTo((true, false)));
        }

        // The packet carries the position relative to the zone. The Percept has it global again.
        [Test]
        public void Project_ShouldGiveWhatThePacketCarriesWithAGlobalPosition_ForTheObjectUpdateCallOnAStandingNpc()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc rat = NpcIn(zone, "rat");
            rat.Place(531234, 478901, 2345);
            rat.ObjectID = 0x0456;
            rat.TestHeading = 3072;
            rat.Health = 75;
            rat.Underwater = true;
            _rules.LivingRealm = (byte) eRealm.Hibernia;

            Percept percept = PerceptProjection.Project(agent, SendObjectUpdate, [rat, true]);

            Assert.That(percept, Is.EqualTo(new ObjectUpdatePercept(
                531234, 478901, 2345, 0x0456, 0, 0, 3072, 0, 75, PacketRealm.Hibernia, false, false, true, false, 101, null)));
        }

        [Test]
        public void Project_ShouldGiveTheSpeedAndGlobalDestination_ForTheObjectUpdateCallOnAMovingNpc()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc rat = NpcIn(zone, "rat");
            rat.Place(531234, 478901, 2345);
            rat.TestHeading = 1024;
            // A quarter of the vertical speed is 0x5A, split over the top bits of both fields.
            rat.Move(191, new Vector3(150, 0, 360), 150.6, new Vector3(533234, 478901, 2845));

            ObjectUpdatePercept percept = (ObjectUpdatePercept) PerceptProjection.Project(agent, SendObjectUpdate, [rat, true]);

            Assert.That((percept.X, percept.Y, percept.Z, percept.Speed, percept.ZSpeed, percept.Heading, percept.Destination),
                Is.EqualTo((531234, 478901, 2345, 150, 90, 1024, new ObjectUpdateDestination(533234, 478901, 2845, 101))));
        }

        [Test]
        public void Project_ShouldGiveANegativeVerticalSpeed_ForTheObjectUpdateCallOnAnNpcGoingDown()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc rat = NpcIn(zone, "rat");
            rat.Move(191, new Vector3(150, 0, -360), 150, new Vector3(rat.X + 2000, rat.Y, rat.Z - 500));

            ObjectUpdatePercept percept = (ObjectUpdatePercept) PerceptProjection.Project(agent, SendObjectUpdate, [rat, true]);

            Assert.That((percept.Speed, percept.ZSpeed), Is.EqualTo((150, -90)));
        }

        // The library hides the real target unless the NPC stands within reach of it.
        [Test]
        public void Project_ShouldGiveThePlaceholderTarget_ForTheObjectUpdateCallOnAnNpcAttackingWhileMoving()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc rat = NpcIn(zone, "rat");
            rat.Attacking = true;
            rat.TargetObject = agent.Player;
            rat.Move(191, new Vector3(150, 0, 0), 150, new Vector3(rat.X + 2000, rat.Y, rat.Z));

            ObjectUpdatePercept percept = (ObjectUpdatePercept) PerceptProjection.Project(agent, SendObjectUpdate, [rat, true]);

            Assert.That(percept.TargetId, Is.EqualTo(65535));
        }

        [Test]
        public void Project_ShouldGiveTheNpcsFlags_ForTheObjectUpdateCall()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc guard = NpcIn(zone, "guard");
            guard.Flags = GameNPC.eFlags.CANTTARGET | GameNPC.eFlags.DONTSHOWNAME | GameNPC.eFlags.FLYING;

            ObjectUpdatePercept percept = (ObjectUpdatePercept) PerceptProjection.Project(agent, SendObjectUpdate, [guard, true]);

            Assert.That((percept.Untargetable, percept.NameHidden, percept.Flying), Is.EqualTo((true, true, true)));
        }

        [Test]
        public void Project_ShouldGiveOnlyAStatueAsUntargetable_ForTheObjectUpdateCallToAGm()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            agent.Account.PrivLevel = 3;
            TestNpc guard = NpcIn(zone, "guard");
            guard.Flags = GameNPC.eFlags.CANTTARGET | GameNPC.eFlags.DONTSHOWNAME;
            TestNpc statue = NpcIn(zone, "statue");
            statue.Flags = GameNPC.eFlags.STATUE;

            ObjectUpdatePercept guardsUpdate = (ObjectUpdatePercept) PerceptProjection.Project(agent, SendObjectUpdate, [guard, true]);
            ObjectUpdatePercept statuesUpdate = (ObjectUpdatePercept) PerceptProjection.Project(agent, SendObjectUpdate, [statue, true]);

            Assert.That((guardsUpdate.Untargetable, guardsUpdate.NameHidden, statuesUpdate.Untargetable), Is.EqualTo((false, false, true)));
        }

        [Test]
        public void Project_ShouldGiveTheGlobalPositionAndNoNpcFields_ForTheObjectUpdateCallOnAnotherObject()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            GameStaticItem item = (GameStaticItem) RuntimeHelpers.GetUninitializedObject(typeof(GameStaticItem));
            item.CurrentRegion = zone.ZoneRegion;
            (item.X, item.Y, item.Z) = (531234, 478901, 2345);
            item.ObjectID = 0x0789;
            item.Heading = 512;

            Percept percept = PerceptProjection.Project(agent, SendObjectUpdate, [item, false]);

            Assert.That(percept, Is.EqualTo(new ObjectUpdatePercept(
                531234, 478901, 2345, 0x0789, 0, 0, 512, 0, 0, PacketRealm.None, false, false, false, false, 101, null)));
        }

        [Test]
        public void Project_ShouldGiveNothing_ForAnObjectUpdateTheAgentCantSee()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            ((TestPlayer) agent.Player).Visible = false;

            Assert.That(PerceptProjection.Project(agent, SendObjectUpdate, [NpcIn(zone, "rat"), true]), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveNothing_ForAnObjectUpdateOutsideAnyZone()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient agent = AgentInWorld(zone);
            TestNpc rat = NpcIn(zone, "rat");
            rat.Place(0, 0, 0);

            Assert.That(PerceptProjection.Project(agent, SendObjectUpdate, [rat, true]), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveTheObjectId_ForBothObjectDeleteCalls()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            TestPlayer vue = PlayerIn(zone, "Vue");
            vue.ObjectID = 0x0123;

            Assert.That(PerceptProjection.Project(Playing(), SendObjectDeleteObject, [vue]), Is.EqualTo(new ObjectDeletePercept(0x0123)));
            Assert.That(PerceptProjection.Project(Playing(), SendObjectDeleteId, [(ushort) 0x0456]), Is.EqualTo(new ObjectDeletePercept(0x0456)));
        }

        // A player who quits leaves the world through `GameObject.RemoveFromWorld`.
        [Test]
        public void Project_ShouldGiveTheObjectIdAndPlayerType_ForTheObjectRemoveCallOnAPlayer()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            TestPlayer vue = PlayerIn(zone, "Vue");
            vue.ObjectID = 0x0123;

            Assert.That(PerceptProjection.Project(Playing(), SendObjectRemove, [vue]), Is.EqualTo(new ObjectRemovePercept(0x0123, 2)));
        }

        [Test]
        public void Project_ShouldGiveTheOtherType_ForTheObjectRemoveCallOnAnythingButAPlayerOrNpc()
        {
            GameStaticItem item = (GameStaticItem) RuntimeHelpers.GetUninitializedObject(typeof(GameStaticItem));
            item.ObjectID = 0x0456;

            Assert.That(PerceptProjection.Project(Playing(), SendObjectRemove, [item]), Is.EqualTo(new ObjectRemovePercept(0x0456, 0)));
        }

        // The server's own 1.127 builder, fed back through the decoder, gives back what went in.
        [Test]
        public void Project_ShouldDecodeThe1127PlayerPosition_ForARawUdpPacket()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient vuesClient = WorldDoubles.Client(0x0304);
            TestPlayer vue = WorldDoubles.Player(vuesClient, "Vue");
            vue.Region = zone.ZoneRegion;
            (vue.X, vue.Y, vue.Z) = (531234, 478901, 2346);
            vue.ObjectID = 0x0123;
            vue.Speed = 191;
            vue.FallSpeed = -12;
            vue.TestManaPercent = 80;
            vue.TestEndurancePercent = 95;
            vue.Roleplay = false;
            PlayerPositionUpdateHandler.StateFlags stateFlags = PlayerPositionUpdateHandler.StateFlags.SWIMMING;
            PlayerPositionUpdateHandler.ActionFlags actionFlags = PlayerPositionUpdateHandler.ActionFlags.TORCH;
            const ushort steedSeat = 2;
            const ushort heading = 0x3ABC;
            const byte health = 0x80 | 67;

            GSUDPPacketOut packet = PlayerPositionPacket("CreateOutPak1127", vuesClient, vue, stateFlags, steedSeat, heading, actionFlags, health);

            Assert.That(PerceptProjection.Project(Playing(), SendUDP, [packet]), Is.EqualTo(new PlayerPositionPercept(
                531234, 478901, 2346, 191, -12, 0x0304, 0x0123, 101,
                (int) stateFlags, steedSeat, 0xABC, 0x3, (int) actionFlags, false, 67, true, 80, 95)));
        }

        // Only the 1.127 layout is decoded. Older clients get another one.
        [Test]
        public void Project_ShouldGiveABarePacketPercept_ForAPlayerPositionOfAnotherLayout()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            GameClient vuesClient = WorldDoubles.Client(0x0304);
            TestPlayer vue = WorldDoubles.Player(vuesClient, "Vue");
            vue.Region = zone.ZoneRegion;
            (vue.X, vue.Y) = (531234, 478901);

            GSUDPPacketOut packet = PlayerPositionPacket("CreateOutPak1124", vuesClient, vue, 0, 0, 0, 0, 100);

            Assert.That(PerceptProjection.Project(Playing(), SendUDP, [packet]), Is.EqualTo(new BarePacketPercept("SendUDP", 0xA9)));
        }

        [Test]
        public void Project_ShouldGiveABarePacketPerceptWithItsCode_ForAnyOtherRawPacket()
        {
            GSUDPPacketOut udp = new GSUDPPacketOut().Init((byte) eServerPackets.PlayerHeading);
            udp.WriteShort(0x0304);
            GSTCPPacketOut tcp = new GSTCPPacketOut().Init((byte) eServerPackets.ObjectUpdate);
            tcp.WriteShort(0x0123);

            Assert.That(PerceptProjection.Project(Playing(), SendUDP, [udp]), Is.EqualTo(new BarePacketPercept("SendUDP", 0xBA)));
            Assert.That(PerceptProjection.Project(Playing(), SendTCP, [tcp]), Is.EqualTo(new BarePacketPercept("SendTCP", (int) eServerPackets.ObjectUpdate)));
        }

        // The packet is shared with every other client it goes to.
        [Test]
        public void Project_ShouldLeaveARawPacketAsItWas()
        {
            GSUDPPacketOut udp = new GSUDPPacketOut().Init((byte) eServerPackets.PlayerHeading);
            udp.WriteShort(0x0304);
            byte[] before = udp.ToArray();
            long position = udp.Position;

            PerceptProjection.Project(Playing(), SendUDP, [udp]);

            Assert.That((udp.ToArray(), udp.Position), Is.EqualTo((before, position)));
        }

        [Test]
        public void Project_ShouldGiveWhatThePacketCarries_ForTheOwnPositionCall()
        {
            Zone zone = WorldDoubles.Region(201, (201, 524288, 458752, 65536, 65536)).Zones[0];
            GameClient client = AgentInWorld(zone);
            TestPlayer greeen = (TestPlayer) client.Player;
            (greeen.X, greeen.Y, greeen.Z) = (531234, 478901, 2345);
            greeen.ObjectID = 0x0123;
            greeen.TestHeading = 2048;

            Percept percept = PerceptProjection.Project(client, SendPlayerPositionAndObjectID, []);

            Assert.That(percept, Is.EqualTo(new OwnPositionPercept(531234, 478901, 2345, 0x0123, 2048, 0, 0, 201, false, false, null)));
        }

        // In a dungeon the packet carries the zone's offsets, in units of 8192.
        [Test]
        public void Project_ShouldGiveTheZonesOffsets_ForTheOwnPositionCallInADungeon()
        {
            Zone zone = WorldDoubles.Region(20, (20, 16384, 40960, 32768, 32768)).Zones[0];
            GameClient client = AgentInWorld(zone);

            OwnPositionPercept percept = (OwnPositionPercept) PerceptProjection.Project(client, SendPlayerPositionAndObjectID, []);

            Assert.That((percept.ZoneXOffset, percept.ZoneYOffset, percept.Region), Is.EqualTo((2, 5, 20)));
        }

        [Test]
        public void Project_ShouldGiveNothing_ForTheOwnPositionCallOutsideAnyZone()
        {
            Zone zone = WorldDoubles.Region(201, (201, 524288, 458752, 65536, 65536)).Zones[0];
            GameClient client = AgentInWorld(zone);
            client.Player.X = 0;

            Assert.That(PerceptProjection.Project(client, SendPlayerPositionAndObjectID, []), Is.Null);
        }

        // The packet carries a percentage of the base speed of 191: 117 for 225, rounded down.
        [Test]
        public void Project_ShouldGiveWhatThePacketCarries_ForTheMaxSpeedCall()
        {
            Zone zone = WorldDoubles.Region(201, (201, 524288, 458752, 65536, 65536)).Zones[0];
            GameClient client = AgentInWorld(zone);
            TestPlayer greeen = (TestPlayer) client.Player;
            greeen.Modified[eProperty.MaxSpeed] = 225;
            greeen.Modified[eProperty.WaterSpeed] = 80;
            greeen.TurningDisabled = true;

            Percept percept = PerceptProjection.Project(client, SendUpdateMaxSpeed, []);

            Assert.That(percept, Is.EqualTo(new MaxSpeedPercept(117, true, 80)));
        }

        [Test]
        public void Project_ShouldGiveNothing_ForTheMaxSpeedCallBeforeTheAgentHasACharacter()
        {
            Assert.That(PerceptProjection.Project(Playing(), SendUpdateMaxSpeed, []), Is.Null);
        }

        // The packet carries whole seconds left, and 0 for an Effect without a timer. The client lists the Effects by when they started.
        [Test]
        public void Project_ShouldGiveEveryEffectInTheBuffBar_ForTheIconsCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Region(201, (201, 524288, 458752, 65536, 65536)).Zones[0]);
            TestPlayer greeen = (TestPlayer) client.Player;
            WorldDoubles.Effect(greeen, "speed", 3060, true, 599_500).StartTick = 2;
            WorldDoubles.Effect(greeen, "slow", 4200, false, 0).StartTick = 1;

            EffectsPercept percept = (EffectsPercept) PerceptProjection.Project(client, SendUpdateIcons, [0, false]);
            EffectsPercept fromOldOverload = (EffectsPercept) PerceptProjection.Project(client, SendUpdateIconsOld, [null, 0]);

            Assert.That(percept.Effects, Is.EqualTo(new EffectEntry[] { new("slow", false), new("speed", true, 599) }));
            Assert.That(fromOldOverload.Effects, Is.EqualTo(percept.Effects));
        }

        [Test]
        public void Project_ShouldLeaveOutEffectsTheBuffBarDoesntShow_ForTheIconsCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Region(201, (201, 524288, 458752, 65536, 65536)).Zones[0]);
            TestPlayer greeen = (TestPlayer) client.Player;
            WorldDoubles.Effect(greeen, "speed", 3060, true, 600_000);
            WorldDoubles.Effect(greeen, "no icon", 0, true, 600_000);
            WorldDoubles.Effect(greeen, "pulse", 3061, true, 600_000, eEffect.Pulse);

            EffectsPercept percept = (EffectsPercept) PerceptProjection.Project(client, SendUpdateIcons, [0, false]);

            Assert.That(percept.Effects, Is.EqualTo(new EffectEntry[] { new("speed", true, 600) }));
        }

        // As the client's window shows a Hastener's buff: "Speed of the Realm", Level 1, Duration 10 minutes, Target Realm area effect, Range 1500, Area radius 350, Casting time Instant.
        [Test]
        public void Project_ShouldGiveASpellEffectsDetails_ForTheIconsCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Region(201, (201, 524288, 458752, 65536, 65536)).Zones[0]);
            TestPlayer greeen = (TestPlayer) client.Player;
            Spell speed = new(new DbSpell { Name = "Speed of the Realm", SpellID = 2430, Icon = 3060, Target = "Realm", Type = "SpeedEnhancement", Range = 1500, Radius = 350, Value = 147, Duration = 600 }, 1);
            WorldDoubles.SpellEffect(greeen, new SpeedEnhancementSpellHandler(greeen, speed, null), 314_000);

            EffectsPercept percept = (EffectsPercept) PerceptProjection.Project(client, SendUpdateIcons, [0, false]);

            Assert.That(percept.Effects.Single() with { Details = null }, Is.EqualTo(new EffectEntry("Speed of the Realm", true, 314)));
            Assert.That(percept.Effects.Single().Details, Is.EqualTo(new SpellDetails(1, 600, null, "Realm area effect", 1500, 0, null, 350, "The target's speed is increased to 147% of normal for 10 minutes.")));
        }

        // As the client's window shows "Aura of Global Feedback": Level 44, Duration 20 minutes, Power cost 28, Target Realm, Range 1000, Casting time 4.0 seconds, Magic Type Energy.
        [Test]
        public void Project_ShouldGiveASpellEffectsPowerCostCastingTimeAndMagicType_WhenItHasThem_ForTheIconsCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Region(201, (201, 524288, 458752, 65536, 65536)).Zones[0]);
            TestPlayer greeen = (TestPlayer) client.Player;
            Spell aura = new(new DbSpell { Name = "Aura of Global Feedback", SpellID = 2431, Icon = 3061, Target = "Realm", Type = "DamageShield", Range = 1000, Damage = 4.6, DamageType = (int) eDamageType.Energy, Duration = 1200, Power = 28, CastTime = 4 }, 44);
            WorldDoubles.SpellEffect(greeen, new DamageShieldSpellHandler(greeen, aura, null), 1_492_000);

            EffectsPercept percept = (EffectsPercept) PerceptProjection.Project(client, SendUpdateIcons, [0, false]);

            Assert.That(percept.Effects.Single().Details, Is.EqualTo(new SpellDetails(44, 1200, 28, "Realm", 1000, 4, "Energy", null, "The target's melee attackers receive 4.6 damage per second for 20 minutes.")));
        }

        // A Human's client shows an ability's Effect, such as Sprint, by its name and time left only.
        [Test]
        public void Project_ShouldGiveNoDetails_ForAnAbilitysEffect()
        {
            GameClient client = AgentInWorld(WorldDoubles.Region(201, (201, 524288, 458752, 65536, 65536)).Zones[0]);
            WorldDoubles.Effect((TestPlayer) client.Player, "Sprint", 3062, true, 0);

            EffectsPercept percept = (EffectsPercept) PerceptProjection.Project(client, SendUpdateIcons, [0, false]);

            Assert.That(percept.Effects.Single().Details, Is.Null);
        }

        [Test]
        public void Project_ShouldGiveAnEmptyBuffBar_ForTheIconsCallWithoutEffects()
        {
            GameClient client = AgentInWorld(WorldDoubles.Region(201, (201, 524288, 458752, 65536, 65536)).Zones[0]);

            EffectsPercept percept = (EffectsPercept) PerceptProjection.Project(client, SendUpdateIcons, [0, true]);

            Assert.That(percept.Effects, Is.Empty);
        }

        [Test]
        public void Project_ShouldGiveNothing_ForTheIconsCallBeforeTheAgentHasACharacter()
        {
            Assert.That(PerceptProjection.Project(Playing(), SendUpdateIcons, [0, false]), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveWhatThePacketCarries_ForTheJumpCall()
        {
            Zone zone = WorldDoubles.Region(201, (201, 524288, 458752, 65536, 65536)).Zones[0];
            GameClient client = AgentInWorld(zone);
            TestPlayer greeen = (TestPlayer) client.Player;
            (greeen.X, greeen.Y, greeen.Z) = (531234, 478901, 2345);
            greeen.ObjectID = 0x0123;
            greeen.TestHeading = 2048;

            Percept percept = PerceptProjection.Project(client, SendPlayerJump, [false]);

            Assert.That(percept, Is.EqualTo(new JumpPercept(531234, 478901, 2345, 0x0123, 2048, 0)));
        }

        // As `/faceloc` sends it: the client keeps its position and takes only the heading.
        [Test]
        public void Project_ShouldGiveNoPosition_ForTheJumpCallWithOnlyTheHeading()
        {
            Zone zone = WorldDoubles.Region(201, (201, 524288, 458752, 65536, 65536)).Zones[0];
            GameClient client = AgentInWorld(zone);
            TestPlayer greeen = (TestPlayer) client.Player;
            greeen.ObjectID = 0x0123;
            greeen.TestHeading = 1024;

            Percept percept = PerceptProjection.Project(client, SendPlayerJump, [true]);

            Assert.That(percept, Is.EqualTo(new JumpPercept(0, 0, 0, 0x0123, 1024, 0)));
        }

        // The packet carries Z in 16 bits.
        [Test]
        public void Project_ShouldGiveZAsThePacketCarriesIt_ForTheJumpCall()
        {
            Zone zone = WorldDoubles.Region(201, (201, 524288, 458752, 65536, 65536)).Zones[0];
            GameClient client = AgentInWorld(zone);
            client.Player.Z = -10;

            JumpPercept percept = (JumpPercept) PerceptProjection.Project(client, SendPlayerJump, [false]);

            Assert.That(percept.Z, Is.EqualTo(65526));
        }

        [Test]
        public void Project_ShouldGiveNothing_ForTheJumpCallBeforeTheAgentHasACharacter()
        {
            Assert.That(PerceptProjection.Project(Playing(), SendPlayerJump, [false]), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveWhatThePacketCarries_ForTheStatusCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            TestPlayer greeen = (TestPlayer) client.Player;
            (greeen.TestHealthPercent, greeen.TestManaPercent, greeen.TestEndurancePercent, greeen.TestConcentrationPercent) = (45, 10, 90, 20);
            (greeen.TestMaxMana, greeen.TestMaxEndurance, greeen.TestMaxConcentration, greeen.TestMaxHealth) = (30, 100, 15, 120);
            (greeen.TestHealth, greeen.TestEndurance, greeen.TestMana, greeen.TestConcentration) = (54, 90, 3, 3);
            greeen.Sitting = true;

            Percept percept = PerceptProjection.Project(client, SendStatusUpdate, []);

            Assert.That(percept, Is.EqualTo(new StatusPercept(45, 10, true, 90, 20, 30, 100, 15, 120, 54, 90, 3, 3)));
        }

        // The server passes the sitting flag itself while the Character sits down or stands up, before it has changed.
        [TestCase((byte) 0x02, true)]
        [TestCase((byte) 0x00, false)]
        public void Project_ShouldTakeTheSittingFlagTheServerPasses_ForTheStatusCall(byte sittingFlag, bool sitting)
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            ((TestPlayer) client.Player).Sitting = !sitting;

            StatusPercept percept = (StatusPercept) PerceptProjection.Project(client, SendStatusUpdateWithFlag, [sittingFlag]);

            Assert.That(percept.Sitting, Is.EqualTo(sitting));
        }

        [Test]
        public void Project_ShouldGiveWhatThePacketCarries_ForThePointsCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            TestPlayer greeen = (TestPlayer) client.Player;
            (greeen.TestRealmPoints, greeen.TestLevelPermill, greeen.TestSpecPoints, greeen.TestBountyPoints) = (1200, 512, 3, 40);
            (greeen.TestRealmSpecPoints, greeen.TestChampionLevelPermill, greeen.TestExperience, greeen.TestExperienceForNextLevel) = (1, 0, 3456, 6000);

            Percept percept = PerceptProjection.Project(client, SendUpdatePoints, []);

            Assert.That(percept, Is.EqualTo(new PointsPercept(1200, 512, 3, 40, 1, 0, 3456, 6000)));
        }

        [Test]
        public void Project_ShouldGiveWhatThePacketCarries_ForThePlayerCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            TestPlayer greeen = (TestPlayer) client.Player;
            greeen.DisplayLevel = 4;
            greeen.TestMaxHealth = 120;
            ICharacterClass characterClass = greeen.CharacterClass;

            Percept percept = PerceptProjection.Project(client, SendUpdatePlayer, []);

            Assert.That(percept, Is.EqualTo(new PlayerPercept(4, "Greeen", characterClass.Name, characterClass.BaseName, 120)));
        }

        [Test]
        public void Project_ShouldGiveNothing_ForTheStatusPointsAndPlayerCallsBeforeTheAgentHasACharacter()
        {
            Assert.That(PerceptProjection.Project(Playing(), SendStatusUpdate, []), Is.Null);
            Assert.That(PerceptProjection.Project(Playing(), SendUpdatePoints, []), Is.Null);
            Assert.That(PerceptProjection.Project(Playing(), SendUpdatePlayer, []), Is.Null);
        }

        [Test]
        public void SkillEntries_ShouldListEachSkillWithItsLevelAndPage_AndAStylesLine()
        {
            Specialization blades = new("Blades", "Blades", 0) { Level = 3 };
            Ability sprint = new("Sprint", "Sprint", "", 0, 0, 1, 0);
            Style shiningBlade = new(new DbStyle { Name = "Shining Blade", SpecKeyName = "Blades", SpecLevelRequirement = 2 }, null);

            SkillEntry[] entries = PerceptProjection.SkillEntries([(blades, blades), (sprint, sprint), (shiningBlade, blades)], [shiningBlade]);

            Assert.That(entries.Select(static entry => entry with { Details = null }), Is.EqualTo(new SkillEntry[] { new("Blades", 3, SkillPage.Specialization), new("Sprint", 1, SkillPage.Abilities), new("Shining Blade", 2, SkillPage.Styles, "Blades") }));
        }

        // A Blademaster's Blades styles, as in the database: Return Blade after a parry, Glowing Blade from the side, Lunging Blade after Return Blade.
        private static readonly Style ShiningBlade = new(new DbStyle { ID = 245, Name = "Shining Blade", SpecKeyName = "Blades", SpecLevelRequirement = 2, EnduranceCost = 15, GrowthRate = 0.5 }, null);
        private static readonly Style ReturnBlade = new(new DbStyle { ID = 246, Name = "Return Blade", SpecKeyName = "Blades", SpecLevelRequirement = 4, OpeningRequirementType = 1, AttackResultRequirement = 3, EnduranceCost = 5, BonusToHit = 10, GrowthRate = 0.75 }, null);
        private static readonly Style GlowingBlade = new(new DbStyle { ID = 249, Name = "Glowing Blade", SpecKeyName = "Blades", SpecLevelRequirement = 10, OpeningRequirementType = 2, OpeningRequirementValue = 1, EnduranceCost = 10, BonusToDefense = 5 }, null);
        private static readonly Style LungingBlade = new(new DbStyle { ID = 250, Name = "Lunging Blade", SpecKeyName = "Blades", SpecLevelRequirement = 12, OpeningRequirementValue = 246, AttackResultRequirement = 7, EnduranceCost = 10 }, null);

        // What the server's forced tooltip update tells the client about each style it lists (`SpellDelve.GetStyleString`), which the client shows when its icon is hovered.
        [Test]
        public void SkillEntries_ShouldGiveEachStyleTheDetailsItsTooltipShows()
        {
            Specialization blades = new("Blades", "Blades", 0) { Level = 10 };
            Style[] allStyles = [ShiningBlade, ReturnBlade, GlowingBlade, LungingBlade];

            SkillEntry[] entries = PerceptProjection.SkillEntries([(blades, blades), (ShiningBlade, blades), (ReturnBlade, blades), (GlowingBlade, blades)], allStyles);

            Assert.That(entries[0].Details, Is.Null, "a specialization has none");
            Assert.That(entries[1].Details with { FollowupStyles = null }, Is.EqualTo(new StyleDetails(Style.eOpening.Offensive, Style.eAttackResultRequirement.Any, null, null, null, 15, 0, 0, 100, false, null)));
            Assert.That(entries[2].Details with { FollowupStyles = null }, Is.EqualTo(new StyleDetails(Style.eOpening.Defensive, Style.eAttackResultRequirement.Parry, null, null, null, 5, 10, 0, 150, false, null)));
            Assert.That(entries[2].Details.FollowupStyles, Is.EqualTo(new[] { "Lunging Blade" }), "styles it opens, even one not trained yet");
            Assert.That(entries[3].Details with { FollowupStyles = null }, Is.EqualTo(new StyleDetails(Style.eOpening.Positional, Style.eAttackResultRequirement.Any, Style.eOpeningPosition.Side, null, null, 10, 0, 5, 0, false, null)));
            Assert.That(entries[1].Details.FollowupStyles, Is.Empty);
        }

        [Test]
        public void SkillEntries_ShouldNameTheStyleAFollowUpNeeds()
        {
            Specialization blades = new("Blades", "Blades", 0) { Level = 12 };

            SkillEntry[] entries = PerceptProjection.SkillEntries([(blades, blades), (LungingBlade, blades)], [ShiningBlade, ReturnBlade, GlowingBlade, LungingBlade]);

            Assert.That(entries[1].Details.Opening, Is.EqualTo(Style.eOpening.Offensive));
            Assert.That(entries[1].Details.OpeningResult, Is.EqualTo(Style.eAttackResultRequirement.Style));
            Assert.That(entries[1].Details.OpeningStyle, Is.EqualTo("Return Blade"));
        }

        [Test]
        public void SkillEntries_ShouldGiveAStyleNoLine_WhenThePacketHasNoIndexForItsSpec()
        {
            Style style = new(new DbStyle { Name = "Odd Style", SpecKeyName = "No Such Spec", SpecLevelRequirement = 1 }, null);

            SkillEntry[] entries = PerceptProjection.SkillEntries([(style, style)], [style]);

            Assert.That(entries.Select(static entry => entry with { Details = null }), Is.EqualTo(new SkillEntry[] { new("Odd Style", 1, SkillPage.Styles) }));
        }

        [Test]
        public void Project_ShouldGiveWhoDiedAndWhoKilledThem_ForTheDeathCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            TestPlayer greeen = (TestPlayer) client.Player;
            greeen.ObjectID = 0x0123;
            TestNpc wolf = WorldDoubles.Npc("wolf pup");
            wolf.ObjectID = 0x1F0B;

            Assert.That(PerceptProjection.Project(client, SendPlayerDied, [greeen, wolf]), Is.EqualTo(new DiedPercept(0x0123, 0x1F0B)));
            Assert.That(PerceptProjection.Project(client, SendPlayerDied, [greeen, null]), Is.EqualTo(new DiedPercept(0x0123, 0)));
            Assert.That(PerceptProjection.Project(client, SendPlayerRevive, [greeen]), Is.EqualTo(new RevivedPercept(0x0123)));
        }

        [Test]
        public void Project_ShouldGiveEachSlotWithItsItemAsThePacketWritesIt_ForTheInventorySlotsCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            TestInventory inventory = WorldDoubles.Inventory((TestPlayer) client.Player);
            inventory.Items[eInventorySlot.RightHandWeapon] = Item(new() { Name = "falcata", Level = 2, DPS_AF = 22, SPD_ABS = 34, Hand = 2, Type_Damage = 2, Object_Type = (int) eObjectType.Blades, Item_Type = (int) eInventorySlot.LeftHandWeapon, Weight = 25, MaxCondition = 100, MaxDurability = 100, Quality = 85, Model = 445, Color = 7 }, condition: 50);
            inventory.Items[eInventorySlot.FirstBackpack + 1] = Item(new() { Name = "bear claw", Level = 1, Object_Type = (int) eObjectType.GenericItem, Item_Type = (int) eInventorySlot.FirstBackpack, Weight = 2, MaxCondition = 100, MaxDurability = 100, Quality = 100, Model = 100 }, count: 300);

            InventoryPercept percept = (InventoryPercept) PerceptProjection.Project(client, SendInventorySlotsUpdate, [new List<eInventorySlot> { eInventorySlot.RightHandWeapon, eInventorySlot.FirstBackpack, eInventorySlot.FirstBackpack + 1 }]);

            Assert.That(percept.Window, Is.EqualTo(eInventoryWindowType.Update));
            Assert.That(percept.Slots.Select(static slot => slot.Slot), Is.EqualTo(new[] { 10, 40, 41 }));
            Assert.That(percept.Slots[0].Item with { SpellNames = null }, Is.EqualTo(new InventoryItem("falcata", 2, 22, 34, 1, 2, 2, (int) eObjectType.Blades, (int) eInventorySlot.LeftHandWeapon, 25, 50, 100, 85, 0, 0, 445, 7, 0, null)));
            Assert.That(percept.Slots[0].Item.SpellNames, Is.Empty);
            Assert.That(percept.Slots[1].Item, Is.Null);
            // A generic item's count fills both value fields, a stack's name starts with it, and its weight is the whole stack's.
            Assert.That(percept.Slots[2].Item with { SpellNames = null }, Is.EqualTo(new InventoryItem("300 bear claw", 1, 0, 0, 300, 0, 0, (int) eObjectType.GenericItem, (int) eInventorySlot.FirstBackpack, 600, 100, 100, 100, 0, 0, 100, 0, 0, null)));
        }

        // The packet doesn't carry an item's type, but the client works out its slot from the icon (ADR 0006).
        [Test]
        public void Project_ShouldGiveEachItemsType_ForTheInventorySlotsCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            TestInventory inventory = WorldDoubles.Inventory((TestPlayer) client.Player);
            inventory.Items[eInventorySlot.FirstBackpack] = Item(new() { Name = "Protector's Wrap", Level = 11, Object_Type = (int) eObjectType.Magical, Item_Type = (int) eInventorySlot.RightRing, Model = 103 });
            inventory.Items[eInventorySlot.FirstBackpack + 1] = Item(new() { Name = "dirk", Level = 3, Object_Type = (int) eObjectType.Piercing, Item_Type = (int) eInventorySlot.LeftHandWeapon, Hand = 2 });
            inventory.Items[eInventorySlot.FirstBackpack + 2] = Item(new() { Name = "Personal Bind Recall Stone", Object_Type = (int) eObjectType.Magical, Item_Type = (int) eInventorySlot.FirstBackpack });

            InventoryPercept percept = (InventoryPercept) PerceptProjection.Project(client, SendInventorySlotsUpdate, [new List<eInventorySlot> { eInventorySlot.FirstBackpack, eInventorySlot.FirstBackpack + 1, eInventorySlot.FirstBackpack + 2 }]);

            Assert.That(percept.Slots.Select(static slot => slot.Item.ItemType), Is.EqualTo(new[] { 36, 11, 40 }));
        }

        [Test]
        public void Project_ShouldGiveTheWindowAndTheItemsSlots_ForTheInventoryItemsCalls()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            TestInventory inventory = WorldDoubles.Inventory((TestPlayer) client.Player);
            DbInventoryItem helm = Item(new() { Name = "rawhide helm", Level = 2, DPS_AF = 4, SPD_ABS = 10, Object_Type = (int) eObjectType.Leather });
            helm.SlotPosition = (int) eInventorySlot.HeadArmor;
            inventory.Items[eInventorySlot.HeadArmor] = helm;

            InventoryPercept update = (InventoryPercept) PerceptProjection.Project(client, SendInventoryItemsUpdate, [new List<DbInventoryItem> { helm, null }]);
            InventoryPercept window = (InventoryPercept) PerceptProjection.Project(client, SendInventoryItemsUpdateWithWindow, [eInventoryWindowType.Equipment, new List<DbInventoryItem> { helm }]);
            InventoryPercept opened = (InventoryPercept) PerceptProjection.Project(client, SendInventoryItemsUpdateWithWindow, [eInventoryWindowType.Inventory, null]);

            Assert.That((update.Window, update.Slots.Single().Slot, update.Slots.Single().Item.Name), Is.EqualTo((eInventoryWindowType.Update, 21, "rawhide helm")));
            Assert.That((window.Window, window.Slots.Single().Slot), Is.EqualTo((eInventoryWindowType.Equipment, 21)));
            Assert.That((opened.Window, opened.Slots), Is.EqualTo((eInventoryWindowType.Inventory, Array.Empty<InventorySlot>())));
        }

        // A vault's window numbers its own slots, and the items come with them.
        [Test]
        public void Project_ShouldGiveTheSlotsAndItemsItIsGiven_ForTheInventoryItemsCallWithSlots()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            DbInventoryItem claw = Item(new() { Name = "bear claw", Object_Type = (int) eObjectType.GenericItem });

            InventoryPercept percept = (InventoryPercept) PerceptProjection.Project(client, SendInventoryItemsUpdateWithSlots, [new Dictionary<int, DbInventoryItem> { [110] = claw, [111] = null }, eInventoryWindowType.HouseVault]);

            Assert.That(percept.Window, Is.EqualTo(eInventoryWindowType.HouseVault));
            Assert.That((percept.Slots[0].Slot, percept.Slots[0].Item.Name, percept.Slots[1].Slot, percept.Slots[1].Item), Is.EqualTo((110, "bear claw", 111, (InventoryItem) null)));
        }

        [Test]
        public void Project_ShouldGiveNoSlots_ForTheInventorySlotsCallWithoutSlots()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));

            InventoryPercept percept = (InventoryPercept) PerceptProjection.Project(client, SendInventorySlotsUpdate, [null]);

            Assert.That(percept.Slots, Is.Empty);
        }

        [Test]
        public void Project_ShouldGiveTheCapacityAndTheWeightCarried_ForTheEncumbranceCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            TestPlayer greeen = (TestPlayer) client.Player;
            greeen.Modified[eProperty.Strength] = 72;
            WorldDoubles.Inventory(greeen).Weight = 58;

            Percept percept = PerceptProjection.Project(client, SendEncumbrance, []);

            Assert.That(percept, Is.EqualTo(new EncumbrancePercept(72, 58)));
        }

        [Test]
        public void Project_ShouldGiveEachCoin_ForTheMoneyCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            TestPlayer greeen = (TestPlayer) client.Player;
            (greeen.TestCopper, greeen.TestSilver, greeen.TestGold, greeen.TestMithril, greeen.TestPlatinum) = (82, 1, 3, 0, 0);

            Percept percept = PerceptProjection.Project(client, SendUpdateMoney, []);

            Assert.That(percept, Is.EqualTo(new MoneyPercept(82, 1, 3, 0, 0)));
        }

        // The packet marks a custom Dialog with the client's session ID and 1, and asks yes or no only when the server waits for an answer.
        [Test]
        public void Project_ShouldGiveTheTextAndWhatTheAnswerEchoes_ForTheCustomDialogCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            CustomDialogResponse response = static (_, _) => { };

            Percept asking = PerceptProjection.Project(client, SendCustomDialog, ["It will cost 2 silver. Do you accept?", response]);
            Percept telling = PerceptProjection.Project(client, SendCustomDialog, ["Done.", null]);

            Assert.That(asking, Is.EqualTo(new DialogPercept(eDialogCode.CustomDialog, 0x0101, 0x01, 0x00, true, "It will cost 2 silver. Do you accept?")));
            Assert.That(telling, Is.EqualTo(new DialogPercept(eDialogCode.CustomDialog, 0x0101, 0x01, 0x00, false, "Done.")));
        }

        [Test]
        public void Project_ShouldGiveNothing_ForTheCustomDialogCallBeforeTheAgentHasACharacter()
        {
            Assert.That(PerceptProjection.Project(Playing(), SendCustomDialog, ["Do you accept?", null]), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveTheCodeDataTypeAndText_ForTheDialogBoxCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));

            Percept percept = PerceptProjection.Project(client, SendDialogBox, [eDialogCode.GroupInvite, (ushort) 0x0304, (ushort) 0x01, (ushort) 0x02, (ushort) 0x03, eDialogType.YesNo, true, "Dalta has invited you to join a group."]);

            Assert.That(percept, Is.EqualTo(new DialogPercept(eDialogCode.GroupInvite, 0x0304, 0x01, 0x02, true, "Dalta has invited you to join a group.")));
        }

        // A portal stone opens the realm's warmap through the same call, which is a map to pick a destination on, not a question.
        [Test]
        public void Project_ShouldGiveNothing_ForTheDialogBoxCallOpeningAWarmap()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));

            Assert.That(PerceptProjection.Project(client, SendDialogBox, [eDialogCode.WarmapWindowHibernia, (ushort) 0, (ushort) 0, (ushort) 0, (ushort) 0, eDialogType.Warmap, false, ""]), Is.Null);
        }

        // Each writes the dialog packet itself: an invite carries the inviter's session ID, leaving a guild its object ID, and a quest its ID, the NPC's object ID, and 0 to take it or 1 to abort it.
        [Test]
        public void Project_ShouldGiveWhatTheAnswerEchoes_ForTheInviteGuildLeaveAndQuestCalls()
        {
            Zone zone = WorldDoubles.Zone(201, 0, 0, 65536, 65536);
            GameClient client = AgentInWorld(zone);
            TestPlayer dalta = PlayerIn(zone, "Dalta");
            dalta.ObjectID = 0x0123;
            TestNpc ionhar = WorldDoubles.Npc("Ionhar");
            ionhar.ObjectID = 0x0456;

            Assert.That(PerceptProjection.Project(client, SendGroupInviteCommand, [dalta, "Dalta has invited you to join a group."]),
                Is.EqualTo(new DialogPercept(eDialogCode.GroupInvite, 0x0304, 0x00, 0x00, true, "Dalta has invited you to join a group.")));
            Assert.That(PerceptProjection.Project(client, SendGuildInviteCommand, [dalta, "Dalta has invited you into Greenwood."]),
                Is.EqualTo(new DialogPercept(eDialogCode.GuildInvite, 0x0304, 0x00, 0x00, true, "Dalta has invited you into Greenwood.")));
            Assert.That(PerceptProjection.Project(client, SendGuildLeaveCommand, [dalta, "Do you really want to leave Greenwood?"]),
                Is.EqualTo(new DialogPercept(eDialogCode.GuildLeave, 0x0123, 0x00, 0x00, true, "Do you really want to leave Greenwood?")));
            Assert.That(PerceptProjection.Project(client, SendQuestSubscribeCommand, [ionhar, (ushort) 7, "Will you help?"]),
                Is.EqualTo(new DialogPercept(eDialogCode.QuestSubscribe, 7, 0x0456, 0x00, true, "Will you help?")));
            Assert.That(PerceptProjection.Project(client, SendQuestAbortCommand, [ionhar, (ushort) 7, "Abort the quest?"]),
                Is.EqualTo(new DialogPercept(eDialogCode.QuestSubscribe, 7, 0x0456, 0x01, true, "Abort the quest?")));
        }

        // A Healer sends only the stats packet after restoring Constitution, so it's the max health it carries that the client shows.
        [Test]
        public void Project_ShouldGiveTheConstitutionLostAndMaxHealth_ForTheStatsCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            TestPlayer greeen = (TestPlayer) client.Player;
            greeen.TestMaxHealth = 496;

            Percept none = PerceptProjection.Project(client, SendCharStatsUpdate, []);
            (greeen.TestConstitutionLost, greeen.TestMaxHealth) = (5, 488);
            Percept lost = PerceptProjection.Project(client, SendCharStatsUpdate, []);

            Assert.That(none, Is.EqualTo(new StatsPercept(0, 496)));
            Assert.That(lost, Is.EqualTo(new StatsPercept(5, 488)));
        }

        [Test]
        public void Project_ShouldGiveNothing_ForTheStatsCallBeforeTheAgentHasACharacter()
        {
            Assert.That(PerceptProjection.Project(Playing(), SendCharStatsUpdate, []), Is.Null);
        }

        [Test]
        public void Project_ShouldGiveTheCaptionAndEachLine_ForTheDetailWindowCall()
        {
            string longLine = new('x', 300);

            ExaminedPercept percept = (ExaminedPercept) PerceptProjection.Project(Playing(), SendCustomTextWindow, ["falcata", new List<string> { "Magical Bonuses:", null, "- 3 Strength", longLine }]);

            Assert.That(percept.Caption, Is.EqualTo("falcata"));
            // A null line is skipped, and one longer than 255 characters split.
            Assert.That(percept.Lines, Is.EqualTo(new[] { "Magical Bonuses:", "- 3 Strength", longLine[..255], longLine[255..] }));
            Assert.That(PerceptProjection.Project(Playing(), SendCustomTextWindow, ["falcata", null]), Is.Null);
        }

        [Test]
        public void Project_ShouldStopAtThePacketsLength_ForTheDetailWindowCall()
        {
            List<string> lines = [.. Enumerable.Repeat(new string('x', 100), 30)];

            ExaminedPercept percept = (ExaminedPercept) PerceptProjection.Project(Playing(), SendCustomTextWindow, ["caption", lines]);

            // 2048 bytes: 3 of header, 2 of flags, 8 of caption and 102 for each whole line. The last is cut to fit after its number, its length and the packet's trailing 0.
            Assert.That(percept.Lines, Has.Length.EqualTo(20));
            Assert.That(percept.Lines[..19], Has.All.Length.EqualTo(100));
            Assert.That(percept.Lines[19], Has.Length.EqualTo(2048 - 13 - 19 * 102 - 1 - 2));
        }

        [Test]
        public void Project_ShouldGiveEveryItemOnEveryPageAsThePacketWritesIt_ForTheMerchantWindowCall()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));
            MerchantTradeItems trade = new(null);
            DbItemTemplate shortSword = new() { Name = "short sword", Level = 1, DPS_AF = 16, SPD_ABS = 30, Hand = 2, Type_Damage = 2, Object_Type = (int) eObjectType.Blades, Item_Type = (int) eInventorySlot.LeftHandWeapon, Weight = 20, Price = 150, Model = 444 };
            DbItemTemplate broadsword = new() { Name = "broadsword", Level = 3, DPS_AF = 26, SPD_ABS = 38, Type_Damage = 2, Object_Type = (int) eObjectType.Blades, Item_Type = (int) eInventorySlot.RightHandWeapon, Weight = 30, Price = 350, Model = 447 };
            DbItemTemplate arrows = new() { Name = "arrows", Level = 1, SPD_ABS = 2, Object_Type = (int) eObjectType.Arrow, Item_Type = (int) eInventorySlot.FirstBackpack, Weight = 1, PackSize = 20, Price = 12, Model = 1500 };
            trade.AddTradeItem(0, eMerchantWindowSlot.FirstInPage, shortSword);
            trade.AddTradeItem(0, (eMerchantWindowSlot) 2, arrows);
            trade.AddTradeItem(1, (eMerchantWindowSlot) 1, broadsword);
            _rules.Unusable = broadsword;

            MerchantWindowPercept percept = (MerchantWindowPercept) PerceptProjection.Project(client, SendMerchantWindow, [trade, eMerchantWindowType.Normal]);

            Assert.That(percept.Window, Is.EqualTo(eMerchantWindowType.Normal));
            Assert.That(percept.Items, Is.EqualTo(new MerchantItem[]
            {
                new(0, 0, 1, 16, 30, 1, 2, 2, (int) eObjectType.Blades, (int) eInventorySlot.LeftHandWeapon, true, 20, 150, 444, "short sword"),
                // A pack's count and its whole weight.
                new(0, 2, 1, 0, 2, 20, 0, 0, (int) eObjectType.Arrow, (int) eInventorySlot.FirstBackpack, true, 20, 12, 1500, "arrows"),
                new(1, 1, 3, 26, 38, 1, 0, 2, (int) eObjectType.Blades, (int) eInventorySlot.RightHandWeapon, false, 30, 350, 447, "broadsword")
            }));
        }

        [Test]
        public void Project_ShouldGiveAnEmptyWindow_ForTheMerchantWindowCallWithoutItems()
        {
            GameClient client = AgentInWorld(WorldDoubles.Zone(201, 0, 0, 65536, 65536));

            MerchantWindowPercept percept = (MerchantWindowPercept) PerceptProjection.Project(client, SendMerchantWindow, [null, eMerchantWindowType.Normal]);

            Assert.That(percept.Items, Is.Empty);
        }

        [Test]
        public void Project_ShouldGiveNothing_ForTheInventoryEncumbranceMoneyAndMerchantCallsBeforeTheAgentHasACharacter()
        {
            Assert.That(PerceptProjection.Project(Playing(), SendInventorySlotsUpdate, [null]), Is.Null);
            Assert.That(PerceptProjection.Project(Playing(), SendEncumbrance, []), Is.Null);
            Assert.That(PerceptProjection.Project(Playing(), SendUpdateMoney, []), Is.Null);
            Assert.That(PerceptProjection.Project(Playing(), SendMerchantWindow, [null, eMerchantWindowType.Normal]), Is.Null);
        }

        private static DbInventoryItem Item(DbItemTemplate template, int count = 1, int condition = 100)
        {
            template.MaxCondition = template.MaxCondition == 0 ? 100 : template.MaxCondition;
            template.MaxDurability = template.MaxDurability == 0 ? 100 : template.MaxDurability;
            return new DbInventoryItem { Template = template, Count = count, Condition = condition, Durability = template.MaxDurability, Color = template.Color };
        }

        private static GameClient AgentInWorld(Zone zone)
        {
            GameClient client = WorldDoubles.Client(0x0101);
            client.ClientState = GameClient.eClientState.Playing;
            client.Account = new DbAccount { PrivLevel = 1 };
            TestPlayer greeen = WorldDoubles.Player(client, "Greeen");
            greeen.Region = zone.ZoneRegion;
            (greeen.X, greeen.Y) = (zone.XOffset + 1000, zone.YOffset + 1000);
            return client;
        }

        private static TestPlayer PlayerIn(Zone zone, string name)
        {
            TestPlayer player = WorldDoubles.Player(WorldDoubles.Client(0x0304), name);
            player.Region = zone.ZoneRegion;
            (player.X, player.Y) = (zone.XOffset + 2000, zone.YOffset + 2000);
            return player;
        }

        private static TestNpc NpcIn(Zone zone, string name)
        {
            TestNpc npc = WorldDoubles.Npc(name);
            npc.Region = zone.ZoneRegion;
            npc.Place(zone.XOffset + 3000, zone.YOffset + 3000, 1000);
            return npc;
        }

        // Named and placed before it's active, since a World object that's active sends itself again on every change.
        private static T WorldObjectIn<T>(Zone zone, T worldObject, string name) where T : GameObject
        {
            worldObject.Name = name;
            worldObject.CurrentRegion = zone.ZoneRegion;
            (worldObject.X, worldObject.Y, worldObject.Z) = (zone.XOffset + 3000, zone.YOffset + 3000, 1000);
            worldObject.ObjectState = GameObject.eObjectState.Active;
            return worldObject;
        }

        private static GameMine Mine(Zone zone)
        {
            GameMine mine = (GameMine) RuntimeHelpers.GetUninitializedObject(typeof(GameMine));
            mine.CurrentRegion = zone.ZoneRegion;
            (mine.X, mine.Y) = (zone.XOffset + 3000, zone.YOffset + 3000);
            return mine;
        }

        // Calls one of `PlayerPositionUpdateHandler`'s own position packet builders.
        private static GSUDPPacketOut PlayerPositionPacket(string builder, GameClient client, GamePlayer player, PlayerPositionUpdateHandler.StateFlags stateFlags, ushort steedSeat, ushort heading, PlayerPositionUpdateHandler.ActionFlags actionFlags, byte health)
        {
            MethodInfo method = typeof(PlayerPositionUpdateHandler).GetMethod(builder, BindingFlags.NonPublic | BindingFlags.Static);
            return (GSUDPPacketOut) method.Invoke(null, [client, player, stateFlags, steedSeat, heading, actionFlags, health]);
        }

        private static GameClient Playing()
        {
            return new(null) { ClientState = GameClient.eClientState.Playing };
        }

        // Names each name after the player, and gives the realm a test sets.
        public class TestRules : DispatchProxy
        {
            public byte LivingRealm { get; set; }
            public bool AllowedToAttack { get; set; }
            // The one item the Agent's class can't use.
            public DbItemTemplate Unusable { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                return targetMethod.Name switch
                {
                    nameof(IServerRules.GetLivingRealm) => LivingRealm,
                    nameof(IServerRules.IsAllowedToAttack) => AllowedToAttack,
                    nameof(IServerRules.GetPlayerName) => ((GamePlayer) args[1]).Name,
                    nameof(IServerRules.GetPlayerGuildName) => $"{((GamePlayer) args[1]).Name}'s guild",
                    nameof(IServerRules.GetPlayerLastName) => $"{((GamePlayer) args[1]).Name}'s last name",
                    nameof(IServerRules.GetPlayerPrefixName) => $"{((GamePlayer) args[1]).Name}'s prefix",
                    nameof(IServerRules.GetPlayerTitle) => $"{((GamePlayer) args[1]).Name}'s title",
                    nameof(IServerRules.CheckAbilityToUseItem) => args[1] != Unusable,
                    _ => throw new NotSupportedException(targetMethod.Name)
                };
            }
        }
    }
}
