using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DOL.GS.Agents;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// The refusals a Mind gets before its login reaches the game loop, over a real WebSocket on a loopback socket.
    /// A hello that gets past them needs a database and a running game loop, so it's left to the manual acceptance test.
    /// </summary>
    [TestFixture]
    public class UT_MindConnection
    {
        private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(10);

        private string _token;

        [SetUp]
        public void SetUp()
        {
            _token = AgentProperties.MIND_TOKEN;
        }

        [TearDown]
        public void TearDown()
        {
            AgentProperties.MIND_TOKEN = _token;
        }

        [Test]
        public async Task Hello_ShouldBeRefused_AndTheConnectionClosed_WhenNoTokenIsConfigured()
        {
            AgentProperties.MIND_TOKEN = string.Empty;

            RefusedMessage refused = await RefusalFor("""{"type":"hello","token":"","character":"Greeen"}""");

            Assert.That(refused.Reason, Does.Contain("no Mind token configured").And.Contain("agent_mind_token"));
            Assert.That(refused.Retry, Is.False);
        }

        [Test]
        public async Task Hello_ShouldBeRefused_AndTheConnectionClosed_WhenTheTokenIsWrong()
        {
            AgentProperties.MIND_TOKEN = "secret";

            RefusedMessage refused = await RefusalFor("""{"type":"hello","token":"guess","character":"Greeen"}""");

            Assert.That(refused.Reason, Does.Contain("token is wrong"));
        }

        [Test]
        public async Task FirstMessage_ShouldBeRefused_AndTheConnectionClosed_WhenItIsNotAHello()
        {
            AgentProperties.MIND_TOKEN = "secret";

            RefusedMessage refused = await RefusalFor("""{"type":"primitive","id":1,"primitive":{"name":"quit"}}""");

            Assert.That(refused.Reason, Does.Contain("first message must be a hello"));
        }

        [Test]
        public async Task Run_ShouldEnd_WhenTheMindClosesBeforeItsHello()
        {
            using SocketPair pair = await SocketPair.Open();
            Task connection = MindConnection.Run(pair.Server, CancellationToken.None);

            await pair.Mind.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).WaitAsync(TimeLimit);
            await connection.WaitAsync(TimeLimit);

            Assert.That(pair.Mind.State, Is.EqualTo(WebSocketState.Closed));
        }

        // Sends the Mind's first message and returns the refusal, after checking that the server then closed the connection normally.
        private static async Task<RefusedMessage> RefusalFor(string firstMessage)
        {
            using SocketPair pair = await SocketPair.Open();
            Task connection = MindConnection.Run(pair.Server, CancellationToken.None);

            await pair.Mind.SendAsync(Encoding.UTF8.GetBytes(firstMessage), WebSocketMessageType.Text, true, CancellationToken.None);

            byte[] buffer = new byte[4096];
            WebSocketReceiveResult result = await pair.Mind.ReceiveAsync(buffer, CancellationToken.None).WaitAsync(TimeLimit);
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Text));
            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Assert.That(json, Does.StartWith("""{"type":"refused","""));

            result = await pair.Mind.ReceiveAsync(buffer, CancellationToken.None).WaitAsync(TimeLimit);
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
            Assert.That(result.CloseStatus, Is.EqualTo(WebSocketCloseStatus.NormalClosure));

            await pair.Mind.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            await connection.WaitAsync(TimeLimit);

            return JsonSerializer.Deserialize<RefusedMessage>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        // A server and a Mind end of one WebSocket, over a loopback TCP connection.
        private sealed class SocketPair : IDisposable
        {
            private readonly TcpClient _serverTcp;
            private readonly TcpClient _mindTcp;

            private SocketPair(TcpClient serverTcp, TcpClient mindTcp)
            {
                _serverTcp = serverTcp;
                _mindTcp = mindTcp;
                Server = WebSocket.CreateFromStream(serverTcp.GetStream(), new WebSocketCreationOptions { IsServer = true });
                Mind = WebSocket.CreateFromStream(mindTcp.GetStream(), new WebSocketCreationOptions { IsServer = false });
            }

            public WebSocket Server { get; }
            public WebSocket Mind { get; }

            public static async Task<SocketPair> Open()
            {
                TcpListener listener = new(IPAddress.Loopback, 0);
                listener.Start();

                try
                {
                    TcpClient mindTcp = new();
                    Task<TcpClient> accept = listener.AcceptTcpClientAsync();
                    await mindTcp.ConnectAsync(IPAddress.Loopback, ((IPEndPoint) listener.LocalEndpoint).Port);
                    return new(await accept, mindTcp);
                }
                finally
                {
                    listener.Stop();
                }
            }

            public void Dispose()
            {
                Server.Dispose();
                Mind.Dispose();
                _serverTcp.Dispose();
                _mindTcp.Dispose();
            }
        }
    }
}
