using System.Text;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using DOL.GS.Styles;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// The JSON a Mind and the server exchange. The Mind's side is the generated `mind/src/protocol.ts`.
    /// </summary>
    [TestFixture]
    public class UT_MindProtocol
    {
        [Test]
        public void Read_ShouldReadAHello_WhateverOrderItsFieldsAreIn()
        {
            Assert.That(Read("""{"type":"hello","token":"secret","character":"Greeen"}"""), Is.EqualTo(new HelloMessage("secret", "Greeen")));
            Assert.That(Read("""{"character":"Greeen","token":"secret","type":"hello"}"""), Is.EqualTo(new HelloMessage("secret", "Greeen")));
        }

        [Test]
        public void Read_ShouldReadAQuitPrimitive()
        {
            MindMessage message = Read("""{"type":"primitive","id":7,"primitive":{"name":"quit"}}""");

            Assert.That(message, Is.EqualTo(new PrimitiveMessage(7, new QuitPrimitive())));
        }

        [Test]
        public void Read_ShouldReadASayPrimitive()
        {
            MindMessage message = Read("""{"type":"primitive","id":8,"primitive":{"name":"say","text":"hello"}}""");

            Assert.That(message, Is.EqualTo(new PrimitiveMessage(8, new SayPrimitive("hello"))));
        }

        [Test]
        public void Read_ShouldReadAMoveToPrimitive()
        {
            MindMessage message = Read("""{"type":"primitive","id":9,"primitive":{"name":"moveTo","x":531234.5,"y":478901,"z":2345}}""");

            Assert.That(message, Is.EqualTo(new PrimitiveMessage(9, new MoveToPrimitive(531234.5f, 478901, 2345))));
        }

        [Test]
        public void Read_ShouldReadAMoveToPrimitive_WithAWithinDistance()
        {
            MindMessage message = Read("""{"type":"primitive","id":9,"primitive":{"name":"moveTo","x":531234.5,"y":478901,"z":2345,"within":100}}""");

            Assert.That(message, Is.EqualTo(new PrimitiveMessage(9, new MoveToPrimitive(531234.5f, 478901, 2345, 100))));
        }

        [Test]
        public void Write_ShouldWriteARoutedPercept_WithItsCornersInOrder()
        {
            RoutedPercept routed = new([new(1, 2, 3, true), new(4.5f, 5, 6, false)], false);

            Assert.That(Write(new PerceptMessage(7, 2200, routed)),
                Is.EqualTo("""{"type":"percept","sequence":7,"elapsedMs":2200,"percept":{"kind":"routed","corners":[{"x":1,"y":2,"z":3,"swim":true},{"x":4.5,"y":5,"z":6,"swim":false}],"reachesTarget":false}}"""));
        }

        [Test]
        public void Read_ShouldReadAStopPrimitive()
        {
            MindMessage message = Read("""{"type":"primitive","id":10,"primitive":{"name":"stop"}}""");

            Assert.That(message, Is.EqualTo(new PrimitiveMessage(10, new StopPrimitive())));
        }

        [Test]
        public void Read_ShouldReadAFacePrimitive()
        {
            MindMessage message = Read("""{"type":"primitive","id":11,"primitive":{"name":"face","heading":1523}}""");

            Assert.That(message, Is.EqualTo(new PrimitiveMessage(11, new FacePrimitive(1523))));
        }

        [Test]
        public void Read_ShouldReadTheFightingAndRestingPrimitives()
        {
            Assert.That(Read("""{"type":"primitive","id":12,"primitive":{"name":"target","objectId":7947}}"""), Is.EqualTo(new PrimitiveMessage(12, new TargetPrimitive(7947))));
            Assert.That(Read("""{"type":"primitive","id":13,"primitive":{"name":"attack","on":true}}"""), Is.EqualTo(new PrimitiveMessage(13, new AttackPrimitive(true))));
            Assert.That(Read("""{"type":"primitive","id":14,"primitive":{"name":"sit"}}"""), Is.EqualTo(new PrimitiveMessage(14, new SitPrimitive())));
            Assert.That(Read("""{"type":"primitive","id":15,"primitive":{"name":"stand"}}"""), Is.EqualTo(new PrimitiveMessage(15, new StandPrimitive())));
        }

        [Test]
        public void Read_ShouldReadACommandPrimitive()
        {
            MindMessage message = Read("""{"type":"primitive","id":16,"primitive":{"name":"command","text":"/train Blades 3"}}""");

            Assert.That(message, Is.EqualTo(new PrimitiveMessage(16, new CommandPrimitive("/train Blades 3"))));
        }

        [Test]
        public void Read_ShouldReadTheItemAndMerchantPrimitives()
        {
            Assert.That(Read("""{"type":"primitive","id":17,"primitive":{"name":"moveItem","from":41,"to":10}}"""), Is.EqualTo(new PrimitiveMessage(17, new MoveItemPrimitive(41, 10))));
            Assert.That(Read("""{"type":"primitive","id":18,"primitive":{"name":"examine","slot":41}}"""), Is.EqualTo(new PrimitiveMessage(18, new ExaminePrimitive(41))));
            Assert.That(Read("""{"type":"primitive","id":19,"primitive":{"name":"examineMerchantItem","index":31}}"""), Is.EqualTo(new PrimitiveMessage(19, new ExamineMerchantItemPrimitive(31))));
            Assert.That(Read("""{"type":"primitive","id":20,"primitive":{"name":"pickUp","objectId":7947}}"""), Is.EqualTo(new PrimitiveMessage(20, new PickUpPrimitive(7947))));
            Assert.That(Read("""{"type":"primitive","id":22,"primitive":{"name":"talkTo","objectId":7947}}"""), Is.EqualTo(new PrimitiveMessage(22, new TalkToPrimitive(7947))));
            Assert.That(Read("""{"type":"primitive","id":23,"primitive":{"name":"buy","index":31,"count":1}}"""), Is.EqualTo(new PrimitiveMessage(23, new BuyPrimitive(31, 1))));
            Assert.That(Read("""{"type":"primitive","id":24,"primitive":{"name":"sell","slot":43}}"""), Is.EqualTo(new PrimitiveMessage(24, new SellPrimitive(43))));
        }

        [Test]
        public void Read_ShouldReadAUseStylePrimitive_WithOrWithoutALine()
        {
            Assert.That(Read("""{"type":"primitive","id":25,"primitive":{"name":"useStyle","style":"Shining Blade"}}"""), Is.EqualTo(new PrimitiveMessage(25, new UseStylePrimitive("Shining Blade"))));
            Assert.That(Read("""{"type":"primitive","id":26,"primitive":{"name":"useStyle","style":"Slam","line":"Shields"}}"""), Is.EqualTo(new PrimitiveMessage(26, new UseStylePrimitive("Slam", "Shields"))));
        }

        [Test]
        public void Read_ShouldReadAnAnswerDialogPrimitive_WithItsCodeAsAName()
        {
            Assert.That(Read("""{"type":"primitive","id":27,"primitive":{"name":"answerDialog","code":"CustomDialog","data1":257,"data2":1,"data3":0,"accept":true}}"""),
                Is.EqualTo(new PrimitiveMessage(27, new AnswerDialogPrimitive(eDialogCode.CustomDialog, 257, 1, 0, true))));
        }

        [Test]
        public void Write_ShouldWriteTheDialogAndStatsPercepts()
        {
            Assert.That(Write(new PerceptMessage(15, 2600, new DialogPercept(eDialogCode.CustomDialog, 257, 1, 0, true, "Do you accept?"))),
                Does.EndWith("""{"kind":"dialog","code":"CustomDialog","data1":257,"data2":1,"data3":0,"yesNo":true,"text":"Do you accept?"}}"""));
            Assert.That(Write(new PerceptMessage(16, 2700, new StatsPercept(7))), Does.EndWith("""{"kind":"stats","constitutionLost":7}}"""));
        }

        [Test]
        public void Write_ShouldWriteASkillsPercept_WithAStylesLineAndNoneForAnythingElse()
        {
            string json = Write(new PerceptMessage(13, 2400, new SkillsPercept([new("Blades", 5, SkillPage.Specialization), new("Shining Blade", 2, SkillPage.Styles, "Blades")])));

            Assert.That(json, Does.EndWith("""{"kind":"skills","skills":[{"name":"Blades","level":5,"page":"Specialization"},{"name":"Shining Blade","level":2,"page":"Styles","line":"Blades"}]}}"""));
        }

        [Test]
        public void Write_ShouldWriteAStylesDetails_WithItsOpeningAsNames()
        {
            StyleDetails glowingBlade = new(Style.eOpening.Positional, Style.eAttackResultRequirement.Any, Style.eOpeningPosition.Side, null, ["Auroric Blade"], 10, 0, 5, 0, false, null);
            string json = Write(new PerceptMessage(14, 2500, new SkillsPercept([new("Glowing Blade", 10, SkillPage.Styles, "Blades", glowingBlade)])));

            Assert.That(json, Does.EndWith("""{"name":"Glowing Blade","level":10,"page":"Styles","line":"Blades","details":{"opening":"Positional","openingResult":"Any","openingPosition":"Side","followupStyles":["Auroric Blade"],"fatigue":10,"attackMod":0,"defensiveMod":5,"openingDamage":0,"hidden":false}}]}}"""));
        }

        [Test]
        public void Write_ShouldWriteAnInventoryPercept_WithItsWindowAsANameAndEmptySlotsWithoutAnItem()
        {
            InventoryItem falcata = new("falcata", 2, 22, 34, 1, 2, 2, 19, 11, 25, 50, 100, 85, 0, 0, 445, 7, 0, []);
            string json = Write(new PerceptMessage(8, 1900, new InventoryPercept(eInventoryWindowType.Update, [new(10, falcata), new(40, null)])));

            Assert.That(json, Is.EqualTo("""{"type":"percept","sequence":8,"elapsedMs":1900,"percept":{"kind":"inventory","window":"Update","slots":[{"slot":10,"item":{"name":"falcata","level":2,"dpsAf":22,"speedAbs":34,"count":1,"hand":2,"damageType":2,"objectType":19,"itemType":11,"weight":25,"condition":50,"durability":100,"quality":85,"bonus":0,"bonusLevel":0,"model":445,"color":7,"effect":0,"spellNames":[]}},{"slot":40}]}}"""));
        }

        [Test]
        public void Write_ShouldWriteTheMoneyEncumbranceExaminedAndMerchantWindowPercepts()
        {
            Assert.That(Write(new PerceptMessage(9, 2000, new MoneyPercept(82, 1, 3, 0, 0))), Does.EndWith("""{"kind":"money","copper":82,"silver":1,"gold":3,"mithril":0,"platinum":0}}"""));
            Assert.That(Write(new PerceptMessage(10, 2100, new EncumbrancePercept(72, 58))), Does.EndWith("""{"kind":"encumbrance","capacity":72,"weight":58}}"""));
            Assert.That(Write(new PerceptMessage(11, 2200, new ExaminedPercept("falcata", ["- 3 Strength"]))), Does.EndWith("""{"kind":"examined","caption":"falcata","lines":["- 3 Strength"]}}"""));
            Assert.That(Write(new PerceptMessage(12, 2300, new MerchantWindowPercept(eMerchantWindowType.Normal, [new(0, 1, 2, 22, 34, 1, 2, 2, 19, 11, true, 25, 250, 445, "falcata")]))),
                Does.EndWith("""{"kind":"merchantWindow","window":"Normal","items":[{"page":0,"place":1,"level":2,"dpsAf":22,"speedAbs":34,"count":1,"hand":2,"damageType":2,"objectType":19,"itemType":11,"usable":true,"weight":25,"price":250,"model":445,"name":"falcata"}]}}"""));
        }

        // Neither a missing object ID nor a missing switch means 0 or off.
        [TestCase("""{"type":"primitive","id":1,"primitive":{"name":"buy","index":31}}""", "count")]
        [TestCase("""{"type":"primitive","id":1,"primitive":{"name":"moveItem","from":41}}""", "to")]
        [TestCase("""{"type":"primitive","id":1,"primitive":{"name":"target"}}""", "objectId")]
        [TestCase("""{"type":"primitive","id":1,"primitive":{"name":"attack"}}""", "on")]
        [TestCase("""{"type":"primitive","id":1,"primitive":{"name":"command"}}""", "text")]
        [TestCase("""{"type":"primitive","id":1,"primitive":{"name":"useStyle","line":"Blades"}}""", "style")]
        [TestCase("""{"type":"primitive","id":1,"primitive":{"name":"answerDialog","code":"CustomDialog","data1":257,"data2":1,"data3":0}}""", "accept")]
        public void Read_ShouldSayWhy_ForAPrimitiveWithoutItsField(string json, string field)
        {
            Assert.That(MindProtocol.Read(Encoding.UTF8.GetBytes(json), out string error), Is.Null);
            Assert.That(error, Does.Contain(field));
        }

        // A missing heading isn't heading 0.
        [Test]
        public void Read_ShouldSayWhy_ForAFaceWithoutAHeading()
        {
            MindMessage message = MindProtocol.Read("""{"type":"primitive","id":11,"primitive":{"name":"face"}}"""u8, out string error);

            Assert.That(message, Is.Null);
            Assert.That(error, Does.Contain("'heading'"));
        }

        // All three coordinates are required: a missing Z isn't the ground at 0.
        [Test]
        public void Read_ShouldSayWhy_ForAMoveToWithoutAllThreeCoordinates()
        {
            MindMessage message = MindProtocol.Read("""{"type":"primitive","id":9,"primitive":{"name":"moveTo","x":531234,"y":478901}}"""u8, out string error);

            Assert.That(message, Is.Null);
            Assert.That(error, Does.Contain("'z'"));
        }

        [Test]
        public void Write_ShouldWriteAStoppedPercept_WithItsReasonAsAName()
        {
            Assert.That(Write(new PerceptMessage(5, 2000, new StoppedPercept(1.5f, 2, 3, StopReason.Replaced))),
                Is.EqualTo("""{"type":"percept","sequence":5,"elapsedMs":2000,"percept":{"kind":"stopped","x":1.5,"y":2,"z":3,"reason":"Replaced"}}"""));
        }

        [Test]
        public void Write_ShouldWriteAnUnreachableStop()
        {
            Assert.That(Write(new PerceptMessage(5, 2000, new StoppedPercept(1, 2, 3, StopReason.Unreachable))), Does.EndWith("\"reason\":\"Unreachable\"}}"));
        }

        [Test]
        public void Write_ShouldLeaveOutAMissingServerName_FromAnOwnPositionPercept()
        {
            Assert.That(Write(new PerceptMessage(6, 2100, new OwnPositionPercept(1, 2, 3, 4, 5, 0, 0, 201, false, false, null))), Does.Not.Contain("serverName"));
        }

        [TestCase("not json")]
        [TestCase("null")]
        [TestCase("""{"token":"secret","character":"Greeen"}""")]
        [TestCase("""{"type":"dance"}""")]
        [TestCase("""{"type":"primitive","id":1,"primitive":{"name":"dance"}}""")]
        public void Read_ShouldGiveNothing_ForAMessageItCannotRead(string json)
        {
            Assert.That(Read(json), Is.Null);
        }

        [TestCase("not json", "isn't JSON")]
        [TestCase("""{"type":"dance"}""", "'dance'")]
        [TestCase("""{"type":"primitive","id":1,"primitive":{"name":"dance"}}""", "'dance'")]
        [TestCase("""{"type":"primitive","id":"one","primitive":{"name":"quit"}}""", "$.id")]
        [TestCase("""{"token":"secret","character":"Greeen"}""", "\"type\"")]
        public void Read_ShouldSayWhy_ForAMessageItCannotRead(string json, string why)
        {
            MindMessage message = MindProtocol.Read(Encoding.UTF8.GetBytes(json), out string error);

            Assert.That(message, Is.Null);
            Assert.That(error, Does.Contain(why));
        }

        [Test]
        public void Read_ShouldGiveNoError_ForAMessageItCanRead()
        {
            MindProtocol.Read("""{"type":"hello","token":"secret","character":"Greeen"}"""u8, out string error);

            Assert.That(error, Is.Null);
        }

        [Test]
        public void Write_ShouldWriteAPercept_WithItsSequenceAndTime()
        {
            Assert.That(Write(new PerceptMessage(3, 1250, new BarePercept("SendUpdatePoints"))),
                Is.EqualTo("""{"type":"percept","sequence":3,"elapsedMs":1250,"percept":{"kind":"bare","call":"SendUpdatePoints"}}"""));
        }

        [Test]
        public void Write_ShouldWriteAMessagePercept_WithItsChatTypeAndLocationAsNames()
        {
            Assert.That(Write(new PerceptMessage(4, 1500, new MessagePercept("Vue says, \"hello\"", eChatType.CT_Say, eChatLoc.CL_ChatWindow))),
                Is.EqualTo("""{"type":"percept","sequence":4,"elapsedMs":1500,"percept":{"kind":"message","text":"Vue says, \"hello\"","chatType":"CT_Say","chatLocation":"CL_ChatWindow"}}"""));
        }

        [Test]
        public void Write_ShouldWriteAPlayerCreate_WithItsRealmAsANameAndWithoutAHorse_WhenThePlayerIsntRiding()
        {
            PlayerCreatePercept percept = new(531234, 478901, 2345.5f, 0x0304, 0x0123, 2048, 0x02BE, 23, PacketRealm.Midgard, false, false, false, false, false, 0, 0, 0, 0, 0, 0, 0, "Vue", "", "", "", "", null);
            string json = Write(new PerceptMessage(4, 1500, percept));

            Assert.That(json, Does.StartWith("""{"type":"percept","sequence":4,"elapsedMs":1500,"percept":{"kind":"playerCreate","x":531234,"y":478901,"z":2345.5,"""));
            Assert.That(json, Does.Contain("\"realm\":\"Midgard\""));
            Assert.That(json, Does.Not.Contain("horse"));
        }

        [Test]
        public void Write_ShouldWriteAnNpcCreate_WithItsQuestIndicatorAsAName()
        {
            NpcCreatePercept percept = new(531234, 478901, 2345, 0x0456, 0, 0, 3072, 0x0190, 40, 3, false, PacketRealm.None, false, false, false, false, false, false, false, false, false, eQuestIndicator.Available, "rat", "");
            string json = Write(new PerceptMessage(5, 1600, percept));

            Assert.That(json, Does.StartWith("""{"type":"percept","sequence":5,"elapsedMs":1600,"percept":{"kind":"npcCreate","x":531234,"y":478901,"z":2345,"""));
            Assert.That(json, Does.Contain("\"questIndicator\":\"Available\""));
        }

        [Test]
        public void Write_ShouldWriteAnObjectUpdate_WithoutADestination_WhenTheNpcIsntGoingAnywhere()
        {
            ObjectUpdatePercept standing = new(531234, 478901, 2345, 0x0456, 0, 0, 3072, 0, 75, PacketRealm.None, false, false, false, false, 101, null);
            ObjectUpdatePercept moving = standing with { Destination = new(533234, 478901, 2845, 101) };

            Assert.That(Write(new PerceptMessage(6, 1700, standing)), Does.StartWith("""{"type":"percept","sequence":6,"elapsedMs":1700,"percept":{"kind":"objectUpdate","x":531234,""").And.Not.Contain("destination"));
            Assert.That(Write(new PerceptMessage(7, 1800, moving)), Does.Contain("""
            "destination":{"x":533234,"y":478901,"z":2845,"zoneId":101}
            """));
        }

        [Test]
        public void Write_ShouldWriteAQueuedPrimitiveResult_WithoutAReason()
        {
            Assert.That(Write(new PrimitiveResultMessage(7, true, null)), Is.EqualTo("""{"type":"primitiveResult","id":7,"queued":true}"""));
            Assert.That(Write(new PrimitiveResultMessage(8, false, "Greeen isn't in the world.")), Is.EqualTo("""{"type":"primitiveResult","id":8,"queued":false,"reason":"Greeen isn't in the world."}"""));
        }

        [Test]
        public void Write_ShouldWriteEnteredRefusedAndReplaced()
        {
            Assert.That(Write(new EnteredMessage(TookOver: false)), Is.EqualTo("""{"type":"entered","tookOver":false}"""));
            Assert.That(Write(new EnteredMessage(TookOver: true)), Is.EqualTo("""{"type":"entered","tookOver":true}"""));
            Assert.That(Write(new RefusedMessage("No token.", false)), Is.EqualTo("""{"type":"refused","reason":"No token.","retry":false}"""));
            Assert.That(Write(new ReplacedMessage()), Is.EqualTo("""{"type":"replaced"}"""));
        }

        private static MindMessage Read(string json)
        {
            return MindProtocol.Read(Encoding.UTF8.GetBytes(json));
        }

        private static string Write(ServerMessage message)
        {
            return Encoding.UTF8.GetString(MindProtocol.Write(message));
        }
    }
}
