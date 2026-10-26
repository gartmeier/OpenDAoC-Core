using System;
using System.Buffers.Binary;
using System.Linq;
using System.Numerics;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// Golden-byte tests: every packet the Harness builds for world entry, idling and moving matches what a real 1.127 client sent in the captures under `Fixtures/Agents/v1127`.
    /// Sequence and session ID are taken from the captured packet. Wire byte 8 (the code's high byte) is leftover client memory, so it and the checksum aren't compared.
    /// Data offsets the README lists as varying are masked per packet.
    /// </summary>
    [TestFixture]
    public class UT_ClientPackets1127
    {
        private static readonly string[] Captures = ClientCaptures.Names;

        // Those with heading updates.
        private static readonly string[] HeadingUpdateCaptures = [.. ClientCaptures.Names, ClientCaptures.MOVEMENT];

        [TestCaseSource(nameof(Captures))]
        public void CharacterSelect_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.CharacterSelect(0x00), FromWorldEntry(capture, eClientPackets.CharacterSelectRequest, 0));
            AssertMatches(ClientPackets1127.CharacterSelect(0x14), FromWorldEntry(capture, eClientPackets.CharacterSelectRequest, 1));
        }

        [TestCaseSource(nameof(Captures))]
        public void CharacterOverview_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.CharacterOverview(eRealm.Hibernia), FromWorldEntry(capture, eClientPackets.CharacterOverviewRequest, 0));
        }

        [TestCaseSource(nameof(Captures))]
        public void RegionList_ShouldMatchCapture(string capture)
        {
            // Data offset 19 is unknown and varies.
            AssertMatches(ClientPackets1127.RegionList(ClientPackets1127.CharacterIndex(300)), FromWorldEntry(capture, eClientPackets.RegionListRequest, 0), 19);
        }

        [TestCaseSource(nameof(Captures))]
        public void Ping_ShouldMatchCapture(string capture)
        {
            foreach (int occurrence in new[] { 0, 1 })
            {
                CapturedPacket captured = FromWorldEntry(capture, eClientPackets.PingRequest, occurrence);
                uint timestamp = BinaryPrimitives.ReadUInt32BigEndian(captured.Data.AsSpan(4));

                // Data offsets 1-2 follow a cycle and 11 flags the first ping of world entry. The server reads neither.
                AssertMatches(ClientPackets1127.Ping(timestamp), captured, 1, 2, 11);
            }
        }

        [TestCaseSource(nameof(Captures))]
        public void IdlePing_ShouldMatchCapture(string capture)
        {
            CapturedPacket[] pings = ClientCaptures.WhilePlaying(capture, eClientPackets.PingRequest);
            Assert.That(pings, Is.Not.Empty);

            foreach (CapturedPacket captured in pings)
            {
                uint timestamp = BinaryPrimitives.ReadUInt32BigEndian(captured.Data.AsSpan(4));

                // Data offsets 1-2 follow a cycle the server doesn't read. Offset 11 is 0 once world entry is over.
                AssertMatches(ClientPackets1127.Ping(timestamp), captured, 1, 2);
            }
        }

        [TestCaseSource(nameof(Captures))]
        public void WorldInit_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.WorldInit(ClientPackets1127.CharacterIndex(300)), FromWorldEntry(capture, eClientPackets.WorldInitRequest, 0));
        }

        [TestCaseSource(nameof(Captures))]
        public void PlayerInit_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.PlayerInit(), FromWorldEntry(capture, eClientPackets.PlayerInitRequest, 0));
        }

        [TestCaseSource(nameof(Captures))]
        public void GameOpen_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.GameOpen(), FromWorldEntry(capture, eClientPackets.GameOpenRequest, 0));
        }

        [TestCaseSource(nameof(Captures))]
        public void PositionUpdate_ShouldMatchCapture(string capture)
        {
            // The Character's saved spot, standing still. The high byte of the heading carries flags.
            Vector3 position = new(296516, 641415, 4848);
            const ushort zoneId = 202;

            CapturedPacket beforeGameOpen = FromWorldEntry(capture, eClientPackets.PositionUpdate, 0);
            AssertMatches(ClientPackets1127.PositionUpdate(position, zoneId, 0x19DF, 0, 67, beforeGameOpen.SessionId, 0, 0), beforeGameOpen);

            CapturedPacket afterGameOpen = FromWorldEntry(capture, eClientPackets.PositionUpdate, 1);
            AssertMatches(ClientPackets1127.PositionUpdate(position, zoneId, 0x09DF, 0, 100, afterGameOpen.SessionId, 0, PlayerPositionUpdateHandler.ActionFlags.TORCH), afterGameOpen);
        }

        [Test]
        public void RunningPositionUpdate_ShouldMatchCapture()
        {
            // At full speed in the first run, heading 0x5F3 with the client's 0x1000 flag. The full packet is in the README.
            CapturedPacket captured = ClientCaptures.WhilePlaying(ClientCaptures.MOVEMENT, eClientPackets.PositionUpdate).Single(static p => p.Sequence == 0x0042);
            Vector3 position = new(296492.4375f, 642525.25f, 4848);

            AssertMatches(ClientPackets1127.PositionUpdate(position, 202, 0x15F3, 191, 100, captured.SessionId, 0, PlayerPositionUpdateHandler.ActionFlags.TORCH), captured);
        }

        [Test]
        public void EveryRunningPositionUpdate_ShouldMatchCapture()
        {
            // Every update with a speed, ramping up, at full speed, steering and stopping. Only the position, speed and heading change while running, so those are taken from the captured packet.
            CapturedPacket[] running = ClientCaptures.WhilePlaying(ClientCaptures.MOVEMENT, eClientPackets.PositionUpdate).Where(static p => ClientCaptures.Speed(p) > 0).ToArray();
            Assert.That(running, Has.Length.GreaterThan(20));

            foreach (CapturedPacket captured in running)
            {
                byte[] data = captured.Data;
                Vector3 position = new(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(0)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(8)));
                ushort heading = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(30));

                AssertMatches(ClientPackets1127.PositionUpdate(position, 202, heading, ClientCaptures.Speed(captured), 100, captured.SessionId, 0, PlayerPositionUpdateHandler.ActionFlags.TORCH), captured);
            }
        }

        // Every update while swimming across the river, in and out of the water included. As when running, only the position, speed and heading change, and the swimming flag is set.
        [Test]
        public void EverySwimmingPositionUpdate_ShouldMatchCapture()
        {
            CapturedPacket[] swimming = ClientCaptures.WhilePlaying(ClientCaptures.SWIM_STRAIGHT, eClientPackets.PositionUpdate).Where(static p => p.Data[26] == (byte) PlayerPositionUpdateHandler.StateFlags.SWIMMING).ToArray();
            Assert.That(swimming, Has.Length.GreaterThan(80));

            foreach (CapturedPacket captured in swimming)
            {
                byte[] data = captured.Data;
                Vector3 position = new(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(0)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(8)));
                ushort heading = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(30));

                AssertMatches(ClientPackets1127.PositionUpdate(position, 207, heading, ClientCaptures.Speed(captured), 100, captured.SessionId, PlayerPositionUpdateHandler.StateFlags.SWIMMING, PlayerPositionUpdateHandler.ActionFlags.TORCH), captured);
            }
        }

        // Once the Human targeted the NPC, every position update carries its object ID and the target-in-view flags.
        [Test]
        public void PositionUpdateWithATarget_ShouldMatchCapture()
        {
            CapturedPacket[] targeting = ClientCaptures.WhilePlaying(ClientCaptures.MOVEMENT, eClientPackets.PositionUpdate).Where(static p => BinaryPrimitives.ReadUInt16BigEndian(p.Data.AsSpan(22)) != 0).ToArray();
            Assert.That(targeting, Is.Not.Empty);

            foreach (CapturedPacket captured in targeting)
            {
                byte[] data = captured.Data;
                Vector3 position = new(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(0)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(8)));
                ushort heading = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(30));
                PlayerPositionUpdateHandler.ActionFlags flags = PlayerPositionUpdateHandler.ActionFlags.TORCH | PlayerPositionUpdateHandler.ActionFlags.TARGET_IN_VIEW;

                AssertMatches(ClientPackets1127.PositionUpdate(position, 202, heading, ClientCaptures.Speed(captured), 100, captured.SessionId, 0, flags, 0x1F0B), captured);
            }
        }

        [Test]
        public void Target_ShouldMatchCapture()
        {
            // The Human clicked the NPC, whose object ID was 0x1F0B. The full packet is in the README.
            CapturedPacket captured = ClientCaptures.WhilePlaying(ClientCaptures.MOVEMENT, eClientPackets.PlayerTarget).Single();

            AssertMatches(ClientPackets1127.Target(0x1F0B), captured);
        }

        // The Human pressed the attack key twice, then fought the Mob until the client stopped attacking by itself when it died.
        [Test]
        public void AttackRequest_ShouldMatchCapture()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.FIGHT, eClientPackets.PlayerAttackRequest);
            Assert.That(captured, Has.Length.EqualTo(4));

            AssertMatches(ClientPackets1127.AttackRequest(true), captured[0]);
            AssertMatches(ClientPackets1127.AttackRequest(false), captured[1]);
            AssertMatches(ClientPackets1127.AttackRequest(true), captured[2]);
            // Data offsets 2-3 hold leftover client memory when the client stops by itself. The server reads neither.
            AssertMatches(ClientPackets1127.AttackRequest(false, byClient: true), captured[3], 2, 3);
        }

        // Clicked twice on the same Mob, cleared once with Esc, and cleared by the client when the Mob it was attacking died.
        [Test]
        public void TargetAndClearTarget_ShouldMatchCapture()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.FIGHT, eClientPackets.PlayerTarget);
            Assert.That(captured, Has.Length.EqualTo(4));

            AssertMatches(ClientPackets1127.Target(0x2C54), captured[0]);
            AssertMatches(ClientPackets1127.ClearTarget(attacking: false), captured[1]);
            AssertMatches(ClientPackets1127.Target(0x2C54), captured[2]);
            AssertMatches(ClientPackets1127.ClearTarget(attacking: true), captured[3]);
        }

        // The Human typed `/sit` twice, then `/stand`. A 1.127 client sends them as commands, not as the sit request.
        [Test]
        public void SitAndStand_ShouldMatchCapture()
        {
            CapturedPacket[] commands = ClientCaptures.WhilePlaying(ClientCaptures.FIGHT, eClientPackets.CommandHandler);

            AssertMatches(ClientPackets1127.Sit(), commands.First(static p => p.Data.SequenceEqual("\0&sit\0"u8.ToArray())));
            AssertMatches(ClientPackets1127.Stand(), commands.Single(static p => p.Data.SequenceEqual("\0&stand\0"u8.ToArray())));
        }

        // While something is targeted, heading updates carry it at data offsets 2-3. While attacking, the health byte has its top bit set, as in position updates.
        [Test]
        public void EveryHeadingUpdateWithATarget_ShouldMatchCapture()
        {
            CapturedPacket[] headingUpdates = ClientCaptures.WhilePlaying(ClientCaptures.FIGHT, eClientPackets.PlayerHeadingUpdate);
            Assert.That(headingUpdates, Is.Not.Empty);

            foreach (CapturedPacket captured in headingUpdates)
            {
                byte[] data = captured.Data;
                ushort targetId = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(2));
                ushort heading = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
                Assert.That(targetId, Is.Not.Zero);

                AssertMatches(ClientPackets1127.HeadingUpdate(heading, data[10], captured.SessionId, PlayerPositionUpdateHandler.ActionFlags.TORCH | PlayerPositionUpdateHandler.ActionFlags.TARGET_IN_VIEW, targetId), captured);
            }
        }

        [Test]
        public void PositionUpdateWhileAttacking_ShouldMatchCapture()
        {
            CapturedPacket[] attacking = ClientCaptures.WhilePlaying(ClientCaptures.FIGHT, eClientPackets.PositionUpdate).Where(static p => (p.Data[35] & 0x80) != 0).ToArray();
            Assert.That(attacking, Is.Not.Empty);

            foreach (CapturedPacket captured in attacking)
            {
                byte[] data = captured.Data;
                Vector3 position = new(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(0)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(8)));
                ushort heading = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(30));
                ushort targetId = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(22));
                ushort zoneId = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(24));
                PlayerPositionUpdateHandler.ActionFlags flags = PlayerPositionUpdateHandler.ActionFlags.TORCH | PlayerPositionUpdateHandler.ActionFlags.TARGET_IN_VIEW;
                Assert.That(targetId, Is.Not.Zero);

                // Data offsets 16-19 are the Z speed, which the Human's runs on sloped ground set and the Harness leaves at 0.
                AssertMatches(ClientPackets1127.PositionUpdate(position, zoneId, heading, ClientCaptures.Speed(captured), data[35], captured.SessionId, 0, flags, targetId), captured, 16, 17, 18, 19);
            }
        }

        [Test]
        public void HeadingUpdate_ShouldMatchCapture()
        {
            // The first of the turn on the spot. The full packet is in the README.
            CapturedPacket captured = ClientCaptures.WhilePlaying(ClientCaptures.MOVEMENT, eClientPackets.PlayerHeadingUpdate).First();

            AssertMatches(ClientPackets1127.HeadingUpdate(0x06D6, 100, captured.SessionId, PlayerPositionUpdateHandler.ActionFlags.TORCH), captured);
        }

        [TestCaseSource(nameof(HeadingUpdateCaptures))]
        public void EveryHeadingUpdate_ShouldMatchCapture(string capture)
        {
            // Only the heading varies, so it's taken from the captured packet.
            CapturedPacket[] headingUpdates = ClientCaptures.WhilePlaying(capture, eClientPackets.PlayerHeadingUpdate);
            Assert.That(headingUpdates, Is.Not.Empty);

            foreach (CapturedPacket captured in headingUpdates)
            {
                ushort heading = BinaryPrimitives.ReadUInt16BigEndian(captured.Data.AsSpan(4));
                AssertMatches(ClientPackets1127.HeadingUpdate(heading, 100, captured.SessionId, PlayerPositionUpdateHandler.ActionFlags.TORCH), captured);
            }
        }

        [TestCaseSource(nameof(Captures))]
        public void ClientFilterSettings_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.ClientFilterSettings(initial: true), FromWorldEntry(capture, eClientPackets.ClientFilterSettings, 0));
            AssertMatches(ClientPackets1127.ClientFilterSettings(initial: false), FromWorldEntry(capture, eClientPackets.ClientFilterSettings, 1));
            AssertMatches(ClientPackets1127.ClientFilterSettings(initial: false), FromWorldEntry(capture, eClientPackets.ClientFilterSettings, 2));
        }

        [TestCaseSource(nameof(Captures))]
        public void Command_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.Command("&gc info 1"), FromWorldEntry(capture, eClientPackets.CommandHandler, 0));
        }

        [TestCaseSource(nameof(Captures))]
        public void Quit_ShouldMatchCapture(string capture)
        {
            // The captured Human typed the `/q` alias, the Harness sends `/quit`. Only the command text differs.
            CapturedPacket captured = ClientCaptures.WhilePlaying(capture, eClientPackets.CommandHandler).Last();
            Assert.That(captured.Data, Is.EqualTo("\0&q\0"u8.ToArray()), "captured command");

            ClientPacket quit = ClientPackets1127.Quit();
            Assert.That(quit.Data, Is.EqualTo("\0&quit\0"u8.ToArray()), "command");
            AssertMatches(quit with { Data = captured.Data }, captured);
        }

        [Test]
        public void Say_ShouldMatchCapture()
        {
            // The Human typed `hello` without a slash. It's the first command in the capture.
            CapturedPacket captured = ClientCaptures.WhilePlaying(ClientCaptures.SAY, eClientPackets.CommandHandler).First();
            Assert.That(captured.Data, Is.EqualTo("\0&s hello\0"u8.ToArray()), "captured command");

            AssertMatches(ClientPackets1127.Say("hello"), captured);
        }

        [TestCaseSource(nameof(Captures))]
        public void ShowWarmapRequest_ShouldMatchCapture(string capture)
        {
            AssertMatches(ClientPackets1127.ShowWarmapRequest(), FromWorldEntry(capture, eClientPackets.ShowWarmapRequest, 0));
        }

        // The Human's client logged in next to Greeen, whose session ID was 4.
        [Test]
        public void CreatePlayerRequest_ShouldMatchCapture()
        {
            CapturedPacket captured = ClientCaptures.WhilePlaying(ClientCaptures.LOGIN_NEXT_TO_AGENT, eClientPackets.CreatePlayerRequest).Single();

            AssertMatches(ClientPackets1127.CreatePlayerRequest(4), captured);
        }

        [Test]
        public void CreatePlayerRequest_ShouldCarryTheSessionIdAsTheHandlerReadsIt()
        {
            GSPacketIn packet = Loaded(ClientPackets1127.CreatePlayerRequest(0x1234));

            Assert.That(packet.Code, Is.EqualTo((ushort) eClientPackets.CreatePlayerRequest));
            Assert.That(packet.ReadShortLowEndian(), Is.EqualTo(0x1234));
        }

        // Every style Dalta used from the quickbar, with Mobs targeted: Shining Blade is 9 and Return Blade 10 after the specializations.
        [Test]
        public void UseSkill_ShouldMatchCapture()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.STYLES, eClientPackets.UseSkill);
            Assert.That(captured, Has.Length.EqualTo(7));

            foreach (CapturedPacket packet in captured)
            {
                byte[] data = packet.Data;
                Vector3 position = new(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(0)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(8)));
                AssertMatches(ClientPackets1127.UseSkill(position, ClientCaptures.Speed(packet), Short(packet, 16), 0xA000, data[20]), packet);
            }
        }

        // Fearnan's casts from the quickbar, standing, with itself or another targeted: a hybrid's spell is a skill like a style, Minor Revigoration 6 and Minor Bark Skin 7 after the specializations.
        [TestCase(ClientCaptures.CAST_HEAL, new byte[] { 6, 6 })]
        [TestCase(ClientCaptures.CAST_BUFFS, new byte[] { 6, 7, 6, 7 })]
        public void UseSkill_ShouldMatchCapture_ForASpell(string capture, byte[] indexes)
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(capture, eClientPackets.UseSkill);
            Assert.That(captured, Has.Length.EqualTo(indexes.Length));

            for (int i = 0; i < captured.Length; i++)
                AssertMatches(ClientPackets1127.UseSkill(new(294695.78f, 641726.6f, 4848), 0, 0x048C, 0xA000, indexes[i]), captured[i]);
        }

        // Vue's clicks on Quick Cast: an ability is a skill like a style, 3 after the specializations, with something targeted but the second time.
        [Test]
        public void UseSkill_ShouldMatchCapture_ForAnAbility()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.QUICK_CAST, eClientPackets.UseSkill);
            Assert.That(captured, Has.Length.EqualTo(4));

            for (int i = 0; i < captured.Length; i++)
            {
                byte[] data = captured[i].Data;
                Vector3 position = new(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(0)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(8)));
                AssertMatches(ClientPackets1127.UseSkill(position, ClientCaptures.Speed(captured[i]), Short(captured[i], 16), i == 1 ? (ushort) 0 : (ushort) 0xA000, 3), captured[i]);
            }
        }

        // Orla's casts from the quickbar: Lesser Powerward, level 1 of Way of the Moon (line 1), with nothing targeted, then Gleam Ray, level 1 of Way of the Sun (line 0), at a Mob.
        [Test]
        public void UseSpell_ShouldMatchCapture()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.CAST_DAMAGE, eClientPackets.UseSpell);
            Assert.That(captured, Has.Length.EqualTo(15));

            for (int i = 0; i < captured.Length; i++)
            {
                byte[] data = captured[i].Data;
                Vector3 position = new(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(0)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(8)));
                (ushort flags, byte line) = i == 0 ? ((ushort) 0, (byte) 1) : ((ushort) 0xA000, (byte) 0);
                AssertMatches(ClientPackets1127.UseSpell(position, ClientCaptures.Speed(captured[i]), Short(captured[i], 16), flags, 1, line), captured[i]);
            }
        }

        // Both clicks on the top entry of the concentration window: the second buff moved up once the first was gone.
        [Test]
        public void RemoveConcentrationEffect_ShouldMatchCapture()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.CAST_BUFFS, eClientPackets.RemoveConcentrationEffect);
            Assert.That(captured, Has.Length.EqualTo(2));

            foreach (CapturedPacket packet in captured)
                AssertMatches(ClientPackets1127.RemoveConcentrationEffect(0), packet);
        }

        // Fearnan's clicks on each button in the pet window, in the order the Human announced them: aggressive, defensive, passive, stay, go to target, here, follow, attack twice, release.
        // One byte per row of the window, the others 0, then a 0 the server doesn't read.
        [Test]
        public void PetWindow_ShouldMatchCapture()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.PET_WINDOW, eClientPackets.PetWindow);
            (byte Aggression, byte Walk, byte Command)[] clicks = [(1, 0, 0), (2, 0, 0), (3, 0, 0), (0, 2, 0), (0, 3, 0), (0, 4, 0), (0, 1, 0), (0, 0, 1), (0, 0, 1), (0, 0, 2)];
            Assert.That(captured, Has.Length.EqualTo(clicks.Length));

            for (int i = 0; i < captured.Length; i++)
                AssertMatches(ClientPackets1127.PetWindow(clicks[i].Aggression, clicks[i].Walk, clicks[i].Command), captured[i]);
        }

        // The right-click on the Recall Stone in backpack slot 40, standing with nothing targeted.
        [Test]
        public void UseSlot_ShouldMatchCapture_FromTheBackpack()
        {
            CapturedPacket packet = ClientCaptures.WhilePlaying(ClientCaptures.USE_ITEM, eClientPackets.UseSlot)[0];

            AssertMatches(ClientPackets1127.UseSlot(new(341883, 592145.25f, 5473.81982421875f), 0, 0x0C00, 0, 40, eUseType.Use1), packet);
        }

        // Every use in the capture, from the backpack and the quickbar, standing and moving, all laid out alike.
        [Test]
        public void UseSlot_ShouldMatchCapture()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.USE_ITEM, eClientPackets.UseSlot);
            Assert.That(captured, Has.Length.EqualTo(7));

            foreach (CapturedPacket packet in captured)
            {
                byte[] data = packet.Data;
                Vector3 position = new(BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(0)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(8)));
                AssertMatches(ClientPackets1127.UseSlot(position, ClientCaptures.Speed(packet), Short(packet, 16), Short(packet, 18), 40, (eUseType) data[21]), packet);
            }
        }

        // Every move in the capture, equipping and unequipping, rearranging the backpack and dropping an item on the ground (slot 1). The client always sent a count of 0.
        [Test]
        public void MoveItem_ShouldMatchCapture()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.ITEMS, eClientPackets.PlayerMoveItem);
            Assert.That(captured, Is.Not.Empty);

            foreach (CapturedPacket packet in captured)
                AssertMatches(ClientPackets1127.MoveItem(packet.SessionId, Short(packet, 4), Short(packet, 2), 0), packet);
        }

        // Shift-clicks on items in the backpack, in the equipment and in a merchant's window. The salvage request (type 102) goes the same way, and has no builder.
        [Test]
        public void ExamineAndExamineMerchantItem_ShouldMatchCapture()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.ITEMS, eClientPackets.DetailRequest);
            CapturedPacket[] inventory = captured.Where(static packet => Short(packet, 0) == 1).ToArray();
            CapturedPacket[] merchant = captured.Where(static packet => Short(packet, 0) == 4).ToArray();
            Assert.That(inventory, Is.Not.Empty);
            Assert.That(merchant, Is.Not.Empty);

            foreach (CapturedPacket packet in inventory)
                AssertMatches(ClientPackets1127.Examine(Short(packet, 6)), packet);

            foreach (CapturedPacket packet in merchant)
                AssertMatches(ClientPackets1127.ExamineMerchantItem(Short(packet, 6)), packet);
        }

        [Test]
        public void InteractRequest_ShouldMatchCapture_AfterTargetingTheObject()
        {
            CapturedPacket[] packets = ClientCaptures.Load(ClientCaptures.ITEMS);
            int[] interacts = [.. Enumerable.Range(0, packets.Length).Where(i => packets[i].Code == (byte) eClientPackets.ObjectInteractRequest)];
            Assert.That(interacts, Is.Not.Empty);

            foreach (int i in interacts)
            {
                CapturedPacket packet = packets[i];
                AssertMatches(ClientPackets1127.InteractRequest(Int(packet, 0), Int(packet, 4), packet.SessionId, Short(packet, 10)), packet);
                Assert.That(Short(packets[i - 1], 0), Is.EqualTo(Short(packet, 10)), "targeted first");
            }
        }

        [Test]
        public void BuyRequest_ShouldMatchCapture()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.ITEMS, eClientPackets.BuyRequest);
            Assert.That(captured, Is.Not.Empty);

            foreach (CapturedPacket packet in captured)
                AssertMatches(ClientPackets1127.BuyRequest(Int(packet, 0), Int(packet, 4), packet.SessionId, Short(packet, 10), packet.Data[12]), packet);
        }

        [Test]
        public void SellRequest_ShouldMatchCapture()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.ITEMS, eClientPackets.SellRequest);
            Assert.That(captured, Is.Not.Empty);

            foreach (CapturedPacket packet in captured)
                AssertMatches(ClientPackets1127.SellRequest(Int(packet, 0), Int(packet, 4), packet.SessionId, Short(packet, 10)), packet);
        }

        // The client targeted the loot first, then sent a single byte: the server picks up its target.
        [Test]
        public void PickUpRequest_ShouldMatchCapture_AfterTargetingTheObject()
        {
            CapturedPacket[] packets = ClientCaptures.Load(ClientCaptures.ITEMS);
            int i = Array.FindIndex(packets, static packet => packet.Code == (byte) eClientPackets.PickUpRequest);
            Assert.That(i, Is.Positive);

            AssertMatches(ClientPackets1127.PickUpRequest(), packets[i]);
            Assert.That(packets[i - 1].Code, Is.EqualTo((byte) eClientPackets.PlayerTarget), "targeted first");
        }

        // The Human declined the Healer's Dialog, then accepted it.
        [Test]
        public void DialogResponse_ShouldMatchCapture()
        {
            CapturedPacket[] captured = ClientCaptures.WhilePlaying(ClientCaptures.HEALER_DIALOG, eClientPackets.DialogResponse);
            Assert.That(captured, Has.Length.EqualTo(2));

            AssertMatches(ClientPackets1127.DialogResponse((byte) eDialogCode.CustomDialog, captured[0].SessionId, 0x01, 0x00, false), captured[0]);
            AssertMatches(ClientPackets1127.DialogResponse((byte) eDialogCode.CustomDialog, captured[1].SessionId, 0x01, 0x00, true), captured[1]);
        }

        [TestCase(100, 0x00)]
        [TestCase(109, 0x09)]
        [TestCase(200, 0x0A)]
        [TestCase(300, 0x14)]
        [TestCase(309, 0x1D)]
        public void CharacterIndex_ShouldNumberSlotsAcrossRealms(int accountSlot, int expected)
        {
            Assert.That(ClientPackets1127.CharacterIndex(accountSlot), Is.EqualTo(expected));
        }

        internal static void AssertMatches(ClientPacket built, CapturedPacket captured, params int[] maskedDataOffsets)
        {
            CapturedPacket wire = new(0, CaptureTransport.Tcp, captured.ClientState, built.ToWire(captured.Sequence, captured.SessionId));

            Assert.That(wire.HasValidChecksum, Is.True, "checksum");
            Assert.That(wire.Bytes[..8], Is.EqualTo(captured.Bytes[..8]), "header");
            Assert.That(wire.Code, Is.EqualTo(captured.Code), "code");
            Assert.That(Masked(wire.Data, maskedDataOffsets), Is.EqualTo(Masked(captured.Data, maskedDataOffsets)), "data");
        }

        // Big-endian, at a data offset.
        private static ushort Short(CapturedPacket packet, int offset)
        {
            return BinaryPrimitives.ReadUInt16BigEndian(packet.Data.AsSpan(offset));
        }

        private static uint Int(CapturedPacket packet, int offset)
        {
            return BinaryPrimitives.ReadUInt32BigEndian(packet.Data.AsSpan(offset));
        }

        // The packet as a handler gets it, ready to read its data.
        private static GSPacketIn Loaded(ClientPacket built)
        {
            byte[] wire = built.ToWire(7, 0x0101);
            GSPacketIn packet = new();
            packet.Load(wire, 0, wire.Length);
            return packet;
        }

        private static byte[] Masked(byte[] data, int[] offsets)
        {
            byte[] masked = (byte[]) data.Clone();

            foreach (int offset in offsets)
            {
                if (offset < masked.Length)
                    masked[offset] = 0;
            }

            return masked;
        }

        // The nth packet with this code in the world-entry part of a capture: from the first packet at the character screen until the client sends its first ping while Playing.
        private static CapturedPacket FromWorldEntry(string capture, eClientPackets code, int occurrence)
        {
            CapturedPacket[] packets = ClientCaptures.Load(capture);
            int start = Array.FindIndex(packets, static p => p.ClientState is GameClient.eClientState.CharScreen);
            int end = Array.FindIndex(packets, static p => p.ClientState is GameClient.eClientState.Playing && p.Code == (byte) eClientPackets.PingRequest);
            return packets[start..end].Where(p => p.Code == (byte) code).ElementAt(occurrence);
        }
    }
}
