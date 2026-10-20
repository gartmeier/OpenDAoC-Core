using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_MindLink
    {
        private static readonly MethodInfo SendUpdatePoints = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUpdatePoints));
        private static readonly MethodInfo SendPlayerQuit = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendPlayerQuit));
        private static readonly MethodInfo GetBowPrepare = typeof(IPacketLib).GetProperty(nameof(IPacketLib.BowPrepare)).GetMethod;
        private static readonly GameClient Client = new(null) { ClientState = GameClient.eClientState.Playing };

        [Test]
        public async Task Perceive_ShouldSendOnePerceptPerCall_InCallOrder_WithSequenceAndTime()
        {
            FakeMind mind = new();
            MindLink link = mind.Link;

            link.Perceive(Client,SendUpdatePoints, []);
            link.Perceive(Client,SendPlayerQuit, [false]);
            link.Close();
            await link.Completion;

            PerceptMessage[] percepts = mind.Sent.Cast<PerceptMessage>().ToArray();
            Assert.That(percepts.Select(static message => message.Percept), Is.EqualTo(new Percept[] { new BarePercept("SendUpdatePoints"), new BarePercept("SendPlayerQuit") }));
            Assert.That(percepts.Select(static message => message.Sequence), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(percepts[0].ElapsedMs, Is.GreaterThanOrEqualTo(0));
            Assert.That(percepts[1].ElapsedMs, Is.GreaterThanOrEqualTo(percepts[0].ElapsedMs));
        }

        [Test]
        public async Task Perceive_ShouldSendNothing_ForAPropertyRead()
        {
            FakeMind mind = new();

            mind.Link.Perceive(Client,GetBowPrepare, []);
            mind.Link.Close();
            await mind.Link.Completion;

            Assert.That(mind.Sent, Is.Empty);
        }

        [Test]
        public async Task Send_ShouldNotWaitForTheMind_AndSendOffTheCallersThread()
        {
            int callingThread = Environment.CurrentManagedThreadId;
            using ManualResetEventSlim gate = new();
            FakeMind mind = new(gate);

            for (int i = 0; i < 100; i++)
                mind.Link.Perceive(Client,SendUpdatePoints, []);

            Assert.That(mind.Sent, Is.Empty);

            gate.Set();
            mind.Link.Close();
            await mind.Link.Completion;

            Assert.That(mind.Sent.Cast<PerceptMessage>().Select(static message => message.Sequence), Is.EqualTo(Enumerable.Range(1, 100).Select(static i => (long) i)));
            Assert.That(mind.Threads, Does.Not.Contain(callingThread));
        }

        [Test]
        public async Task Close_ShouldCloseTheConnection_AfterEverythingQueuedIsSent()
        {
            FakeMind mind = new();

            mind.Link.Send(new EnteredMessage());
            mind.Link.Close();
            mind.Link.Send(new EnteredMessage());
            await mind.Link.Completion;

            Assert.That(mind.Sent, Has.Count.EqualTo(1));
            Assert.That(mind.ClosedAfter, Is.EqualTo(1));
        }

        [Test]
        public async Task Refuse_ShouldSendTheReason_ThenCloseTheConnection()
        {
            FakeMind mind = new();

            mind.Link.Refuse("No Character is named 'Nobody'.");
            await mind.Link.Completion;

            Assert.That(mind.Sent, Is.EqualTo(new ServerMessage[] { new RefusedMessage("No Character is named 'Nobody'.", false) }));
            Assert.That(mind.ClosedAfter, Is.EqualTo(1));
        }

        [Test]
        public async Task Refuse_ShouldSayWhetherToRetry()
        {
            FakeMind mind = new();

            mind.Link.Refuse("Greeen is linkdead.", retry: true);
            await mind.Link.Completion;

            Assert.That(mind.Sent, Is.EqualTo(new ServerMessage[] { new RefusedMessage("Greeen is linkdead.", true) }));
        }

        [Test]
        public async Task Replace_ShouldTellTheMind_ThenCloseTheConnection_AfterWhatWasQueuedBefore()
        {
            FakeMind mind = new();

            mind.Link.Send(new EnteredMessage());
            mind.Link.Replace();
            mind.Link.Send(new EnteredMessage());
            await mind.Link.Completion;

            Assert.That(mind.Sent, Is.EqualTo(new ServerMessage[] { new EnteredMessage(), new ReplacedMessage() }));
            Assert.That(mind.ClosedAfter, Is.EqualTo(2));
            Assert.That(mind.Link.IsReplaced, Is.True);
        }

        [Test]
        public async Task OnLost_ShouldStopSending_AndNotCloseTheConnection()
        {
            FakeMind mind = new();

            Assert.That(mind.Link.IsConnected, Is.True);

            mind.Link.OnLost();
            mind.Link.Send(new EnteredMessage());
            await mind.Link.Completion;

            Assert.That(mind.Link.IsConnected, Is.False);
            Assert.That(mind.Sent, Is.Empty);
            Assert.That(mind.ClosedAfter, Is.Null);
        }

        [Test]
        public async Task Send_ShouldNotThrow_AndCountTheMindAsLost_WhenSendingFails()
        {
            FakeMind mind = new(failSends: true);

            Assert.DoesNotThrow(() => mind.Link.Send(new EnteredMessage()));
            await mind.Link.Completion;

            Assert.That(mind.Link.IsConnected, Is.False);
            Assert.DoesNotThrow(() => mind.Link.Send(new EnteredMessage()));
        }

        // Stands in for the WebSocket. Holds every send until the gate opens, like a Mind that stopped reading, and remembers which threads sent.
        private sealed class FakeMind
        {
            private readonly ManualResetEventSlim _gate;
            private readonly bool _failSends;
            private readonly List<ServerMessage> _sent = [];

            public FakeMind(ManualResetEventSlim gate = null, bool failSends = false)
            {
                _gate = gate;
                _failSends = failSends;
                Link = new(Send, Close);
            }

            public MindLink Link { get; }
            public ConcurrentBag<int> Threads { get; } = [];

            // How many messages had been sent when the connection was closed, or null if it wasn't.
            public int? ClosedAfter { get; private set; }

            public IReadOnlyList<ServerMessage> Sent
            {
                get
                {
                    lock (_sent)
                    {
                        return _sent.ToList();
                    }
                }
            }

            private Task Send(ServerMessage message)
            {
                _gate?.Wait(TimeSpan.FromSeconds(10));
                Threads.Add(Environment.CurrentManagedThreadId);

                if (_failSends)
                    throw new InvalidOperationException("The connection is gone.");

                lock (_sent)
                {
                    _sent.Add(message);
                }

                return Task.CompletedTask;
            }

            private Task Close()
            {
                ClosedAfter = Sent.Count;
                return Task.CompletedTask;
            }
        }
    }
}
