using System.Collections.Generic;
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

        // Stands in for a Mind's WebSocket and remembers what was sent.
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
