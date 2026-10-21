using System.Text;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
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
        public void Write_ShouldWriteAQueuedPrimitiveResult_WithoutAReason()
        {
            Assert.That(Write(new PrimitiveResultMessage(7, true, null)), Is.EqualTo("""{"type":"primitiveResult","id":7,"queued":true}"""));
            Assert.That(Write(new PrimitiveResultMessage(8, false, "Greeen isn't in the world.")), Is.EqualTo("""{"type":"primitiveResult","id":8,"queued":false,"reason":"Greeen isn't in the world."}"""));
        }

        [Test]
        public void Write_ShouldWriteEnteredRefusedAndReplaced()
        {
            Assert.That(Write(new EnteredMessage()), Is.EqualTo("""{"type":"entered"}"""));
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
