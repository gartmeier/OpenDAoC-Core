using System;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using DOL.AI;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using DOL.GS.ServerRules;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_PerceptProjection
    {
        private static readonly MethodInfo SendUpdatePoints = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUpdatePoints));
        private static readonly MethodInfo SendMessage = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendMessage));
        private static readonly MethodInfo GetBowPrepare = typeof(IPacketLib).GetProperty(nameof(IPacketLib.BowPrepare)).GetMethod;
        private static readonly MethodInfo SendPlayerCreate = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendPlayerCreate));
        private static readonly MethodInfo SendObjectDeleteObject = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendObjectDelete), [typeof(GameObject)]);
        private static readonly MethodInfo SendObjectDeleteId = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendObjectDelete), [typeof(ushort)]);
        private static readonly MethodInfo SendNPCCreate = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendNPCCreate));
        private static readonly MethodInfo SendObjectUpdate = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendObjectUpdate));
        private static readonly MethodInfo SendObjectRemove = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendObjectRemove));
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
            Percept percept = PerceptProjection.Project(Playing(), SendUpdatePoints, []);

            Assert.That(percept, Is.EqualTo(new BarePercept("SendUpdatePoints")));
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
            vue.Mana = 80;
            vue.Endurance = 95;
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
                    _ => throw new NotSupportedException(targetMethod.Name)
                };
            }
        }
    }
}
