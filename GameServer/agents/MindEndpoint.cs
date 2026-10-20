using System;
using System.Net.WebSockets;
using System.Reflection;
using System.Threading;
using DOL.Events;
using DOL.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DOL.GS.Agents
{
    /// <summary>
    /// Where Agents' Minds connect: a WebSocket endpoint on its own small listener, bound to localhost only, that starts and stops with the server.
    /// Separate from the web API host, which may be switched off.
    /// The listener pings every Mind and drops one that doesn't answer in time. Bun answers those pings itself, so a busy Mind stays connected.
    /// </summary>
    public static class MindEndpoint
    {
        private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        public const string PATH = "/mind";

        // In milliseconds. How often the listener pings a Mind, and how long it waits for the answer before the Mind counts as lost.
        public const int KEEP_ALIVE_INTERVAL = 10000;
        public const int KEEP_ALIVE_TIMEOUT = 20000;

        // In milliseconds. How long stopping waits for connections to end before it drops them.
        private const int STOP_TIME_LIMIT = 2000;

        private static readonly Lock _lock = new();
        private static WebApplication _listener;

        [GameServerStartedEvent]
        public static void OnServerStarted(DOLEvent e, object sender, EventArgs args)
        {
            int port = AgentProperties.MIND_PORT;

            lock (_lock)
            {
                try
                {
                    WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
                    builder.Logging.ClearProviders();
                    builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(port));
                    WebApplication listener = builder.Build();

                    listener.UseWebSockets(new WebSocketOptions
                    {
                        KeepAliveInterval = TimeSpan.FromMilliseconds(KEEP_ALIVE_INTERVAL),
                        KeepAliveTimeout = TimeSpan.FromMilliseconds(KEEP_ALIVE_TIMEOUT)
                    });

                    listener.Map(PATH, async context =>
                    {
                        if (!context.WebSockets.IsWebSocketRequest)
                        {
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            return;
                        }

                        using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
                        await MindConnection.Run(socket, listener.Lifetime.ApplicationStopping);
                    });

                    listener.StartAsync().GetAwaiter().GetResult();
                    _listener = listener;

                    if (log.IsInfoEnabled)
                        log.Info($"Minds can connect at ws://localhost:{port}{PATH}{(string.IsNullOrEmpty(AgentProperties.MIND_TOKEN) ? ", but every Mind is refused until the server property agent_mind_token is set" : string.Empty)}.");
                }
                catch (Exception exception)
                {
                    if (log.IsErrorEnabled)
                        log.Error($"The endpoint for Minds couldn't listen on localhost port {port}, so no Agent can log in", exception);
                }
            }
        }

        // Before the server saves its players, as it does for Humans. Minds still connected see their connection drop, as a Human's client does.
        [GameServerStoppedEvent]
        public static void OnServerStopped(DOLEvent e, object sender, EventArgs args)
        {
            lock (_lock)
            {
                if (_listener == null)
                    return;

                try
                {
                    using CancellationTokenSource timeLimit = new(STOP_TIME_LIMIT);
                    _listener.StopAsync(timeLimit.Token).GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    if (log.IsWarnEnabled)
                        log.Warn("The endpoint for Minds didn't stop cleanly", exception);
                }

                _listener = null;
            }
        }
    }
}
