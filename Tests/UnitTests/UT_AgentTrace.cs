using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DOL.Database;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// The trace's content changes whenever the server changes what it sends, so these tests check how calls are recorded, not which calls a session makes or their exact formatting.
    /// </summary>
    [TestFixture]
    public class UT_AgentTrace
    {
        private static readonly MethodInfo SendMessage = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendMessage));
        private static readonly MethodInfo SendPlayerQuit = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendPlayerQuit));
        private static readonly MethodInfo SendInventoryItemsUpdate = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendInventoryItemsUpdate), [typeof(List<DbInventoryItem>)]);

        [Test]
        public async Task Record_ShouldWriteOneLinePerCall_InOrder()
        {
            StringWriter writer = new();
            AgentTrace trace = new("Greeen", () => writer);

            trace.Record(GameClient.eClientState.Playing, SendMessage, ["Welcome", eChatType.CT_System, eChatLoc.CL_SystemWindow]);
            trace.Record(GameClient.eClientState.Playing, SendPlayerQuit, [false]);
            trace.Dispose();
            await trace.Completion;

            string[] calls = Calls(writer);
            Assert.That(calls, Has.Length.EqualTo(2));
            Assert.That(Field(calls[0], 0), Is.EqualTo("1"));
            Assert.That(Field(calls[0], 2), Is.EqualTo("Playing"));
            Assert.That(Field(calls[0], 3), Does.StartWith("SendMessage(").And.Contain("\"Welcome\"").And.Contain("CT_System").And.Contain("CL_SystemWindow"));
            Assert.That(Field(calls[1], 0), Is.EqualTo("2"));
            Assert.That(Field(calls[1], 3), Does.StartWith("SendPlayerQuit(").And.Contain("False"));
            Assert.That(long.Parse(Field(calls[1], 1)), Is.GreaterThanOrEqualTo(long.Parse(Field(calls[0], 1))));
        }

        [Test]
        public async Task Record_ShouldKeepEachCallOnOneLine()
        {
            StringWriter writer = new();
            AgentTrace trace = new("Greeen", () => writer);

            trace.Record(GameClient.eClientState.Playing, SendMessage, ["Line one\r\nline\ttwo", eChatType.CT_System, eChatLoc.CL_SystemWindow]);
            trace.Dispose();
            await trace.Completion;

            string[] calls = Calls(writer);
            Assert.That(calls, Has.Length.EqualTo(1));
            Assert.That(Field(calls[0], 3), Does.Contain("\"Line one\\r\\nline\\ttwo\""));
        }

        [Test]
        public async Task Record_ShouldNameObjectsAndListCollectionItems()
        {
            StringWriter writer = new();
            AgentTrace trace = new("Greeen", () => writer);
            List<DbInventoryItem> items = [new() { Name = "Sword" }, new() { Name = "Shield" }];

            trace.Record(GameClient.eClientState.Playing, SendInventoryItemsUpdate, [items]);
            trace.Record(GameClient.eClientState.Playing, SendMessage, [null, eChatType.CT_System, eChatLoc.CL_SystemWindow]);
            trace.Dispose();
            await trace.Completion;

            string[] calls = Calls(writer);
            Assert.That(Field(calls[0], 3), Does.StartWith("SendInventoryItemsUpdate(").And.Contain("\"Sword\"").And.Contain("\"Shield\""));
            Assert.That(Field(calls[1], 3), Does.StartWith("SendMessage(").And.Contain("null"));
        }

        [Test]
        public async Task Record_ShouldNotWaitForTheWriter()
        {
            int recordingThread = Environment.CurrentManagedThreadId;
            using ManualResetEventSlim gate = new();
            GatedWriter writer = new(gate);
            AgentTrace trace = new("Greeen", () => writer);

            for (int i = 0; i < 100; i++)
                trace.Record(GameClient.eClientState.Playing, SendPlayerQuit, [false]);

            Assert.That(writer.Written.ToString(), Is.Empty);

            gate.Set();
            trace.Dispose();
            await trace.Completion;

            Assert.That(Calls(writer.Written), Has.Length.EqualTo(100));
            Assert.That(writer.Threads, Does.Not.Contain(recordingThread));
        }

        [Test]
        public async Task Record_ShouldBeIgnored_AfterTheTraceIsDisposed()
        {
            StringWriter writer = new();
            AgentTrace trace = new("Greeen", () => writer);
            trace.Dispose();

            Assert.DoesNotThrow(() => trace.Record(GameClient.eClientState.Playing, SendPlayerQuit, [false]));
            await trace.Completion;
            Assert.That(Calls(writer), Is.Empty);
        }

        [Test]
        public async Task Record_ShouldNotThrow_WhenTheWriterCannotBeOpened()
        {
            AgentTrace trace = new("Greeen", () => throw new IOException("disk full"));

            Assert.DoesNotThrow(() => trace.Record(GameClient.eClientState.Playing, SendPlayerQuit, [false]));
            trace.Dispose();
            await trace.Completion;
        }

        [Test]
        public async Task Record_ShouldOnlyWriteToItsOwnTrace_WhenSeveralAgentsAreTraced()
        {
            StringWriter firstWriter = new();
            StringWriter secondWriter = new();
            AgentTrace first = new("Greeen", () => firstWriter);
            AgentTrace second = new("Bluuue", () => secondWriter);

            for (int i = 0; i < 10; i++)
            {
                first.Record(GameClient.eClientState.Playing, SendPlayerQuit, [false]);
                second.Record(GameClient.eClientState.Playing, SendPlayerQuit, [true]);
            }

            first.Dispose();
            second.Dispose();
            await Task.WhenAll(first.Completion, second.Completion);

            Assert.That(Calls(firstWriter), Has.Length.EqualTo(10));
            Assert.That(Calls(firstWriter).Select(static call => Field(call, 3)), Is.All.Contain("False"));
            Assert.That(Calls(secondWriter), Has.Length.EqualTo(10));
            Assert.That(Calls(secondWriter).Select(static call => Field(call, 3)), Is.All.Contain("True"));
            Assert.That(firstWriter.ToString(), Does.Contain("Greeen"));
            Assert.That(secondWriter.ToString(), Does.Contain("Bluuue"));
        }

        private static string[] Calls(StringWriter writer)
        {
            return writer.ToString().Split(writer.NewLine).Where(static line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith(AgentTrace.COMMENT_PREFIX)).ToArray();
        }

        private static string Field(string call, int index)
        {
            return call.Split('\t')[index];
        }

        // Holds every write until the gate opens, like a stalled disk, and remembers which threads wrote.
        private sealed class GatedWriter(ManualResetEventSlim gate) : TextWriter
        {
            public StringWriter Written { get; } = new();
            public HashSet<int> Threads { get; } = [];

            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                gate.Wait(TimeSpan.FromSeconds(10));
                Threads.Add(Environment.CurrentManagedThreadId);
                Written.Write(value);
            }
        }
    }
}
