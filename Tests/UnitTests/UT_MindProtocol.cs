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

        [TestCase("not json")]
        [TestCase("null")]
        [TestCase("""{"token":"secret","character":"Greeen"}""")]
        [TestCase("""{"type":"dance"}""")]
        [TestCase("""{"type":"primitive","id":1,"primitive":{"name":"dance"}}""")]
        public void Read_ShouldGiveNothing_ForAMessageItCannotRead(string json)
        {
            Assert.That(Read(json), Is.Null);
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
        public void Write_ShouldWriteAQueuedPrimitiveResult_WithoutAReason()
        {
            Assert.That(Write(new PrimitiveResultMessage(7, true, null)), Is.EqualTo("""{"type":"primitiveResult","id":7,"queued":true}"""));
            Assert.That(Write(new PrimitiveResultMessage(8, false, "Greeen isn't in the world.")), Is.EqualTo("""{"type":"primitiveResult","id":8,"queued":false,"reason":"Greeen isn't in the world."}"""));
        }

        [Test]
        public void Write_ShouldWriteEnteredAndRefused()
        {
            Assert.That(Write(new EnteredMessage()), Is.EqualTo("""{"type":"entered"}"""));
            Assert.That(Write(new RefusedMessage("No token.", false)), Is.EqualTo("""{"type":"refused","reason":"No token.","retry":false}"""));
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
