using DoubaoVoice.WebProxy.Services;
using DoubaoVoice.WebProxy.Models;
using Serilog;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add configuration support
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/doubao-proxy-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddSingleton<DoubaoSessionManager>();
builder.Services.AddSingleton<IDoubaoSessionManager>(sp => sp.GetRequiredService<DoubaoSessionManager>());

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseWebSockets();

// Configure WebSocket endpoint with query parameter support
app.Map("/ws", async context =>
{
    // JSON serialization options for consistent message format
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    if (context.WebSockets.IsWebSocketRequest)
    {
        // Read configuration from query string parameters
        var appId = context.Request.Query["appId"];
        var accessToken = context.Request.Query["accessToken"];
        var serviceUrl = context.Request.Query["serviceUrl"];
        var resourceId = context.Request.Query["resourceId"];
        var sampleRate = context.Request.Query["sampleRate"];
        var bitsPerSample = context.Request.Query["bitsPerSample"];
        var channels = context.Request.Query["channels"];

        // Validate required parameters
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(accessToken))
        {
            Log.Warning("Missing required parameters: appId and accessToken");
            await context.Response.WriteAsync("Missing required parameters: appId and accessToken must be provided in query string");
            context.Response.StatusCode = 400;
            return;
        }

        // Create client config from query parameters
        var clientConfig = new ClientConfigDto
        {
            AppId = appId!,
            AccessToken = accessToken!,
            ServiceUrl = serviceUrl.Count > 0 ? serviceUrl.ToString() : null,
            ResourceId = resourceId.Count > 0 ? resourceId.ToString() : null,
            SampleRate = sampleRate.Count > 0 ? int.Parse(sampleRate.ToString()!) : null,
            BitsPerSample = bitsPerSample.Count > 0 ? int.Parse(bitsPerSample.ToString()!) : null,
            Channels = channels.Count > 0 ? int.Parse(channels.ToString()!) : null
        };

        try
        {
            clientConfig.Validate();
        }
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "Invalid configuration: {Message}", ex.Message);
            await context.Response.WriteAsync($"Invalid configuration: {ex.Message}");
            context.Response.StatusCode = 400;
            return;
        }

        // Create session with client config
        var sessionManager = app.Services.GetRequiredService<IDoubaoSessionManager>();
        var session = sessionManager.CreateSession(context.Connection.Id);
        sessionManager.SetClientConfig(context.Connection.Id, clientConfig);

        Log.Information("WebSocket connection accepted for {ConnectionId} with client config: AppId={AppId}",
            context.Connection.Id, clientConfig.AppId);

        // Accept WebSocket
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        try
        {
            // Send connection accepted status
            var connectedMessage = ControlMessage.CreateStatus("connected", session.SessionId);
            var json = connectedMessage.ToJson();
            await webSocket.SendAsync(System.Text.Encoding.UTF8.GetBytes(json),
                System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);

            Log.Debug("Sent status: connected, SessionId={SessionId}", session.SessionId);

            // Handle messages in a loop
            var buffer = new byte[4096];

            while (!webSocket.CloseStatus.HasValue)
            {
                var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), CancellationToken.None);

                // Check if client sent a close frame
                if (receiveResult.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                {
                    Log.Information("Client sent close frame for {ConnectionId}", context.Connection.Id);
                    break;
                }

                if (receiveResult.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
                {
                    var messageText = System.Text.Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                    Log.Debug("Received: {MessageText}", messageText);

                    // Parse the message to get type and command
                    try
                    {
                        var message = JsonSerializer.Deserialize<JsonDocument>(messageText);
                        var type = message?.RootElement.GetProperty("type").GetString();

                        // Handle control messages
                        if (type == "control")
                        {
                            var command = message?.RootElement.GetProperty("payload").GetProperty("command").GetString();
                            Log.Information("Received control command: {Command}", command);

                            switch (command)
                            {
                                case "StartRecognition":
                                    sessionManager.UpdateSessionState(context.Connection.Id, SessionState.Recognizing);
                                    var recognizingStatus = new
                                    {
                                        type = "status",
                                        messageId = Guid.NewGuid().ToString(),
                                        payload = new
                                        {
                                            status = "recognizing",
                                            sessionId = session.SessionId
                                        }
                                    };
                                    var recognizingJson = JsonSerializer.Serialize(recognizingStatus, jsonOptions);
                                    await webSocket.SendAsync(System.Text.Encoding.UTF8.GetBytes(recognizingJson),
                                        System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                                    Log.Information("Recognition started for session {SessionId}", session.SessionId);
                                    break;

                                case "PauseRecognition":
                                    sessionManager.UpdateSessionState(context.Connection.Id, SessionState.Paused);
                                    var pausedStatus = new
                                    {
                                        type = "status",
                                        messageId = Guid.NewGuid().ToString(),
                                        payload = new
                                        {
                                            status = "paused",
                                            sessionId = session.SessionId
                                        }
                                    };
                                    var pausedJson = JsonSerializer.Serialize(pausedStatus, jsonOptions);
                                    await webSocket.SendAsync(System.Text.Encoding.UTF8.GetBytes(pausedJson),
                                        System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                                    Log.Information("Recognition paused for session {SessionId}", session.SessionId);
                                    break;

                                case "StopRecognition":
                                case "EndRecognition":
                                    sessionManager.UpdateSessionState(context.Connection.Id, SessionState.Closed);
                                    var stoppedStatus = new
                                    {
                                        type = "status",
                                        messageId = Guid.NewGuid().ToString(),
                                        payload = new
                                        {
                                            status = "closed",
                                            sessionId = session.SessionId
                                        }
                                    };
                                    var stoppedJson = JsonSerializer.Serialize(stoppedStatus, jsonOptions);
                                    await webSocket.SendAsync(System.Text.Encoding.UTF8.GetBytes(stoppedJson),
                                        System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                                    Log.Information("Recognition stopped for session {SessionId}", session.SessionId);
                                    break;

                                default:
                                    Log.Warning("Unknown control command: {Command}", command);
                                    break;
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Log.Warning(ex, "Failed to parse message: {MessageText}", messageText);
                    }
                }
                // Handle audio data (binary)
                else if (receiveResult.MessageType == System.Net.WebSockets.WebSocketMessageType.Binary)
                {
                    if (session.State != SessionState.Recognizing)
                    {
                        Log.Warning("Received audio data but session is not in recognizing state");
                    }
                    else
                    {
                        await sessionManager.AddAudioAsync(context.Connection.Id, buffer.Take(receiveResult.Count).ToArray());
                        var audioObj = new
                        {
                            type = "audio",
                            timestamp = DateTime.UtcNow.ToString("O"),
                            durationMs = 200
                        };
                        var audioJson = JsonSerializer.Serialize(audioObj, jsonOptions);
                        await webSocket.SendAsync(System.Text.Encoding.UTF8.GetBytes(audioJson),
                            System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                        Log.Debug("Forwarded {Count} bytes of audio data", receiveResult.Count);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling WebSocket connection for {ConnectionId}", context.Connection.Id);
        }
        finally
        {
            // Always remove the session
            sessionManager.RemoveSession(context.Connection.Id);

            // Close WebSocket if still open
            if (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
            {
                await webSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// Serve static files
app.UseStaticFiles();

app.MapGet("/", () => "DoubaoVoice.WebProxy is running. Connect to /ws?appId=xxx&accessToken=xxx for WebSocket API or see /index.html for testing.");

try
{
    Log.Information("Starting DoubaoVoice.WebProxy");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
