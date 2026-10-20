using System;
using System.Reflection;
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
        private static readonly MethodInfo SendUDP = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUDP));
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
        public void Project_ShouldGiveTheObjectId_ForBothObjectDeleteCalls()
        {
            Zone zone = WorldDoubles.Zone(101, 524288, 458752, 65536, 65536);
            TestPlayer vue = PlayerIn(zone, "Vue");
            vue.ObjectID = 0x0123;

            Assert.That(PerceptProjection.Project(Playing(), SendObjectDeleteObject, [vue]), Is.EqualTo(new ObjectDeletePercept(0x0123)));
            Assert.That(PerceptProjection.Project(Playing(), SendObjectDeleteId, [(ushort) 0x0456]), Is.EqualTo(new ObjectDeletePercept(0x0456)));
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

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                return targetMethod.Name switch
                {
                    nameof(IServerRules.GetLivingRealm) => LivingRealm,
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
