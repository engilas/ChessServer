#define UseOptions // or NoOptions or UseOptionsAO
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;
using Types;

namespace WebSocketSampleApp
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.AddConsole()
                    .AddDebug()
                    .AddFilter<ConsoleLoggerProvider>(category: null, level: LogLevel.Information)
                    .AddFilter<DebugLoggerProvider>(category: null, level: LogLevel.Information);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

#if NoOptions
            #region UseWebSockets
            app.UseWebSockets();
            #endregion
#endif
#if UseOptions
            #region UseWebSocketsOptions
            var webSocketOptions = new WebSocketOptions() 
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };

            app.UseWebSockets(webSocketOptions);
            #endregion
#endif

#if UseOptionsAO
            #region UseWebSocketsOptionsAO
            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };
            webSocketOptions.AllowedOrigins.Add("https://client.com");
            webSocketOptions.AllowedOrigins.Add("https://www.client.com");

            app.UseWebSockets(webSocketOptions);
            #endregion
#endif

            #region AcceptWebSocket
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/ws")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        await Echo(context, webSocket, app.ApplicationServices.GetService<ILogger<Startup>>());
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }

            });
            #endregion
            app.UseFileServer();
        }

        private int _total = 0;
        private static MatchManager.Matcher _matcher = MatchManager.createMatcher();

        #region Echo
        private async Task Echo(HttpContext context, WebSocket webSocket, ILogger logger)
        {
            Channel.ClientState state = Channel.ClientState.New;
            var queue = new ConcurrentQueue<string>();
            var sem = new SemaphoreSlim(0);
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            var _ = Task.Run(async () =>
            {
                //var xbuffer = new byte[1024 * 4];
                while (!ct.IsCancellationRequested)
                {
                    //queue.
                    await sem.WaitAsync(ct);
                    queue.TryDequeue(out var msg);
                    var bytes = Encoding.UTF8.GetBytes(msg);
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length), WebSocketMessageType.Text,
                        true, CancellationToken.None);
                }
            });

            void WriteMsg(string msg)
            {
                queue.Enqueue(msg);
                sem.Release();
            }

            //async Task WriteMsgAsync(string msg)
            //{
            //    queue.Enqueue(msg);
            //    sem.Release();
            //}

            //var sem = new SemaphoreSlim(1);
            //async Task writeMsg(string msg, CancellationToken ct)
            //{
            //    await sem.WaitAsync();
            //    try
            //    {

            //    }
            //}

            var channel =
                ClientChannelAdapter.getClientChannel(context.Connection.Id, x =>
                    {
                        var msg = Serializer.serializeNotify(x);
                        WriteMsg(msg);
                    }, x => state = x,
                    () => state);
            //var channel = new Channel.ClientChannel(context.Connection.Id, x => 1, x => 3, qq => Channel.ClientState.New);

            logger.LogWarning("Connected. Total: " + Interlocked.Increment(ref _total));
            //var buffer = new byte[1024 * 4];
            //WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            WebSocketReceiveResult result = null;
            //Task.Run(async () =>
            //{
            //    await Task.Delay(3000);
            //    await webSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "EQea",
            //            CancellationToken.None);
            //    await Task.Delay(3000);
            //});
            do
            {
                var (result2, bytes) = await ReceiveFullMessage(webSocket, CancellationToken.None);
                result = result2;
                if (result.CloseStatus.HasValue) break;
                var input = Encoding.UTF8.GetString(bytes);
                logger.LogInformation(input);
                try
                {
                    var parsed = Serializer.deserializeClientMessage(Encoding.UTF8.GetString(bytes));
                    var response = CommandProcessor.processCommand(_matcher, channel, parsed.Request);
                    var serialized = Serializer.serializeResponse(parsed.MessageId, response);
                    logger.LogInformation(serialized);
                    WriteMsg(serialized);
                }
                catch
                {
                    logger.LogError("Error processing command");
                }

                //await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType,
                //    result.EndOfMessage, CancellationToken.None);

                //result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            } while (!result.CloseStatus.HasValue);

            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            logger.LogWarning("Disconnected. Total: " + Interlocked.Decrement(ref _total));
        }
        #endregion
        //

        private static async Task<(WebSocketReceiveResult, byte[])> ReceiveFullMessage(WebSocket socket, CancellationToken cancelToken)
        {
            WebSocketReceiveResult response;
            var message = new List<byte>();

            var buffer = new byte[4096];
            do
            {
                response = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancelToken);

                if (response.CloseStatus.HasValue) return (response, null);

                message.AddRange(new ArraySegment<byte>(buffer, 0, response.Count));
            }
            while (!response.EndOfMessage);

            return (response, message.ToArray());
        }
    }
}
