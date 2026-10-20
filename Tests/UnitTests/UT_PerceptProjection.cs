using System.Reflection;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_PerceptProjection
    {
        private static readonly MethodInfo SendUpdatePoints = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUpdatePoints));
        private static readonly MethodInfo SendMessage = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendMessage));
        private static readonly MethodInfo GetBowPrepare = typeof(IPacketLib).GetProperty(nameof(IPacketLib.BowPrepare)).GetMethod;

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

        private static GameClient Playing()
        {
            return new(null) { ClientState = GameClient.eClientState.Playing };
        }
    }
}
