using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DOL.GS.Agents;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// The real listener, started and stopped as the server does, without a game loop behind it. Only refusals that come before the login checks can be seen.
    /// </summary>
    [TestFixture]
    public class UT_MindEndpoint
    {
        private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(10);

        private int _port;
        private string _token;

        [SetUp]
        public void SetUp()
        {
            _port = AgentProperties.MIND_PORT;
            _token = AgentProperties.MIND_TOKEN;
            AgentProperties.MIND_PORT = FreePort();
            AgentProperties.MIND_TOKEN = string.Empty;
            MindEndpoint.OnServerStarted(null, null, EventArgs.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            MindEndpoint.OnServerStopped(null, null, EventArgs.Empty);
            AgentProperties.MIND_PORT = _port;
            AgentProperties.MIND_TOKEN = _token;
        }

        [Test]
        public async Task Mind_ShouldBeRefused_AndTheConnectionClosed_WhenNoTokenIsConfigured()
        {
            using ClientWebSocket mind = new();
            await mind.ConnectAsync(new Uri($"ws://localhost:{AgentProperties.MIND_PORT}{MindEndpoint.PATH}"), CancellationToken.None).WaitAsync(TimeLimit);
            await mind.SendAsync(Encoding.UTF8.GetBytes("""{"type":"hello","token":"","character":"Greeen"}"""), WebSocketMessageType.Text, true, CancellationToken.None);

            byte[] buffer = new byte[4096];
            WebSocketReceiveResult result = await mind.ReceiveAsync(buffer, CancellationToken.None).WaitAsync(TimeLimit);
            Assert.That(Encoding.UTF8.GetString(buffer, 0, result.Count), Does.StartWith("""{"type":"refused","reason":"The server has no Mind token configured"""));

            result = await mind.ReceiveAsync(buffer, CancellationToken.None).WaitAsync(TimeLimit);
            Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
            Assert.That(result.CloseStatus, Is.EqualTo(WebSocketCloseStatus.NormalClosure));
        }

        [Test]
        public async Task RequestThatIsNotAWebSocket_ShouldBeABadRequest()
        {
            using HttpClient http = new();

            HttpResponseMessage response = await http.GetAsync($"http://localhost:{AgentProperties.MIND_PORT}{MindEndpoint.PATH}").WaitAsync(TimeLimit);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        private static int FreePort()
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
