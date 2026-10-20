using System.Threading.Tasks;
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
            string reason = Dispatch(new SayPrimitive("hello"));

            Assert.That(reason, Does.EndWith("isn't in the world."));
        }

        [Test]
        public void Dispatch_ShouldRefuseAQuitWithAReason_WhileTheAgentIsntInTheWorld()
        {
            string reason = Dispatch(new QuitPrimitive());

            Assert.That(reason, Does.EndWith("isn't in the world."));
        }

        // Every Harness client has a Mind. This one's link sends nowhere and is closed afterwards.
        private static string Dispatch(Primitive primitive)
        {
            MindLink mind = new(_ => Task.CompletedTask, () => Task.CompletedTask);

            try
            {
                return new HarnessClient(mind).Dispatch(primitive);
            }
            finally
            {
                mind.Close();
            }
        }
    }
}
