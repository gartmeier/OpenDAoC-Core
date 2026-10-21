using DOL.GS.Agents;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// The Agent's client asks the server for a player it gets positions for but doesn't know, as a real client does when it enters the world after that player.
    /// </summary>
    [TestFixture]
    public class UT_PlayerCreateRequests
    {
        private const int VUE = 17737;
        private const int VUE_SESSION = 2;

        [Test]
        public void OnPerceived_ShouldAskForAPlayer_ItGetsAPositionForButDoesntKnow()
        {
            PlayerCreateRequests requests = new();

            Assert.That(requests.OnPerceived(Position(VUE, VUE_SESSION), 1000), Is.EqualTo(VUE_SESSION));
        }

        [Test]
        public void OnPerceived_ShouldNotAsk_ForAPlayerItWasSentTheCreateOf()
        {
            PlayerCreateRequests requests = new();
            requests.OnPerceived(Create(VUE, VUE_SESSION), 1000);

            Assert.That(requests.OnPerceived(Position(VUE, VUE_SESSION), 1100), Is.Null);
        }

        // Positions keep coming, about five a second, while the create is on its way.
        [Test]
        public void OnPerceived_ShouldAskOnlyOnce_WhileItWaitsForTheCreate()
        {
            PlayerCreateRequests requests = new();
            requests.OnPerceived(Position(VUE, VUE_SESSION), 1000);

            for (long now = 1200; now < 1000 + PlayerCreateRequests.REQUEST_INTERVAL; now += 200)
                Assert.That(requests.OnPerceived(Position(VUE, VUE_SESSION), now), Is.Null);
        }

        // For example when the server sent no create because the Character can't see the player.
        [Test]
        public void OnPerceived_ShouldAskAgain_WhenNoCreateCameInTime()
        {
            PlayerCreateRequests requests = new();
            requests.OnPerceived(Position(VUE, VUE_SESSION), 1000);

            Assert.That(requests.OnPerceived(Position(VUE, VUE_SESSION), 1000 + PlayerCreateRequests.REQUEST_INTERVAL), Is.EqualTo(VUE_SESSION));
        }

        [Test]
        public void OnPerceived_ShouldAskForEachPlayerItDoesntKnow()
        {
            PlayerCreateRequests requests = new();
            requests.OnPerceived(Position(VUE, VUE_SESSION), 1000);

            Assert.That(requests.OnPerceived(Position(17738, 3), 1100), Is.EqualTo(3));
        }

        [Test]
        public void OnPerceived_ShouldAskAgain_ForAPlayerThatWentOutOfView()
        {
            PlayerCreateRequests requests = new();
            requests.OnPerceived(Create(VUE, VUE_SESSION), 1000);
            requests.OnPerceived(new ObjectDeletePercept(VUE), 2000);

            Assert.That(requests.OnPerceived(Position(VUE, VUE_SESSION), 3000), Is.EqualTo(VUE_SESSION));
        }

        [Test]
        public void OnPerceived_ShouldAskAgain_ForAPlayerThatLeftTheWorldAndCameBack()
        {
            PlayerCreateRequests requests = new();
            requests.OnPerceived(Create(VUE, VUE_SESSION), 1000);
            requests.OnPerceived(new ObjectRemovePercept(VUE, 2), 2000);

            Assert.That(requests.OnPerceived(Position(VUE, VUE_SESSION), 3000), Is.EqualTo(VUE_SESSION));
        }

        [Test]
        public void OnPerceived_ShouldAskForNothing_ForOtherPercepts()
        {
            PlayerCreateRequests requests = new();

            Assert.That(requests.OnPerceived(new BarePacketPercept("SendUDP", 0xBA), 1000), Is.Null);
            Assert.That(requests.OnPerceived(new ObjectDeletePercept(VUE), 1000), Is.Null);
        }

        private static PlayerPositionPercept Position(int objectId, int sessionId)
        {
            return new(296715, 642621, 4848, 191, 0, sessionId, objectId, 202, 0, 0, 1158, 1, 0, false, 100, false, 100, 100);
        }

        private static PlayerCreatePercept Create(int objectId, int sessionId)
        {
            return new(296715, 642621, 4848, sessionId, objectId, 1158, 0x02BE, 50, PacketRealm.Hibernia, false, false, false, false, false, 0, 0, 0, 0, 0, 0, 0, "Vue", "", "", "", "", null);
        }
    }
}
