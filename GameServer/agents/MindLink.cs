using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DOL.Logging;

namespace DOL.GS.Agents
{
    /// <summary>
    /// One Mind's connection as the Harness sees it. It's the Agent's network connection (ADR 0005): the Harness client watches <see cref="IsConnected"/> as a real client's socket would be watched.
    /// What the server tells the Mind is queued here and sent off the caller's thread, in order, by a sender that drains the queue, as the trace does. Queueing never waits, so a stalled Mind can't hold up the game loop.
    /// </summary>
    public sealed class MindLink
    {
        private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Channel<ServerMessage> _outbox = Channel.CreateUnbounded<ServerMessage>(new() { SingleReader = true });
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private long _perceptSequence;
        private volatile bool _connected = true;

        // `send` sends one message and `close` closes the connection normally. Both are called off the caller's thread, one at a time.
        public MindLink(Func<ServerMessage, Task> send, Func<Task> close)
        {
            Completion = Task.Run(() => SendUntilClosed(send, close));
        }

        // False once the connection is gone, however that came about. A connection the server closed counts as gone too, once the Mind has answered the close.
        public bool IsConnected => _connected;

        // Completes once the sender has stopped: everything queued was sent and the connection closed, or the connection was lost.
        public Task Completion { get; }

        // Never waits. Ignored once the connection is closing or gone.
        public void Send(ServerMessage message)
        {
            _outbox.Writer.TryWrite(message);
        }

        // One call the server made on the Agent's packet library, which belongs to `client`. Called on the game loop.
        public void Perceive(GameClient client, MethodInfo method, object[] args)
        {
            Percept percept = PerceptProjection.Project(client, method, args);

            if (percept != null)
                Send(new PerceptMessage(Interlocked.Increment(ref _perceptSequence), _stopwatch.ElapsedMilliseconds, percept));
        }

        // True once another Mind has taken over the Agent's connection from this one.
        public bool IsReplaced { get; private set; }

        // `reason` is a whole sentence for the Human, and `retry` says whether the same hello can succeed later.
        public void Refuse(string reason, bool retry = false)
        {
            Send(new RefusedMessage(reason, retry));
            Close();
        }

        // Another Mind has taken over the Agent's connection. Tells this one, after whatever was queued before, and closes its connection.
        public void Replace()
        {
            IsReplaced = true;
            Send(new ReplacedMessage());
            Close();
        }

        // Closes the connection normally once everything queued is sent.
        public void Close()
        {
            _outbox.Writer.TryComplete();
        }

        // The connection is gone. Nothing more is sent.
        public void OnLost()
        {
            _connected = false;
            _outbox.Writer.TryComplete();
        }

        private async Task SendUntilClosed(Func<ServerMessage, Task> send, Func<Task> close)
        {
            try
            {
                ChannelReader<ServerMessage> reader = _outbox.Reader;

                while (await reader.WaitToReadAsync())
                {
                    while (reader.TryRead(out ServerMessage message))
                    {
                        if (!_connected)
                            return;

                        await send(message);
                    }
                }

                if (_connected)
                    await close();
            }
            catch (Exception e)
            {
                // A connection that can't be written to any more is gone.
                if (_connected && log.IsInfoEnabled)
                    log.Info($"Sending to a Mind failed, so its connection counts as lost: {e.Message}");

                OnLost();
            }
        }
    }
}
