using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using DOL.Database;
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

        // `/assist` has the server tell the client to select what the assisted Character has targeted. The client selects it, as a click would, which sets the server's target.
        [Test]
        public async Task ChangeTarget_ShouldSelectTheNewTarget_AsAClickDoes()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            client.OnPerceived(new TargetChangedPercept(0x1F0B));

            Assert.That(client.TargetId, Is.EqualTo(0x1F0B));
            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([ClientPackets1127.Target(0x1F0B)])));
            await Close(mind);
        }

        [Test]
        public async Task ChangeTarget_ShouldSendNothing_ForWhatIsSelectedAlready()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.Dispatch(new TargetPrimitive(0x1F0B), mind.Link);
            int queued = Inbound(client).Length;

            client.OnPerceived(new TargetChangedPercept(0x1F0B));

            Assert.That(Inbound(client), Has.Length.EqualTo(queued));
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
                new SpecializationEntry("Blades", 5),
                new SpecializationEntry("Blunt", 8),
                new SpecializationEntry("Shields", 8),
                new AbilityEntry("Sprint", 1),
                new StyleEntry("Shining Blade", 2, "Blades", null),
                new StyleEntry("Slam", 8, "Blunt", null),
                new StyleEntry("Slam", 5, "Shields", null)]);
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
                new SpecializationEntry("Blades", 6),
                new AbilityEntry("Sprint", 1),
                new StyleEntry("Shining Blade", 2, "Blades", null),
                new StyleEntry("Taunting Blade", 6, "Blades", null)]));

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

        // Specializations first, then abilities and spells, as the server lists a Naturalist's. The style is there only to be told apart.
        private static SkillsPercept NaturalistSkills()
        {
            return new([
                new SpecializationEntry("Regrowth", 2),
                new SpecializationEntry("Nurture", 2),
                new AbilityEntry("Staves", 1),
                new SpellEntry("Minor Revigoration", 1, null),
                new SpellEntry("Minor Bark Skin", 2, null),
                new StyleEntry("Shining Blade", 2, "Blades", null)]);
        }

        // The index counts from Staves, the first entry after the specializations, as for a style: a hybrid's spells are in the same skill list.
        [Test]
        public async Task CastSpell_ShouldQueueTheSpellsIndexInTheSkillListLastSent()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.OnPerceived(NaturalistSkills());
            client.Dispatch(new TargetPrimitive(0x1F0B), mind.Link);

            Assert.That(client.Dispatch(new CastSpellPrimitive("Minor Revigoration"), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new CastSpellPrimitive("Minor Bark Skin"), mind.Link), Is.Null);

            Vector3 at = new(296516, 641415, 4848);
            Assert.That(Wire(Inbound(client)[1..]), Is.EqualTo(Wire([
                ClientPackets1127.UseSkill(at, 0, 0, 0xA000, 1),
                ClientPackets1127.UseSkill(at, 0, 0, 0xA000, 2)])));
            await Close(mind);
        }

        // Specializations first, then abilities, as the server lists an Eldritch's. A realm ability is used the same way, and the spell and style are there only to be told apart.
        private static SkillsPercept EldritchSkills()
        {
            return new([
                new SpecializationEntry("Light", 5),
                new SpecializationEntry("Void", 5),
                new AbilityEntry("Staves", 1),
                new AbilityEntry("Sprint", 1),
                new AbilityEntry("Quickcast", 1),
                new RealmAbilityEntry("Purge", 1),
                new SpellEntry("Minor Revigoration", 1, null),
                new StyleEntry("Shining Blade", 2, "Blades", null)]);
        }

        // The index counts from Staves, the first entry after the specializations, as in `quick-cast.tsv`.
        [Test]
        public async Task UseAbility_ShouldQueueTheSkillPacketWithTheAbilitysIndexInTheSkillListLastSent()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.OnPerceived(EldritchSkills());

            Assert.That(client.Dispatch(new UseAbilityPrimitive("Sprint"), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new UseAbilityPrimitive("Quickcast"), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new UseAbilityPrimitive("Purge"), mind.Link), Is.Null);

            Vector3 at = new(296516, 641415, 4848);
            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([
                ClientPackets1127.UseSkill(at, 0, 0, 0, 1),
                ClientPackets1127.UseSkill(at, 0, 0, 0, 2),
                ClientPackets1127.UseSkill(at, 0, 0, 0, 3)])));
            await Close(mind);
        }

        private static IEnumerable<TestCaseData> AbilitiesTheCharacterDoesntHave()
        {
            yield return new TestCaseData(new UseAbilityPrimitive("Mastery of Concentration"), "Greeen has no Ability \"Mastery of Concentration\".");
            // Only abilities and realm abilities match, never a specialization, a spell or a style of that name.
            yield return new TestCaseData(new UseAbilityPrimitive("Void"), "Greeen has no Ability \"Void\".");
            yield return new TestCaseData(new UseAbilityPrimitive("Minor Revigoration"), "Greeen has no Ability \"Minor Revigoration\".");
            yield return new TestCaseData(new UseAbilityPrimitive("Shining Blade"), "Greeen has no Ability \"Shining Blade\".");
            yield return new TestCaseData(new CastSpellPrimitive("Quickcast"), "Greeen has no Spell \"Quickcast\".");
            yield return new TestCaseData(new UseStylePrimitive("Sprint"), "Greeen has no Style \"Sprint\".");
        }

        [TestCaseSource(nameof(AbilitiesTheCharacterDoesntHave))]
        public async Task UseAbility_ShouldBeRefusedWithAReason_ForAnAbilityTheCharacterDoesntHave(Primitive primitive, string reason)
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.OnPerceived(EldritchSkills());

            Assert.That(client.Dispatch(primitive, mind.Link), Is.EqualTo(reason));
            Assert.That(Inbound(client), Is.Empty);
            await Close(mind);
        }

        [Test]
        public async Task UseAbility_ShouldBeRefusedWithAReason_BeforeTheServerSentASkillList()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            Assert.That(client.Dispatch(new UseAbilityPrimitive("Sprint"), mind.Link), Is.EqualTo("Greeen has no Ability \"Sprint\"."));
            await Close(mind);
        }

        // One click per order, on its row of the pet window, as in `pet-window.tsv`. Whether there's a pet, the server decides.
        [Test]
        public async Task CommandPet_ShouldQueueThePetWindowPacketWithTheOrdersByteOnItsRow_WithOrWithoutAPet()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            foreach (PetOrder order in Enum.GetValues<PetOrder>())
                Assert.That(client.Dispatch(new CommandPetPrimitive(order), mind.Link), Is.Null);

            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([
                ClientPackets1127.PetWindow(1, 0, 0),
                ClientPackets1127.PetWindow(2, 0, 0),
                ClientPackets1127.PetWindow(3, 0, 0),
                ClientPackets1127.PetWindow(0, 1, 0),
                ClientPackets1127.PetWindow(0, 2, 0),
                ClientPackets1127.PetWindow(0, 3, 0),
                ClientPackets1127.PetWindow(0, 4, 0),
                ClientPackets1127.PetWindow(0, 0, 1),
                ClientPackets1127.PetWindow(0, 0, 2)])));
            await Close(mind);
        }

        // JSON may carry an enum as a number.
        [Test]
        public async Task CommandPet_ShouldBeRefusedWithAReason_ForAnOrderThatIsntOne()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            Assert.That(client.Dispatch(new CommandPetPrimitive((PetOrder) 42), mind.Link), Is.EqualTo("There's no pet order 42."));
            Assert.That(Inbound(client), Is.Empty);
            await Close(mind);
        }

        // A Magician's first spells, in its spell lines: Gleam Ray from Way of the Sun, Lesser Powerward from Way of the Moon, and Gleam Streak, which Way of the Sun gains at level 2.
        private static readonly SpellLine WayOfTheSun = new("Light", "Way of the Sun", "Light", true);
        private static readonly SpellLine WayOfTheMoon = new("Mana", "Way of the Moon", "Mana", true);
        private static readonly Spell GleamRay = new(new DbSpell { Name = "Gleam Ray", SpellID = 4101, Target = "Enemy", Type = "DirectDamage" }, 1);
        private static readonly Spell GleamStreak = new(new DbSpell { Name = "Gleam Streak", SpellID = 4102, Target = "Enemy", Type = "DirectDamage" }, 2);
        private static readonly Spell LesserPowerward = new(new DbSpell { Name = "Lesser Powerward", SpellID = 4301, Target = "Self", Type = "BaseArmorFactorBuff" }, 1);

        // What the client hears when the server sends the Character's skills, its spell lines among them.
        private static Percept SkillsSent(HarnessClient client)
        {
            return PerceptProjection.Project(client, typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUpdatePlayerSkills)), [false]);
        }

        // A list caster's spells aren't in its skill list. The use spell packet names one by its level, in the line at its index among the lines the server built, as in `cast-damage.tsv`.
        [Test]
        public async Task CastSpell_ShouldQueueTheUseSpellPacketWithTheSpellsLevelAndLine_ForAListCastersSpell()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            ((TestPlayer) client.Player).ListSpells = [(WayOfTheSun, [GleamRay, GleamStreak]), (WayOfTheMoon, [LesserPowerward])];
            client.OnPerceived(SkillsSent(client));
            client.Dispatch(new TargetPrimitive(0x1F0B), mind.Link);

            Assert.That(client.Dispatch(new CastSpellPrimitive("Gleam Streak"), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new CastSpellPrimitive("Lesser Powerward"), mind.Link), Is.Null);

            Vector3 at = new(296516, 641415, 4848);
            Assert.That(Wire(Inbound(client)[1..]), Is.EqualTo(Wire([
                ClientPackets1127.UseSpell(at, 0, 0, 0xA000, 2, 0),
                ClientPackets1127.UseSpell(at, 0, 0, 0xA000, 1, 1)])));
            Assert.That(ResolvedByUseSpellHandler(client.Player, 2, 0), Is.SameAs(GleamStreak));
            Assert.That(ResolvedByUseSpellHandler(client.Player, 1, 1), Is.SameAs(LesserPowerward));
            await Close(mind);
        }

        // The spell the server casts for a use spell packet of `level` and `lineIndex`, as `UseSpellHandler` looks it up.
        private static Skill ResolvedByUseSpellHandler(GamePlayer player, int level, int lineIndex)
        {
            object[] args = [player, lineIndex, level, null, null];
            typeof(UseSpellHandler).GetMethod("GetSkill", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
            return (Skill) args[3];
        }

        // As for a hybrid: a name in none of the lines, or a style's, and every spell before the client has heard of its lines.
        [Test]
        public async Task CastSpell_ShouldBeRefusedWithAReason_ForAListCastersSpellItDoesntHave_OrBeforeTheServerSentItsLines()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            ((TestPlayer) client.Player).ListSpells = [(WayOfTheSun, [GleamRay]), (WayOfTheMoon, [LesserPowerward])];

            Assert.That(client.Dispatch(new CastSpellPrimitive("Gleam Ray"), mind.Link), Is.EqualTo("Greeen has no Spell \"Gleam Ray\"."));

            client.OnPerceived(SkillsSent(client));
            Assert.That(client.Dispatch(new CastSpellPrimitive("Gleam Streak"), mind.Link), Is.EqualTo("Greeen has no Spell \"Gleam Streak\"."));
            Assert.That(client.Dispatch(new CastSpellPrimitive("Way of the Sun"), mind.Link), Is.EqualTo("Greeen has no Spell \"Way of the Sun\"."));
            Assert.That(client.Dispatch(new UseStylePrimitive("Gleam Ray"), mind.Link), Is.EqualTo("Greeen has no Style \"Gleam Ray\"."));

            Assert.That(Inbound(client), Is.Empty);
            await Close(mind);
        }

        // The server reads the line's index against the lines it built last, which it builds again when it sends the skills: a spell gained on levelling is cast once the client has heard of it.
        [Test]
        public async Task CastSpell_ShouldFindAListCastersNewSpell_OnceTheServerSentTheSkillsAgain()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            TestPlayer greeen = (TestPlayer) client.Player;
            greeen.ListSpells = [(WayOfTheSun, [GleamRay])];
            client.OnPerceived(SkillsSent(client));
            Assert.That(client.Dispatch(new CastSpellPrimitive("Gleam Ray"), mind.Link), Is.Null);

            greeen.ListSpells = [(WayOfTheSun, [GleamRay, GleamStreak])];
            Assert.That(client.Dispatch(new CastSpellPrimitive("Gleam Streak"), mind.Link), Is.EqualTo("Greeen has no Spell \"Gleam Streak\"."));

            client.OnPerceived(SkillsSent(client));
            Assert.That(client.Dispatch(new CastSpellPrimitive("Gleam Streak"), mind.Link), Is.Null);

            Vector3 at = new(296516, 641415, 4848);
            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([ClientPackets1127.UseSpell(at, 0, 0, 0, 1, 0), ClientPackets1127.UseSpell(at, 0, 0, 0, 2, 0)])));
            await Close(mind);
        }

        private static IEnumerable<TestCaseData> SpellsTheCharacterDoesntHave()
        {
            yield return new TestCaseData(new CastSpellPrimitive("Renewal"), "Greeen has no Spell \"Renewal\".");
            // Only spells match, never a specialization, an ability or a style of that name.
            yield return new TestCaseData(new CastSpellPrimitive("Regrowth"), "Greeen has no Spell \"Regrowth\".");
            yield return new TestCaseData(new CastSpellPrimitive("Staves"), "Greeen has no Spell \"Staves\".");
            yield return new TestCaseData(new CastSpellPrimitive("Shining Blade"), "Greeen has no Spell \"Shining Blade\".");
            yield return new TestCaseData(new UseStylePrimitive("Minor Revigoration"), "Greeen has no Style \"Minor Revigoration\".");
        }

        [TestCaseSource(nameof(SpellsTheCharacterDoesntHave))]
        public async Task CastSpell_ShouldBeRefusedWithAReason_ForASpellTheCharacterDoesntHave(Primitive primitive, string reason)
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.OnPerceived(NaturalistSkills());

            Assert.That(client.Dispatch(primitive, mind.Link), Is.EqualTo(reason));
            Assert.That(Inbound(client), Is.Empty);
            await Close(mind);
        }

        // As the server cuts the names: Minor Bark Skin on Greeen itself and on Vue, and Strength of the Oak on Greeen.
        private static ConcentrationPercept NaturalistConcentration()
        {
            return new([new("Minor Bark S..", "Greeen", 1), new("Minor Bark S..", "Vue", 1), new("Strength of ..", "Greeen", 1)]);
        }

        // The index is the buff's place in the concentration window. Names are matched as the window cuts them.
        [Test]
        public async Task CancelConcentration_ShouldQueueTheBuffsIndexInTheListLastSent_TheFirstMatchUnlessATargetIsGiven()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.OnPerceived(NaturalistConcentration());

            Assert.That(client.Dispatch(new CancelConcentrationPrimitive("Minor Bark Skin"), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new CancelConcentrationPrimitive("Minor Bark Skin", "Vue"), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new CancelConcentrationPrimitive("Strength of the Oak"), mind.Link), Is.Null);

            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([
                ClientPackets1127.RemoveConcentrationEffect(0),
                ClientPackets1127.RemoveConcentrationEffect(1),
                ClientPackets1127.RemoveConcentrationEffect(2)])));
            await Close(mind);
        }

        private static IEnumerable<TestCaseData> BuffsTheCharacterDoesntKeepUp()
        {
            yield return new TestCaseData(new CancelConcentrationPrimitive("Oak's Stoutness"), "Greeen keeps up no \"Oak's Stoutness\".");
            yield return new TestCaseData(new CancelConcentrationPrimitive("Strength of the Oak", "Vue"), "Greeen keeps up no \"Strength of the Oak\" on Vue.");
        }

        [TestCaseSource(nameof(BuffsTheCharacterDoesntKeepUp))]
        public async Task CancelConcentration_ShouldBeRefusedWithAReason_ForABuffTheCharacterDoesntKeepUp(Primitive primitive, string reason)
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.OnPerceived(NaturalistConcentration());

            Assert.That(client.Dispatch(primitive, mind.Link), Is.EqualTo(reason));
            Assert.That(Inbound(client), Is.Empty);
            await Close(mind);
        }

        [Test]
        public async Task CancelConcentration_ShouldFollowTheList_WhenTheServerSendsANewOne()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.OnPerceived(NaturalistConcentration());
            client.OnPerceived(new ConcentrationPercept([new("Strength of ..", "Greeen", 1)]));

            Assert.That(client.Dispatch(new CancelConcentrationPrimitive("Strength of the Oak"), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new CancelConcentrationPrimitive("Minor Bark Skin"), mind.Link), Is.EqualTo("Greeen keeps up no \"Minor Bark Skin\"."));

            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([ClientPackets1127.RemoveConcentrationEffect(0)])));
            await Close(mind);
        }

        [Test]
        public async Task CastSpell_ShouldBeRefusedWithAReason_BeforeTheServerSentASkillList()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            Assert.That(client.Dispatch(new CastSpellPrimitive("Minor Revigoration"), mind.Link), Is.EqualTo("Greeen has no Spell \"Minor Revigoration\"."));
            await Close(mind);
        }

        // A right-click in the backpack sends type 1, and the server reads type 2 as the item's second spell.
        [Test]
        public async Task UseItem_ShouldQueueTheUseSlotPacket_ItsSecondSpellWithSecond()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            Assert.That(client.Dispatch(new UseItemPrimitive(40), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new UseItemPrimitive(26, true), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new UseItemPrimitive(26, false), mind.Link), Is.Null);

            Vector3 at = new(296516, 641415, 4848);
            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([
                ClientPackets1127.UseSlot(at, 0, 0, 0, 40, eUseType.Use1),
                ClientPackets1127.UseSlot(at, 0, 0, 0, 26, eUseType.Use2),
                ClientPackets1127.UseSlot(at, 0, 0, 0, 26, eUseType.Use1)])));
            await Close(mind);
        }

        // The server reads whether the target is in view from the use slot packet too, as from the skill packet.
        [Test]
        public async Task UseItem_ShouldClaimTheTargetIsInView_WhileSomethingIsTargeted()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            client.Dispatch(new TargetPrimitive(0x1F0B), mind.Link);

            client.Dispatch(new UseItemPrimitive(40), mind.Link);

            Assert.That(Wire(Inbound(client)[1..]), Is.EqualTo(Wire([ClientPackets1127.UseSlot(new(296516, 641415, 4848), 0, 0, 0xA000, 40, eUseType.Use1)])));
            await Close(mind);
        }

        private static IEnumerable<TestCaseData> PrimitivesThePacketsCantCarry()
        {
            // The use slot packet's slot is a single byte.
            yield return new TestCaseData(new UseItemPrimitive(256), "A slot to use is 0 to 255");
            yield return new TestCaseData(new UseItemPrimitive(-1, true), "A slot to use is 0 to 255");
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
            yield return new TestCaseData(new AnswerDialogPrimitive(eDialogCode.CustomDialog, 65536, 1, 0, true), "A Dialog's data is 0 to 65535");
            yield return new TestCaseData(new AnswerDialogPrimitive(eDialogCode.CustomDialog, 1, 1, -1, true), "A Dialog's data is 0 to 65535");
        }

        [Test]
        public async Task AnswerDialog_ShouldQueueTheDialogResponseTheCaptureShows()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);

            Assert.That(client.Dispatch(new AnswerDialogPrimitive(eDialogCode.CustomDialog, 0x0101, 0x01, 0x00, true), mind.Link), Is.Null);
            Assert.That(client.Dispatch(new AnswerDialogPrimitive(eDialogCode.GroupInvite, 0x0304, 0x00, 0x00, false), mind.Link), Is.Null);

            Assert.That(Wire(Inbound(client)), Is.EqualTo(Wire([
                ClientPackets1127.DialogResponse((byte) eDialogCode.CustomDialog, 0x0101, 0x01, 0x00, true),
                ClientPackets1127.DialogResponse((byte) eDialogCode.GroupInvite, 0x0304, 0x00, 0x00, false)])));
            await Close(mind);
        }

        // The real packet library hands the response handler the server's callback through the player, and declines a custom Dialog still open first.
        [Test]
        public async Task Out_ShouldHandTheServersCallbackToThePlayer_DecliningTheCustomDialogStillOpen()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            using AgentTrace trace = new("Greeen", () => TextWriter.Null);
            IPacketLib packetLib = HarnessPacketLib.Create(client, trace);
            List<byte> firstAnswers = [];
            CustomDialogResponse first = (_, response) => firstAnswers.Add(response);
            CustomDialogResponse second = static (_, _) => { };

            packetLib.SendCustomDialog("Do you accept?", first);
            Assert.That(client.Player.CustomDialogCallback, Is.SameAs(first));
            packetLib.SendCustomDialog("And now?", second);

            Assert.That(firstAnswers, Is.EqualTo(new byte[] { 0x00 }));
            Assert.That(client.Player.CustomDialogCallback, Is.SameAs(second));
            await Close(mind);
        }

        // The real packet library leaves the inviter's name on the player, which the response handler checks before it acts on an answer.
        [Test]
        public async Task Out_ShouldLeaveTheInvitersNameForTheResponseHandler_ForAGroupOrGuildInvite()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            using AgentTrace trace = new("Greeen", () => TextWriter.Null);
            IPacketLib packetLib = HarnessPacketLib.Create(client, trace);
            TestPlayer dalta = WorldDoubles.Player(WorldDoubles.Client(0x0304), "Dalta");

            packetLib.SendGroupInviteCommand(dalta, "Dalta has invited you to join a group.");
            packetLib.SendGuildInviteCommand(dalta, "Dalta has invited you into Greenwood.");

            Assert.That(client.Player.TempProperties.GetProperty<string>(DialogResponseHandler.GROUP_INVITE_KEY), Is.EqualTo("Dalta"));
            Assert.That(client.Player.TempProperties.GetProperty<string>(DialogResponseHandler.GUILD_INVITE_KEY), Is.EqualTo("Dalta"));
            await Close(mind);
        }

        [Test]
        public async Task TakeOver_ShouldTellTheNewMindTheNewestDialogStillOpen_AfterWhatWorldEntryTells()
        {
            FakeMind oldMind = new();
            FakeMind newMind = new();
            HarnessClient client = InWorld(oldMind.Link, 1);
            WorldDoubles.Inventory((TestPlayer) client.Player);
            RecordingPacketLib.Install(client);
            WorldDoubles.ObjectCache((TestPlayer) client.Player);
            client.OnPerceived(new DialogPercept(eDialogCode.CustomDialog, 1, 1, 0, true, "Do you accept?"));
            client.OnPerceived(new DialogPercept(eDialogCode.CustomDialog, 1, 1, 0, true, "And now?"));

            client.TakeOver(newMind.Link);
            await Close(newMind);

            Assert.That(newMind.Sent.OfType<PerceptMessage>().Select(static message => message.Percept), Is.EqualTo(new Percept[]
            {
                new DialogPercept(eDialogCode.CustomDialog, 1, 1, 0, true, "And now?")
            }));
        }

        // The pet window the client still shows, which `WorldInitRequestHandler` doesn't send: a Character enters the world without a pet.
        [Test]
        public async Task TakeOver_ShouldHaveTheServerOpenThePetWindowAgain_AfterWhatWorldEntryTells_WhileTheCharacterHasAPet()
        {
            FakeMind oldMind = new();
            FakeMind newMind = new();
            HarnessClient client = InWorld(oldMind.Link, 1);
            WorldDoubles.Inventory((TestPlayer) client.Player);
            RecordingPacketLib calls = RecordingPacketLib.Install(client);
            WorldDoubles.ObjectCache((TestPlayer) client.Player);
            WorldDoubles.Pet((TestPlayer) client.Player, WorldDoubles.Npc("wolf cub"));

            client.TakeOver(newMind.Link);
            await Close(newMind);

            Assert.That(calls.Names.TakeLast(2), Is.EqualTo(new[] { nameof(IPacketLib.SendEncumbrance), nameof(IPacketLib.SendPetWindow) }));
        }

        // What `PlayerInitRequestHandler` sends a Character entering the world in a Group: the group window and how every member is doing.
        [Test]
        public async Task TakeOver_ShouldHaveTheServerSendTheGroupWindowAndEveryMembersStatus_AfterWhatWorldEntryTells_WhileTheCharacterIsInAGroup()
        {
            FakeMind oldMind = new();
            FakeMind newMind = new();
            HarnessClient client = InWorld(oldMind.Link, 1);
            WorldDoubles.Inventory((TestPlayer) client.Player);
            RecordingPacketLib calls = RecordingPacketLib.Install(client);
            WorldDoubles.ObjectCache((TestPlayer) client.Player);
            WorldDoubles.Group(client.Player, WorldDoubles.Player(WorldDoubles.Client(0x0304), "Vue"));

            client.TakeOver(newMind.Link);
            await Close(newMind);

            Assert.That(calls.Names.TakeLast(3), Is.EqualTo(new[] { nameof(IPacketLib.SendEncumbrance), nameof(IPacketLib.SendGroupWindowUpdate), nameof(IPacketLib.SendGroupMembersUpdate) }));
        }

        [Test]
        public async Task TakeOver_ShouldTellTheNewMindNoDialog_OnceTheOpenOneWasAnswered()
        {
            FakeMind oldMind = new();
            FakeMind newMind = new();
            HarnessClient client = InWorld(oldMind.Link, 1);
            WorldDoubles.Inventory((TestPlayer) client.Player);
            RecordingPacketLib.Install(client);
            WorldDoubles.ObjectCache((TestPlayer) client.Player);
            client.OnPerceived(new DialogPercept(eDialogCode.CustomDialog, 1, 1, 0, true, "Do you accept?"));
            client.Dispatch(new AnswerDialogPrimitive(eDialogCode.CustomDialog, 1, 1, 0, true), oldMind.Link);

            client.TakeOver(newMind.Link);
            await Close(newMind);

            Assert.That(newMind.Sent.OfType<PerceptMessage>(), Is.Empty);
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
                nameof(IPacketLib.SendCharStatsUpdate),
                nameof(IPacketLib.SendUpdateIcons),
                nameof(IPacketLib.SendUpdatePoints),
                nameof(IPacketLib.SendConcentrationList),
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

        // The group updates hand their members over as a list: the runtime proxy throws on its first call of a method that takes a span (ADR 0001).
        [Test]
        public async Task Out_ShouldTellTheMindAGroupMembersPosition_ThroughTheRuntimeProxy()
        {
            FakeMind mind = new();
            HarnessClient client = InWorld(mind.Link, 0x0101);
            using AgentTrace trace = new("Greeen", () => TextWriter.Null);
            IPacketLib packetLib = HarnessPacketLib.Create(client, trace);
            TestPlayer vue = WorldDoubles.Player(WorldDoubles.Client(0x0304), "Vue");
            vue.ObjectID = 0x0456;
            vue.Region = client.Player.CurrentRegion;
            (vue.X, vue.Y) = (300000, 640000);

            packetLib.SendGroupMembersMapUpdate([vue]);
            await Close(mind);

            Assert.That(mind.Sent.OfType<PerceptMessage>().Select(static message => message.Percept).OfType<GroupPositionsPercept>().Single().Members,
                Is.EqualTo(new GroupMemberPosition[] { new(0x0456, 202, 300000, 640000) }));
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
