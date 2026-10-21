using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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

        // The packets queued for the client's next tick, in order.
        private static ClientPacket[] Inbound(HarnessClient client)
        {
            return [.. (ConcurrentQueue<ClientPacket>) typeof(HarnessClient).GetField("_inbound", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client)];
        }

        // Once closed, the link has sent everything it was given.
        private static async Task Close(FakeMind mind)
        {
            mind.Link.Close();
            await mind.Link.Completion;
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
