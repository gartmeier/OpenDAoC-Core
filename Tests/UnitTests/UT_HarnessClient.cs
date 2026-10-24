using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_HarnessClient
    {
        private Action _restoreServer;

        // Game objects can't be made without a server (see `WorldDoubles`).
        [SetUp]
        public void SetUp()
        {
            _restoreServer = WorldDoubles.InstallServer(null);
        }

        [TearDown]
        public void TearDown()
        {
            _restoreServer();
        }

        [Test]
        public void Dispatch_ShouldRefuseASayWithAReason_WhileTheAgentIsntInTheWorld()
        {
            string reason = Dispatch(new SayPrimitive("hello"));

            Assert.That(reason, Does.EndWith("isn't in the world."));
        }

        [Test]
        public void Dispatch_ShouldRefuseAQuitWithAReason_WhileTheAgentIsntInTheWorld()
        {
            string reason = Dispatch(new QuitPrimitive());

            Assert.That(reason, Does.EndWith("isn't in the world."));
        }

        [Test]
        public void Dispatch_ShouldRefuseAMoveToWithAReason_WhileTheAgentIsntInTheWorld()
        {
            string reason = Dispatch(new MoveToPrimitive(1, 2, 3));

            Assert.That(reason, Does.EndWith("isn't in the world."));
        }

        [Test]
        public void Dispatch_ShouldRefuseAStopWithAReason_WhileTheAgentIsntInTheWorld()
        {
            string reason = Dispatch(new StopPrimitive());

            Assert.That(reason, Does.EndWith("isn't in the world."));
        }

        [Test]
        public void Dispatch_ShouldRefuseAFaceWithAReason_WhileTheAgentIsntInTheWorld()
        {
            string reason = Dispatch(new FacePrimitive(1024));

            Assert.That(reason, Does.EndWith("isn't in the world."));
        }

        [Test]
        public async Task Face_ShouldQueueTheHeadingUpdateTheCaptureShows()
        {
            // The first of the captured turn on the spot. The full packet is in the README.
            CapturedPacket captured = ClientCaptures.WhilePlaying(ClientCaptures.MOVEMENT, eClientPackets.PlayerHeadingUpdate).First();
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, captured.SessionId);

            string reason = client.Dispatch(new FacePrimitive(0x06D6), mind.Link);

            Assert.That(reason, Is.Null);
            ClientPacket[] queued = Inbound(client);
            Assert.That(queued, Has.Length.EqualTo(1));
            UT_ClientPackets1127.AssertMatches(queued[0], captured);
            await Close(mind);
        }

        // A running client sends no heading update. The run's next position update carries its own heading, and the run goes on.
        [Test]
        public async Task Face_ShouldQueueNothingAndLeaveTheRunGoing_DuringARun()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.OnPerceived(new MaxSpeedPercept(100, false, 100));
            client.Dispatch(new MoveToPrimitive(298516, 641415, 4848), mind.Link);

            string reason = client.Dispatch(new FacePrimitive(0x06D6), mind.Link);

            Assert.That(reason, Is.Null);
            Assert.That(Inbound(client), Is.Empty);
            await Close(mind);
            Assert.That(mind.Sent, Has.None.Matches<ServerMessage>(static message => message is PerceptMessage { Percept: StoppedPercept }));
        }

        [TestCase(-1)]
        [TestCase(4096)]
        public async Task Face_ShouldBeRefusedWithAReason_ForAHeadingOutsideAFullTurn(int heading)
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            string reason = client.Dispatch(new FacePrimitive(heading), mind.Link);

            Assert.That(reason, Does.StartWith("A heading is 0 to 4095"));
            Assert.That(Inbound(client), Is.Empty);
            await Close(mind);
        }

        // Until the server has read the heading update, the Character still has its old heading. A real client's next position update carries the one it turned to.
        [Test]
        public async Task PositionUpdate_ShouldCarryTheHeadingOfAFace_TheServerHasntReadYet()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            ((TestPlayer) client.Player).TestHeading = 100;

            client.Dispatch(new FacePrimitive(2000), mind.Link);

            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(client.PositionUpdate().Data.AsSpan(30)), Is.EqualTo(2000));
            await Close(mind);
        }

        [Test]
        public async Task Target_ShouldQueueTheTargetPacketTheCaptureShows()
        {
            CapturedPacket captured = ClientCaptures.WhilePlaying(ClientCaptures.MOVEMENT, eClientPackets.PlayerTarget).Single();
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, captured.SessionId);

            string reason = client.Dispatch(new TargetPrimitive(0x1F0B), mind.Link);

            Assert.That(reason, Is.Null);
            ClientPacket[] queued = Inbound(client);
            Assert.That(queued, Has.Length.EqualTo(1));
            UT_ClientPackets1127.AssertMatches(queued[0], captured);
            await Close(mind);
        }

        [TestCase(-1)]
        [TestCase(65536)]
        public async Task Target_ShouldBeRefusedWithAReason_ForAnObjectIdThePacketCantCarry(int objectId)
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            string reason = client.Dispatch(new TargetPrimitive(objectId), mind.Link);

            Assert.That(reason, Does.StartWith("An object ID is 0 to 65535"));
            Assert.That(Inbound(client), Is.Empty);
            await Close(mind);
        }

        // As the captured client's do: the server reads whether the target is in view from every position update, so leaving it out would take the target out of view.
        [Test]
        public async Task PositionUpdate_ShouldCarryTheTargetAndClaimItsInView_OnceSomethingIsTargeted()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            client.Dispatch(new TargetPrimitive(0x1F0B), mind.Link);

            ClientPacket update = client.PositionUpdate();
            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(update.Data.AsSpan(22)), Is.EqualTo(0x1F0B));
            Assert.That((PlayerPositionUpdateHandler.ActionFlags) update.Data[32], Is.EqualTo(PlayerPositionUpdateHandler.ActionFlags.TORCH | PlayerPositionUpdateHandler.ActionFlags.TARGET_IN_VIEW));
            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(client.HeadingUpdate(100).Data.AsSpan(2)), Is.EqualTo(0x1F0B));
            await Close(mind);
        }

        // A client forgets what it can't see any more, and its selection with it.
        [Test]
        public async Task PositionUpdate_ShouldCarryNoTarget_OnceTheTargetHasGoneOutOfView()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            ((TestPlayer) client.Player).ActionFlags |= PlayerPositionUpdateHandler.ActionFlags.TARGET_IN_VIEW;
            client.Dispatch(new TargetPrimitive(0x1F0B), mind.Link);

            client.OnPerceived(new ObjectDeletePercept(0x1F0B));

            ClientPacket update = client.PositionUpdate();
            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(update.Data.AsSpan(22)), Is.Zero);
            Assert.That((PlayerPositionUpdateHandler.ActionFlags) update.Data[32], Is.EqualTo(PlayerPositionUpdateHandler.ActionFlags.TORCH));
            await Close(mind);
        }

        [Test]
        public async Task Attack_ShouldQueueTheAttackRequest_AsTheHumanPressingTheButton()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            client.Dispatch(new AttackPrimitive(true), mind.Link);
            client.Dispatch(new AttackPrimitive(false), mind.Link);

            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([ClientPackets1127.AttackRequest(true), ClientPackets1127.AttackRequest(false)])));
            await Close(mind);
        }

        [Test]
        public async Task SitAndStand_ShouldQueueTheCommandsTheCaptureShows()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            client.Dispatch(new SitPrimitive(), mind.Link);
            client.Dispatch(new StandPrimitive(), mind.Link);

            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([ClientPackets1127.Sit(), ClientPackets1127.Stand()])));
            await Close(mind);
        }

        // As captured: the top bit of the health byte, in position and heading updates.
        [Test]
        public async Task PositionUpdate_ShouldSayItsAttacking_WhileInAttackMode()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            client.Dispatch(new AttackPrimitive(true), mind.Link);
            Assert.That(client.PositionUpdate().Data[35], Is.EqualTo(0x80 | 100));
            Assert.That(client.HeadingUpdate(100).Data[10], Is.EqualTo(0x80 | 100));

            client.Dispatch(new AttackPrimitive(false), mind.Link);
            Assert.That(client.PositionUpdate().Data[35], Is.EqualTo(100));
            await Close(mind);
        }

        [Test]
        public async Task Target_ShouldClearTheTarget_ForObjectId0()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.Dispatch(new TargetPrimitive(0x1F0B), mind.Link);

            client.Dispatch(new TargetPrimitive(0), mind.Link);

            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([ClientPackets1127.Target(0x1F0B), ClientPackets1127.ClearTarget(attacking: false)])));
            Assert.That(client.TargetId, Is.Zero);
            await Close(mind);
        }

        // As the captured client does when the Mob it attacks dies and leaves the world.
        [Test]
        public async Task OnPerceived_ShouldStopAttackingAndClearTheTarget_WhenTheTargetItAttacksLeavesTheWorld()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.Dispatch(new TargetPrimitive(0x1F0B), mind.Link);
            client.Dispatch(new AttackPrimitive(true), mind.Link);
            int queued = Inbound(client).Length;

            client.OnPerceived(new ObjectRemovePercept(0x1F0B, 0));

            Assert.That(Wire(Inbound(client)[queued..]), Is.EqualTo(Wire([ClientPackets1127.AttackRequest(false, byClient: true), ClientPackets1127.ClearTarget(attacking: true)])));
            Assert.That(client.PositionUpdate().Data[35], Is.EqualTo(100));
            await Close(mind);
        }

        [Test]
        public async Task Command_ShouldQueueTheCommandAsTheClientSendsIt()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            string reason = client.Dispatch(new CommandPrimitive("/train Blades 3"), mind.Link);

            Assert.That(reason, Is.Null);
            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([ClientPackets1127.Command("&train Blades 3")])));
            await Close(mind);
        }

        [TestCase("train Blades 3")]
        [TestCase("")]
        [TestCase(null)]
        public async Task Command_ShouldBeRefusedWithAReason_WithoutASlash(string text)
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            string reason = client.Dispatch(new CommandPrimitive(text), mind.Link);

            Assert.That(reason, Does.StartWith("A command starts with /"));
            Assert.That(Inbound(client), Is.Empty);
            await Close(mind);
        }

        [Test]
        public async Task ItemPrimitives_ShouldQueueTheRequestsTheClientSends()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            Assert.That(client.Dispatch(new MoveItemPrimitive(41, 10), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new ExaminePrimitive(41), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new ExamineMerchantItemPrimitive(31), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new BuyPrimitive(31, 2), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new SellPrimitive(43), mind.Link), Is.Null);

            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([
                ClientPackets1127.MoveItem(0x0101, 41, 10, 0),
                ClientPackets1127.Examine(41),
                ClientPackets1127.ExamineMerchantItem(31),
                ClientPackets1127.BuyRequest(296516, 641415, 0x0101, 31, 2),
                ClientPackets1127.SellRequest(296516, 641415, 0x0101, 43)])));
            await Close(mind);
        }

        // The server picks up and trades with the client's target, so a click on the object selects it first.
        [Test]
        public async Task PickUpAndTalkTo_ShouldTargetTheObjectFirst_WhenItIsntTargeted()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            client.Dispatch(new PickUpPrimitive(0x1F0B), mind.Link);
            client.Dispatch(new TalkToPrimitive(0x2C54), mind.Link);

            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([
                ClientPackets1127.Target(0x1F0B),
                ClientPackets1127.PickUpRequest(),
                ClientPackets1127.Target(0x2C54),
                ClientPackets1127.InteractRequest(296516, 641415, 0x0101, 0x2C54)])));
            Assert.That(client.TargetId, Is.EqualTo(0x2C54));
            await Close(mind);
        }

        [Test]
        public async Task PickUpAndTalkTo_ShouldNotTargetAgain_WhenTheObjectIsTargeted()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.Dispatch(new TargetPrimitive(0x1F0B), mind.Link);

            client.Dispatch(new TalkToPrimitive(0x1F0B), mind.Link);
            client.Dispatch(new PickUpPrimitive(0x1F0B), mind.Link);

            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([
                ClientPackets1127.Target(0x1F0B),
                ClientPackets1127.InteractRequest(296516, 641415, 0x0101, 0x1F0B),
                ClientPackets1127.PickUpRequest()])));
            await Close(mind);
        }

        // Specializations first, then abilities and styles, as the server lists them. A Blademaster's two Slams are in Blunt and Shields.
        private static SkillsPercept BlademasterSkills()
        {
            return new([
                new("Blades", 5, SkillPage.Specialization),
                new("Blunt", 8, SkillPage.Specialization),
                new("Shields", 8, SkillPage.Specialization),
                new("Sprint", 1, SkillPage.Abilities),
                new("Shining Blade", 2, SkillPage.Styles, "Blades"),
                new("Slam", 8, SkillPage.Styles, "Blunt"),
                new("Slam", 5, SkillPage.Styles, "Shields")]);
        }

        // The index counts from Sprint, the first entry after the specializations, as the skill handler reads it.
        [Test]
        public async Task UseStyle_ShouldQueueTheStylesIndexInTheSkillListLastSent_TheFirstMatchUnlessALineIsGiven()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.OnPerceived(BlademasterSkills());

            Assert.That(client.Dispatch(new UseStylePrimitive("Shining Blade"), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new UseStylePrimitive("Slam"), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new UseStylePrimitive("Slam", "Shields"), mind.Link), Is.Null);

            Vector3 at = new(296516, 641415, 4848);
            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([
                ClientPackets1127.UseSkill(at, 0, 0, 0, 1),
                ClientPackets1127.UseSkill(at, 0, 0, 0, 2),
                ClientPackets1127.UseSkill(at, 0, 0, 0, 3)])));
            await Close(mind);
        }

        // The server reads whether the target is in view from the skill packet too, and the Harness claims it always is, as in its position updates.
        [Test]
        public async Task UseStyle_ShouldClaimTheTargetIsInView_WhileSomethingIsTargeted()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.OnPerceived(BlademasterSkills());
            client.Dispatch(new TargetPrimitive(0x1F0B), mind.Link);

            client.Dispatch(new UseStylePrimitive("Shining Blade"), mind.Link);

            Assert.That(Wire(Inbound(client)[1..]), Is.EqualTo(Wire([ClientPackets1127.UseSkill(new(296516, 641415, 4848), 0, 0, 0xA000, 1)])));
            await Close(mind);
        }

        [Test]
        public async Task UseStyle_ShouldFollowTheSkillList_WhenTheServerSendsANewOne()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.OnPerceived(BlademasterSkills());
            client.OnPerceived(new SkillsPercept([
                new("Blades", 6, SkillPage.Specialization),
                new("Sprint", 1, SkillPage.Abilities),
                new("Shining Blade", 2, SkillPage.Styles, "Blades"),
                new("Taunting Blade", 6, SkillPage.Styles, "Blades")]));

            client.Dispatch(new UseStylePrimitive("Taunting Blade"), mind.Link);

            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([ClientPackets1127.UseSkill(new(296516, 641415, 4848), 0, 0, 0, 2)])));
            await Close(mind);
        }

        private static IEnumerable<TestCaseData> StylesTheCharacterDoesntHave()
        {
            yield return new TestCaseData(new UseStylePrimitive("Taunting Blade"), "Greeen has no Style \"Taunting Blade\".");
            yield return new TestCaseData(new UseStylePrimitive("Slam", "Blades"), "Greeen has no Style \"Slam\" in Blades.");
            // Only styles match, never a specialization or an ability of that name.
            yield return new TestCaseData(new UseStylePrimitive("Blades"), "Greeen has no Style \"Blades\".");
            yield return new TestCaseData(new UseStylePrimitive("Sprint"), "Greeen has no Style \"Sprint\".");
        }

        [TestCaseSource(nameof(StylesTheCharacterDoesntHave))]
        public async Task UseStyle_ShouldBeRefusedWithAReason_ForAStyleTheCharacterDoesntHave(Primitive primitive, string reason)
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.OnPerceived(BlademasterSkills());

            Assert.That(client.Dispatch(primitive, mind.Link), Is.EqualTo(reason));
            Assert.That(Inbound(client), Is.Empty);
            await Close(mind);
        }

        [Test]
        public async Task UseStyle_ShouldBeRefusedWithAReason_BeforeTheServerSentASkillList()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            Assert.That(client.Dispatch(new UseStylePrimitive("Shining Blade"), mind.Link), Is.EqualTo("Greeen has no Style \"Shining Blade\"."));
            await Close(mind);
        }

        private static IEnumerable<TestCaseData> PrimitivesThePacketsCantCarry()
        {
            // The paper doll lets the server choose the slot, and above 1000 the item goes to another object.
            yield return new TestCaseData(new MoveItemPrimitive(0, 10), "A move is between the Character's own slots, 1 to 99");
            yield return new TestCaseData(new MoveItemPrimitive(41, 100), "A move is between the Character's own slots, 1 to 99");
            yield return new TestCaseData(new MoveItemPrimitive(41, 1001), "A move is between the Character's own slots, 1 to 99");
            yield return new TestCaseData(new ExaminePrimitive(65536), "A slot is 0 to 65535");
            yield return new TestCaseData(new SellPrimitive(65536), "A slot is 0 to 65535");
            yield return new TestCaseData(new ExamineMerchantItemPrimitive(-1), "An index is 0 to 65535");
            yield return new TestCaseData(new BuyPrimitive(65536, 1), "An index is 0 to 65535");
            yield return new TestCaseData(new BuyPrimitive(31, 0), "A count is 1 to 255");
            yield return new TestCaseData(new BuyPrimitive(31, 256), "A count is 1 to 255");
            yield return new TestCaseData(new PickUpPrimitive(65536), "An object ID is 1 to 65535");
            yield return new TestCaseData(new TalkToPrimitive(0), "An object ID is 1 to 65535");
        }

        [TestCaseSource(nameof(PrimitivesThePacketsCantCarry))]
        public async Task ItemPrimitives_ShouldBeRefusedWithAReason_ForWhatThePacketsCantCarry(Primitive primitive, string reason)
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            Assert.That(client.Dispatch(primitive, mind.Link), Does.StartWith(reason));
            Assert.That(Inbound(client), Is.Empty);
            Assert.That(client.TargetId, Is.Zero);
            await Close(mind);
        }

        [Test]
        public async Task TakeOver_ShouldConnectTheClientToTheNewMind_AndTellTheOldOneItWasReplaced()
        {
            FakeMind oldMind = new();
            FakeMind newMind = new();
            HarnessClient client = new(oldMind.Link);

            string refusal = client.TakeOver(newMind.Link);
            await oldMind.Link.Completion;

            Assert.That(refusal, Is.Null);
            Assert.That(client.Mind, Is.SameAs(newMind.Link));
            Assert.That(oldMind.Sent, Is.EqualTo(new ServerMessage[] { new ReplacedMessage() }));
            Assert.That(oldMind.Closed, Is.True);
            // Not in the world yet, so `entered` comes once world entry completes, as for any Mind.
            Assert.That(newMind.Link.IsConnected, Is.True);
            newMind.Link.Close();
            await newMind.Link.Completion;
            Assert.That(newMind.Sent, Is.Empty);
        }

        [Test]
        public async Task TakeOver_ShouldHaveTheServerTellTheNewMindWhatWorldEntryTellsAClient_AndCreateEveryObjectInViewAnew()
        {
            FakeMind oldMind = new();
            FakeMind newMind = new();
            HarnessClient client = InWorld(oldMind.Link, 1);
            WorldDoubles.Inventory((TestPlayer) client.Player);
            RecordingPacketLib calls = RecordingPacketLib.Install(client);
            PlayerObjectCache cache = WorldDoubles.ObjectCache((TestPlayer) client.Player);
            cache.NpcUpdateCache[WorldDoubles.Npc("mudman")] = new(0, 100);

            string refusal = client.TakeOver(newMind.Link);
            await Close(newMind);

            Assert.That(refusal, Is.Null);
            Assert.That(newMind.Sent, Is.EqualTo(new ServerMessage[] { new EnteredMessage(TookOver: true) }));
            // As `WorldInitRequestHandler` tells a client entering the world, in its order, with the status and encumbrance it sends later.
            Assert.That(calls.Names, Is.EqualTo(new[]
            {
                nameof(IPacketLib.SendPlayerPositionAndObjectID),
                nameof(IPacketLib.SendUpdateMaxSpeed),
                nameof(IPacketLib.SendInventoryItemsUpdate),
                nameof(IPacketLib.SendInventoryItemsUpdate),
                nameof(IPacketLib.SendUpdatePlayerSkills),
                nameof(IPacketLib.SendUpdatePlayer),
                nameof(IPacketLib.SendUpdateMoney),
                nameof(IPacketLib.SendUpdatePoints),
                nameof(IPacketLib.SendStatusUpdate),
                nameof(IPacketLib.SendEncumbrance)
            }));
            // The server creates for the client every NPC, item and door it no longer remembers sending.
            Assert.That(cache.NpcUpdateCache, Is.Empty);
        }

        [Test]
        public async Task TakeOver_ShouldMakeTheClientAskAgainForEveryPlayerItSees()
        {
            FakeMind oldMind = new();
            FakeMind newMind = new();
            HarnessClient client = InWorld(oldMind.Link, 1);
            WorldDoubles.Inventory((TestPlayer) client.Player);
            RecordingPacketLib.Install(client);
            WorldDoubles.ObjectCache((TestPlayer) client.Player);
            client.OnPerceived(Position(objectId: 30, sessionId: 4));
            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([ClientPackets1127.CreatePlayerRequest(4)])));

            client.TakeOver(newMind.Link);
            client.OnPerceived(Position(objectId: 30, sessionId: 4));

            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([ClientPackets1127.CreatePlayerRequest(4), ClientPackets1127.CreatePlayerRequest(4)])));
            await Close(newMind);
        }

        [Test]
        public void TakeOver_ShouldBeRefusedWithAReason_OnceTheOldMindIsLost()
        {
            FakeMind oldMind = new();
            FakeMind newMind = new();
            HarnessClient client = new(oldMind.Link);
            oldMind.Link.OnLost();

            string refusal = client.TakeOver(newMind.Link);

            Assert.That(refusal, Does.Contain("linkdead"));
            Assert.That(client.Mind, Is.SameAs(oldMind.Link));
            newMind.Link.Close();
        }

        [Test]
        public async Task Dispatch_ShouldIgnoreAReplacedMind()
        {
            FakeMind oldMind = new();
            FakeMind newMind = new();
            HarnessClient client = new(oldMind.Link);
            client.TakeOver(newMind.Link);

            string reason = client.Dispatch(new SayPrimitive("hello"), oldMind.Link);

            Assert.That(reason, Does.Contain("Another Mind has taken over"));
            newMind.Link.Close();
            await newMind.Link.Completion;
        }

        [Test]
        public async Task Out_ShouldReleaseThePooledPacketsItIsHanded()
        {
            FakeMind mind = new();
            using AgentTrace trace = new("Greeen", () => TextWriter.Null);
            IPacketLib packetLib = HarnessPacketLib.Create(new HarnessClient(mind.Link), trace);
            GSUDPPacketOut udp = new GSUDPPacketOut().Init((byte) eServerPackets.PlayerPosition);
            GSTCPPacketOut tcp = new GSTCPPacketOut().Init((byte) eServerPackets.Message);
            udp.IssuedTimestamp = 1;
            tcp.IssuedTimestamp = 1;

            packetLib.SendUDP(udp);
            packetLib.SendTCP(tcp);

            Assert.That(udp.IssuedTimestamp, Is.Zero);
            Assert.That(tcp.IssuedTimestamp, Is.Zero);
            await Close(mind);
        }

        // Every Harness client has a Mind. This one's link sends nowhere and is closed afterwards.
        private static string Dispatch(Primitive primitive)
        {
            FakeMind mind = new();

            try
            {
                return new HarnessClient(mind.Link).Dispatch(primitive, mind.Link);
            }
            finally
            {
                mind.Link.Close();
            }
        }

        // Greeen standing at the captured spot in zone 202, playing with a full health bar and a torch, as in the captures.
        private static HarnessClient InWorld(MindLink mind, ushort sessionId)
        {
            HarnessClient client = new(mind) { ClientState = GameClient.eClientState.Playing };
            WorldDoubles.SetSessionId(client, sessionId);
            TestPlayer greeen = WorldDoubles.Player(client, "Greeen");
            greeen.ObjectState = GameObject.eObjectState.Active;
            greeen.TestHealthPercent = 100;
            greeen.ActionFlags = PlayerPositionUpdateHandler.ActionFlags.TORCH;
            greeen.Region = WorldDoubles.Region(200, (202, 294912, 630784, 65536, 65536));
            (greeen.X, greeen.Y, greeen.Z) = (296516, 641415, 4848);
            return client;
        }

        private static PlayerPositionPercept Position(int objectId, int sessionId)
        {
            return new(296715, 642621, 4848, 191, 0, sessionId, objectId, 202, 0, 0, 1158, 1, 0, false, 100, false, 100, 100);
        }

        // The packets queued for the client's next tick, in order.
        private static ClientPacket[] Inbound(HarnessClient client)
        {
            return [.. (ConcurrentQueue<ClientPacket>) typeof(HarnessClient).GetField("_inbound", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client)];
        }

        // Packets as bytes, which compare by value.
        private static byte[][] Wire(ClientPacket[] packets)
        {
            return [.. packets.Select(static packet => packet.ToWire(1, 0x0101))];
        }

        // Once closed, the link has sent everything it was given.
        private static async Task Close(FakeMind mind)
        {
            mind.Link.Close();
            await mind.Link.Completion;
        }

        // Stands in for a Mind's WebSocket and remembers what was sent.
        // A packet library that records which of its calls the server made, in order, and sends nothing.
        public class RecordingPacketLib : DispatchProxy
        {
            private readonly List<string> _names = [];

            public IReadOnlyList<string> Names => _names;

            public static RecordingPacketLib Install(HarnessClient client)
            {
                IPacketLib proxy = Create<IPacketLib, RecordingPacketLib>();
                client.Out = proxy;
                return (RecordingPacketLib) proxy;
            }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                _names.Add(targetMethod.Name);
                Type type = targetMethod.ReturnType;
                return type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null;
            }
        }

        private sealed class FakeMind
        {
            private readonly List<ServerMessage> _sent = [];

            public FakeMind()
            {
                Link = new(Send, Close);
            }

            public MindLink Link { get; }
            public bool Closed { get; private set; }

            public IReadOnlyList<ServerMessage> Sent
            {
                get
                {
                    lock (_sent)
                    {
                        return [.. _sent];
                    }
                }
            }

            private Task Send(ServerMessage message)
            {
                lock (_sent)
                {
                    _sent.Add(message);
                }

                return Task.CompletedTask;
            }

            private Task Close()
            {
                Closed = true;
                return Task.CompletedTask;
            }
        }
    }
}
