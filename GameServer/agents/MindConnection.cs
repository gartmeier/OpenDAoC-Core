using System;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DOL.Logging;

namespace DOL.GS.Agents
{
    /// <summary>
    /// One Mind's WebSocket, from its hello until it closes. Its first message must be a hello, which logs the Agent's Character in through the Harness, or takes over its connection.
    /// The Mind's connection is the Agent's network connection (ADR 0005): once this ends, however it ends, the Harness client finds its <see cref="MindLink"/> lost.
    /// Anything a Mind sends that the protocol doesn't allow is refused with what's wrong, and the server closes the connection. Before the hello, no Agent is touched.
    /// The handshake and Primitives run on the game loop. Nothing here waits on it while holding anything the game loop needs.
    /// </summary>
    public sealed class MindConnection
    {
        private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        // In milliseconds. How long a Mind has after connecting to send its hello.
        public const int HELLO_TIME_LIMIT = 10000;

        // In milliseconds. How long the server waits for a Mind to answer its close before it drops the connection.
        private const int CLOSE_TIME_LIMIT = 5000;

        // In bytes. Longer messages are refused.
        private const int MAX_MESSAGE_SIZE = 64 * 1024;

        /// <summary>
        /// Logs the hello's Character in, connected to `link`, or takes over its connection. The server's runs <see cref="AgentLogin.TryStart"/> on the game loop.
        /// </summary>
        public delegate Task<LoginOutcome> Login(string character, MindLink link, CancellationToken stopping);

        private readonly WebSocket _socket;
        private readonly CancellationToken _stopping;
        // Cancelling it drops the connection: when the server stops, or when the Mind doesn't answer the server's close in time.
        private readonly CancellationTokenSource _drop;
        private readonly Login _login;
        private readonly TimeSpan _helloTimeLimit;
        private readonly MindLink _link;
        private readonly byte[] _buffer = new byte[4096];
        // 1 once the hello has arrived or its time has run out, whichever came first.
        private int _helloSettled;
        // The server has refused the Mind, and the connection is closing. Whatever else the Mind sends is ignored.
        private volatile bool _closing;
        // Set once the login checks have passed. Only the receive loop touches it.
        private HarnessClient _client;
        // Who the Mind is for, in the server log.
        private string _name = "A Mind";

        private MindConnection(WebSocket socket, CancellationToken stopping, Login login, TimeSpan helloTimeLimit)
        {
            _socket = socket;
            _stopping = stopping;
            _drop = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            _login = login;
            _helloTimeLimit = helloTimeLimit;
            _link = new(Send, Close);
        }

        // Completes once the connection has ended. `stopping` ends it when the server stops.
        public static Task Run(WebSocket socket, CancellationToken stopping)
        {
            return Run(socket, stopping, LogInOnGameLoop, TimeSpan.FromMilliseconds(HELLO_TIME_LIMIT));
        }

        public static Task Run(WebSocket socket, CancellationToken stopping, Login login, TimeSpan helloTimeLimit)
        {
            return new MindConnection(socket, stopping, login, helloTimeLimit).Run();
        }

        private async Task Run()
        {
            Timer helloTimer = new(static state => ((MindConnection) state).OnNoHello(), this, _helloTimeLimit, Timeout.InfiniteTimeSpan);

            try
            {
                while (await Receive() is (var message, var error))
                    await Handle(message, error);
            }
            catch (Exception e) when (e is WebSocketException or OperationCanceledException)
            {
                // The connection broke, missed its keep-alive or its close, or the server is stopping.
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error($"{_name}'s connection failed", e);
            }
            finally
            {
                await helloTimer.DisposeAsync();
                _link.OnLost();
                await _link.Completion;

                // The Mind closed first. Answer its close, as a WebSocket must.
                if (_socket.State is WebSocketState.CloseReceived)
                {
                    try
                    {
                        await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, _stopping);
                    }
                    catch (Exception e) when (e is WebSocketException or OperationCanceledException) { }
                }

                _drop.Dispose();

                if (_client != null && log.IsInfoEnabled)
                    log.Info(_link.IsReplaced ? $"{_name}'s old Mind has disconnected, after a new one took over." : $"{_name}'s Mind has disconnected.");
            }
        }

        // `message` is null if it couldn't be read, and `error` says why.
        private async Task Handle(MindMessage message, string error)
        {
            if (_closing)
                return;

            if (Interlocked.Exchange(ref _helloSettled, 1) == 0)
            {
                await Greet(message, error);
                return;
            }

            // The hello's time ran out first, so the connection is closing.
            if (_client == null)
                return;

            switch (message)
            {
                case PrimitiveMessage { Primitive: not null } primitive:
                {
                    Dispatch(primitive);
                    return;
                }
                case PrimitiveMessage:
                {
                    Refuse("A primitive message has no \"primitive\".");
                    return;
                }
                case HelloMessage:
                {
                    Refuse("The Mind sent a second hello. Only its first message is a hello.");
                    return;
                }
                case null:
                {
                    Refuse($"The server couldn't read a message from the Mind. {error}");
                    return;
                }
                default:
                {
                    Refuse($"The server doesn't take a {message.GetType().Name} from a Mind.");
                    return;
                }
            }
        }

        private async Task Greet(MindMessage message, string error)
        {
            if (message is not HelloMessage hello)
            {
                Refuse($"The first message must be a hello with the token and the Agent's Character. {error ?? $"It was a {message.GetType().Name}."}");
                return;
            }

            string token = AgentProperties.MIND_TOKEN;

            if (string.IsNullOrEmpty(token))
            {
                Refuse("The server has no Mind token configured, so it refuses every Mind. Set the server property agent_mind_token.");
                return;
            }

            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hello.Token ?? string.Empty), Encoding.UTF8.GetBytes(token)))
            {
                Refuse("The token is wrong. It must match the server property agent_mind_token.");
                return;
            }

            if (string.IsNullOrWhiteSpace(hello.Character))
            {
                Refuse("The hello names no Character.");
                return;
            }

            _name = hello.Character;
            LoginOutcome outcome;

            try
            {
                outcome = await _login(hello.Character, _link, _stopping);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                if (log.IsErrorEnabled)
                    log.Error($"Logging {hello.Character} in for its Mind failed", e);

                Refuse($"The server failed to log {hello.Character} in: {e.Message}");
                return;
            }

            switch (outcome)
            {
                case LoginOutcome.Refused refused:
                {
                    Refuse(refused.Reason, refused.Retry);
                    return;
                }
                case LoginOutcome.Entering entering:
                {
                    _client = entering.Client;
                    _name = _client.CharacterName;

                    if (log.IsInfoEnabled)
                        log.Info($"{_name}'s Mind has connected, and {_name} is entering the world. Tracing to {_client.TracePath}.");

                    return;
                }
                case LoginOutcome.TookOver tookOver:
                {
                    _client = tookOver.Client;
                    _name = _client.CharacterName;

                    if (log.IsInfoEnabled)
                        log.Info($"A new Mind has taken over {_name}'s connection, and {_name} stays in the world.");

                    return;
                }
            }
        }

        private void OnNoHello()
        {
            if (Interlocked.Exchange(ref _helloSettled, 1) == 0)
                Refuse($"The Mind sent no hello within {_helloTimeLimit.TotalSeconds:0.#} s of connecting.");
        }

        // The result goes out after the Primitive is queued, or not, on the game loop.
        private void Dispatch(PrimitiveMessage message)
        {
            ClientService.Instance.Post(static state =>
            {
                string reason = state.client.Dispatch(state.message.Primitive, state.link);
                state.link.Send(new PrimitiveResultMessage(state.message.Id, reason == null, reason));
            }, (client: _client, link: _link, message));
        }

        // After the hello, closing the connection makes the Agent's Mind lost, as when the server drops a real client.
        private void Refuse(string reason, bool retry = false)
        {
            _closing = true;

            if (log.IsInfoEnabled)
                log.Info($"{_name} was refused: {reason}");

            _link.Refuse(reason, retry);
        }

        // On the game loop, as `/agent` commands are run.
        private static Task<LoginOutcome> LogInOnGameLoop(string character, MindLink link, CancellationToken stopping)
        {
            TaskCompletionSource<LoginOutcome> login = new(TaskCreationOptions.RunContinuationsAsynchronously);

            ClientService.Instance.Post(static state =>
            {
                try
                {
                    state.login.SetResult(AgentLogin.TryStart(state.character, state.link));
                }
                catch (Exception e)
                {
                    state.login.SetException(e);
                }
            }, (character, link, login));

            return login.Task.WaitAsync(stopping);
        }

        // Returns null once the Mind has closed the connection. Otherwise the message, or null and what's wrong with it.
        private async Task<(MindMessage Message, string Error)?> Receive()
        {
            using MemoryStream message = new();
            string error = null;

            while (true)
            {
                WebSocketReceiveResult result = await _socket.ReceiveAsync(_buffer, _drop.Token);

                if (result.MessageType is WebSocketMessageType.Close)
                    return null;

                if (result.MessageType is not WebSocketMessageType.Text)
                    error = "It isn't text. Every message is JSON in a text frame.";
                else if (message.Length + result.Count > MAX_MESSAGE_SIZE)
                    error = $"It's longer than {MAX_MESSAGE_SIZE} bytes.";
                else if (error == null)
                    message.Write(_buffer, 0, result.Count);

                if (result.EndOfMessage)
                    return error != null ? (null, error) : (MindProtocol.Read(message.GetBuffer().AsSpan(0, (int) message.Length), out error), error);
            }
        }

        private Task Send(ServerMessage message)
        {
            return _socket.SendAsync(MindProtocol.Write(message), WebSocketMessageType.Text, true, _stopping);
        }

        // A Mind that doesn't answer the close in time is dropped.
        private Task Close()
        {
            _drop.CancelAfter(CLOSE_TIME_LIMIT);
            return _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, _stopping);
        }
    }
}
