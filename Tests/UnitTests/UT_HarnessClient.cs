using DOL.GS.Agents;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_HarnessClient
    {
        [Test]
        public void Dispatch_ShouldRefuseASayWithAReason_WhileTheAgentIsntInTheWorld()
        {
            string reason = new HarnessClient().Dispatch(new SayPrimitive("hello"));

            Assert.That(reason, Does.EndWith("isn't in the world."));
        }

        [Test]
        public void Dispatch_ShouldRefuseAQuitWithAReason_WhileTheAgentIsntInTheWorld()
        {
            string reason = new HarnessClient().Dispatch(new QuitPrimitive());

            Assert.That(reason, Does.EndWith("isn't in the world."));
        }
    }
}
