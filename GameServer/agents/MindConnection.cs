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
    /// One Mind's WebSocket, from its hello until it closes. Its first message must be a hello, which logs the Agent's Character in through the Harness.
    /// The Mind's connection is the Agent's network connection (ADR 0005): once this ends, however it ends, the Harness client finds its <see cref="MindLink"/> lost.
    /// The handshake and Primitives run on the game loop. Nothing here waits on it while holding anything the game loop needs.
    /// </summary>
    public sealed class MindConnection
    {
        private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        // In bytes. Longer messages are read as malformed.
        private const int MAX_MESSAGE_SIZE = 64 * 1024;

        private readonly WebSocket _socket;
        private readonly CancellationToken _stopping;
        private readonly MindLink _link;
        private readonly byte[] _buffer = new byte[4096];
        private bool _greeted;
        // Set once the login checks have passed. Only the receive loop touches it.
        private HarnessClient _client;
        // Who the Mind is for, in the server log.
        private string _name = "A Mind";

        private MindConnection(WebSocket socket, CancellationToken stopping)
        {
            _socket = socket;
            _stopping = stopping;
            _link = new(Send, Close);
        }

        // Completes once the connection has ended. `stopping` ends it when the server stops.
        public static Task Run(WebSocket socket, CancellationToken stopping)
        {
            return new MindConnection(socket, stopping).Run();
        }

        private async Task Run()
        {
            try
            {
                while (await Receive() is byte[] message)
                    await Handle(MindProtocol.Read(message));
            }
            catch (Exception e) when (e is WebSocketException or OperationCanceledException)
            {
                // The connection broke, missed its keep-alive, or the server is stopping.
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error($"{_name}'s connection failed", e);
            }
            finally
            {
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

                if (_client != null && log.IsInfoEnabled)
                    log.Info($"{_name}'s Mind has disconnected.");
            }
        }

        // A message that can't be read is null.
        private async Task Handle(MindMessage message)
        {
            if (!_greeted)
            {
                _greeted = true;
                await Greet(message as HelloMessage);
                return;
            }

            // Refused, so the connection is closing.
            if (_client == null)
                return;

            switch (message)
            {
                case PrimitiveMessage primitive:
                {
                    Dispatch(primitive);
                    return;
                }
                default:
                {
                    if (log.IsDebugEnabled)
                        log.Debug($"Ignored a message from {_name}'s Mind that isn't a Primitive.");

                    return;
                }
            }
        }

        private async Task Greet(HelloMessage hello)
        {
            if (hello == null)
            {
                Refuse("The first message must be a hello with the token and the Agent's Character.");
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
            TaskCompletionSource<(string Refusal, HarnessClient Client)> login = new(TaskCreationOptions.RunContinuationsAsynchronously);

            // On the game loop, as `/agent` commands are run.
            ClientService.Instance.Post(static state =>
            {
                try
                {
                    string refusal = AgentLogin.TryStart(state.character, state.link, out HarnessClient client, out _);
                    state.login.SetResult((refusal, client));
                }
                catch (Exception e)
                {
                    state.login.SetException(e);
                }
            }, (character: hello.Character, link: _link, login));

            string refusal;
            HarnessClient client;

            try
            {
                (refusal, client) = await login.Task.WaitAsync(_stopping);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                if (log.IsErrorEnabled)
                    log.Error($"Logging {hello.Character} in for its Mind failed", e);

                Refuse($"The server failed to log {hello.Character} in: {e.Message}");
                return;
            }

            if (refusal != null)
            {
                Refuse(refusal);
                return;
            }

            _client = client;
            _name = client.CharacterName;

            if (log.IsInfoEnabled)
                log.Info($"{_name}'s Mind has connected, and {_name} is entering the world. Tracing to {client.TracePath}.");
        }

        // The result goes out after the Primitive is queued, or not, on the game loop.
        private void Dispatch(PrimitiveMessage message)
        {
            ClientService.Instance.Post(static state =>
            {
                string reason = state.client.Dispatch(state.message.Primitive);
                state.link.Send(new PrimitiveResultMessage(state.message.Id, reason == null, reason));
            }, (client: _client, link: _link, message));
        }

        private void Refuse(string reason)
        {
            if (log.IsInfoEnabled)
                log.Info($"{_name} was refused: {reason}");

            _link.Refuse(reason);
        }

        // Returns null once the Mind has closed the connection. A message that's too long or isn't text comes back empty, which reads as malformed.
        private async Task<byte[]> Receive()
        {
            using MemoryStream message = new();
            bool readable = true;

            while (true)
            {
                WebSocketReceiveResult result = await _socket.ReceiveAsync(_buffer, _stopping);

                if (result.MessageType is WebSocketMessageType.Close)
                    return null;

                if (message.Length + result.Count > MAX_MESSAGE_SIZE || result.MessageType is not WebSocketMessageType.Text)
                    readable = false;
                else
                    message.Write(_buffer, 0, result.Count);

                if (result.EndOfMessage)
                    return readable ? message.ToArray() : [];
            }
        }

        private Task Send(ServerMessage message)
        {
            return _socket.SendAsync(MindProtocol.Write(message), WebSocketMessageType.Text, true, _stopping);
        }

        private Task Close()
        {
            return _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, _stopping);
        }
    }
}
