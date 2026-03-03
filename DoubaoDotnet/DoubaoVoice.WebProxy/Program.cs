using DoubaoVoice.WebProxy.Services;
using DoubaoVoice.WebProxy.Models;
using DoubaoVoice.WebProxy.Handlers;
using Serilog;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using DoubaoVoice.SDK;
using System.Net.WebSockets;
using DoubaoVoice.SDK.Audio;
using System.Buffers.Binary;

var builder = WebApplication.CreateBuilder(args);

// Add configuration support
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Configure Serilog (only read from appsettings.json to avoid duplicate logs)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddSingleton<DoubaoSessionManager>();
builder.Services.AddSingleton<IDoubaoSessionManager>(sp => sp.GetRequiredService<DoubaoSessionManager>());
builder.Services.AddSingleton<DoubaoWebSocketHandler>();

var app = builder.Build();

// Dictionary to store Doubao clients per connection
var doubaoClients = new ConcurrentDictionary<string, (DoubaoVoiceClient client, CancellationTokenSource cts)>();

// Audio recorder for debugging
var audioRecorders = new ConcurrentDictionary<string, AudioRecorder>();

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

        // Log connection details for debugging
        Log.Information("WebSocket connection request details:");
        Log.Information("  Connection ID: {ConnectionId}", context.Connection.Id);
        Log.Information("  App ID: {AppId}", clientConfig.AppId);
        Log.Information("  Service URL: {ServiceUrl}", clientConfig.ServiceUrl ?? "default");
        Log.Information("  Resource ID: {ResourceId}", clientConfig.ResourceId ?? "default");
        Log.Information("  Sample Rate: {SampleRate}Hz", clientConfig.SampleRate ?? 16000);
        Log.Information("  Remote IP: {RemoteIP}", context.Connection.RemoteIpAddress);
        Log.Information("  User Agent: {UserAgent}", context.Request.Headers["User-Agent"].ToString());

        // Create session with client config
        var sessionManager = app.Services.GetRequiredService<IDoubaoSessionManager>();
        var session = sessionManager.CreateSession(context.Connection.Id);
        sessionManager.SetClientConfig(context.Connection.Id, clientConfig);

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
                                    // Connect to Doubao and start recognition
                                    try
                                    {
                                        Log.Information("StartRecognition command received for session {SessionId}", session.SessionId);
                                        Log.Information("Creating DoubaoVoiceClient with config: AppId={AppId}, Did={Did}",
                                            clientConfig.AppId, clientConfig.Did);

                                        var voiceConfig = clientConfig.ToDoubaoVoiceConfig();
                                        Log.Debug("DoubaoVoiceConfig created: ServiceUrl={ServiceUrl}, ResourceId={ResourceId}",
                                            voiceConfig.ServiceUrl, voiceConfig.ResourceId);

                                        var doubaoClient = new DoubaoVoiceClient(voiceConfig);
                                        var receiveCts = new CancellationTokenSource();

                                        // Subscribe to events with detailed logging
                                        doubaoClient.OnResultReceived += async (s, e) =>
                                        {
                                            Log.Debug("Result received from Doubao: Text={Text}, IsFinal={IsFinal}",
                                                e.Result.Text, e.IsFinal);

                                            var resultDto = new RecognitionResultDto
                                            {
                                                Text = e.Result.Text,
                                                Confidence = 1.0f,
                                                Duration = e.Result.AudioDuration,
                                                IsFinal = e.IsFinal,
                                                Utterances = e.Result.Utterances.Select(u => new UtteranceDto
                                                {
                                                    Text = u.Text,
                                                    StartTime = u.StartTime,
                                                    EndTime = u.EndTime,
                                                    Definite = u.Definite,
                                                    Words = u.Words.Select(w => new WordDto
                                                    {
                                                        Text = w.Text,
                                                        StartTime = w.StartTime,
                                                        EndTime = w.EndTime
                                                    }).ToList()
                                                }).ToList()
                                            };

                                            var resultMessage = ControlMessage.CreateResult(resultDto);
                                            await webSocket.SendAsync(System.Text.Encoding.UTF8.GetBytes(resultMessage.ToJson()),
                                                System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                                            Log.Debug("Result sent to WebSocket client");
                                        };

                                        doubaoClient.OnError += (s, e) =>
                                        {
                                            Log.Error(e.Exception, "Doubao error: {Message}", e.ErrorMessage);
                                        };

                                        doubaoClient.OnDisconnected += (s, e) =>
                                        {
                                            Log.Warning("Doubao disconnected: {Reason}", e.Reason);
                                        };

                                        doubaoClient.OnConnected += (s, e) =>
                                        {
                                            Log.Information("Doubao connected successfully");
                                        };

                                        // Create audio recorder for debugging
                                        var audioRecorder = new AudioRecorder(
                                            session.SessionId,
                                            voiceConfig.SampleRate,
                                            voiceConfig.BitsPerSample,
                                            voiceConfig.Channels
                                        );
                                        audioRecorders[context.Connection.Id] = audioRecorder;
                                        Log.Information("Audio recorder created for session {SessionId}", session.SessionId);

                                        // Connect to Doubao
                                        Log.Information("Connecting to Doubao WebSocket at {ServiceUrl}", voiceConfig.ServiceUrl);
                                        await doubaoClient.ConnectAsync();
                                        Log.Information("Doubao WebSocket connection established for session {SessionId}", session.SessionId);

                                        Log.Debug("Sending full client request to Doubao");
                                        await doubaoClient.SendFullClientRequest();
                                        Log.Debug("Full client request sent to Doubao");

                                        // Give the server a moment to process the initial request
                                        // This matches the CLI behavior which waits 500ms before starting audio
                                        await Task.Delay(500);
                                        Log.Debug("Server initialization delay completed");

                                        // Start receiving messages in background with detailed error logging
                                        var receiveTask = Task.Run(async () =>
                                        {
                                            try
                                            {
                                                await doubaoClient.ReceiveMessagesAsync(receiveCts.Token);
                                                Log.Debug("ReceiveMessagesAsync completed normally");
                                            }
                                            catch (Exception ex)
                                            {
                                                Log.Error(ex, "ReceiveMessagesAsync failed for session {SessionId}", session.SessionId);
                                                throw;
                                            }
                                        }, receiveCts.Token);
                                        Log.Debug("Doubao receive task started in background");

                                        // Verify connection is established
                                        var maxWait = TimeSpan.FromSeconds(3);
                                        var startTime = DateTime.UtcNow;
                                        while (!doubaoClient.IsConnected && (DateTime.UtcNow - startTime) < maxWait)
                                        {
                                            await Task.Delay(100);
                                        }

                                        if (!doubaoClient.IsConnected)
                                        {
                                            throw new Exception("Doubao WebSocket not connected after timeout");
                                        }

                                        Log.Information("Doubao connection verified, IsConnected={IsConnected}", doubaoClient.IsConnected);

                                        // Store client for later cleanup
                                        doubaoClients[context.Connection.Id] = (doubaoClient, receiveCts);
                                        sessionManager.UpdateSessionState(context.Connection.Id, SessionState.Recognizing);

                                        var recognizingStatus = ControlMessage.CreateStatus("recognizing", session.SessionId);
                                        var recognizingJson = recognizingStatus.ToJson();
                                        await webSocket.SendAsync(System.Text.Encoding.UTF8.GetBytes(recognizingJson),
                                            System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);

                                        Log.Information("Recognition started for session {SessionId}", session.SessionId);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, "Failed to start recognition for session {SessionId}", session.SessionId);
                                        var errorMsg = ControlMessage.CreateError("Failed to start recognition: " + ex.Message);
                                        await webSocket.SendAsync(System.Text.Encoding.UTF8.GetBytes(errorMsg.ToJson()),
                                            System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                                    }
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
                                    // Send final segment marker to Doubao before disconnecting
                                    if (doubaoClients.TryGetValue(context.Connection.Id, out var clientData))
                                    {
                                        try
                                        {
                                            Log.Debug("Sending final segment marker to Doubao for session {SessionId}", session.SessionId);
                                            await clientData.client.SendAudioSegment(Array.Empty<byte>(), true);
                                            Log.Debug("Final segment marker sent");

                                            // Wait briefly for server to process
                                            await Task.Delay(100);
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Warning(ex, "Failed to send final segment marker for session {SessionId}", session.SessionId);
                                        }
                                        finally
                                        {
                                            // Disconnect Doubao client
                                            try
                                            {
                                                clientData.cts.Cancel();
                                                await clientData.client.DisconnectAsync();
                                                clientData.client.Dispose();
                                                Log.Information("Doubao client disconnected for session {SessionId}", session.SessionId);
                                            }
                                            catch (Exception ex)
                                            {
                                                Log.Warning(ex, "Error disconnecting Doubao client for session {SessionId}", session.SessionId);
                                            }
                                        }
                                        // Remove from dictionary
                                        doubaoClients.TryRemove(context.Connection.Id, out _);
                                    }

                                    // Save recorded audio for debugging
                                    if (audioRecorders.TryRemove(context.Connection.Id, out var recorder))
                                    {
                                        try
                                        {
                                            await recorder.SaveToFileAsync();
                                            recorder.Dispose();
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Warning(ex, "Failed to save recorded audio for session {SessionId}", session.SessionId);
                                        }
                                    }

                                    sessionManager.UpdateSessionState(context.Connection.Id, SessionState.Closed);
                                    var stoppedStatus = ControlMessage.CreateStatus("closed", session.SessionId);
                                    var stoppedJson = stoppedStatus.ToJson();
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
                        Log.Warning("Received audio data but session is not in recognizing state (Current: {State})", session.State);
                    }
                    else if (doubaoClients.TryGetValue(context.Connection.Id, out var clientData))
                    {
                        // Forward audio to Doubao
                        var audioData = buffer.Take(receiveResult.Count).ToArray();

                        // Record audio for debugging
                        if (audioRecorders.TryGetValue(context.Connection.Id, out var recorder))
                        {
                            recorder.AddAudioData(audioData);
                        }

                        Log.Debug("Received {Count} bytes of audio data from frontend, client.IsConnected={IsConnected}",
                            receiveResult.Count, clientData.client.IsConnected);
                        try
                        {
                            // Note: Don't use receiveResult.EndOfMessage as the frontend sends each buffer as a complete message
                            // The client will send a proper last segment marker when recognition ends via EndRecognition command
                            await clientData.client.SendAudioSegment(audioData, false);
                            Log.Debug("Forwarded {Count} bytes of audio data to Doubao successfully", receiveResult.Count);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to forward audio to Doubao for {ConnectionId}", context.Connection.Id);
                            // Don't continue the connection after a send error
                            break;
                        }
                    }
                    else
                    {
                        Log.Warning("Received audio data but Doubao client is not connected for {ConnectionId}", context.Connection.Id);
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
            // Cleanup Doubao client
            if (doubaoClients.TryRemove(context.Connection.Id, out var clientData))
            {
                try
                {
                    clientData.cts.Cancel();
                    // Use Abort for immediate cleanup without waiting
                    clientData.client.Abort();
                    Log.Debug("Doubao client aborted for {ConnectionId}", context.Connection.Id);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error cleaning up Doubao client for {ConnectionId}", context.Connection.Id);
                }
            }

            // Cleanup audio recorder
            if (audioRecorders.TryRemove(context.Connection.Id, out var recorder2))
            {
                try
                {
                    await recorder2.SaveToFileAsync();
                    recorder2.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error cleaning up audio recorder for {ConnectionId}", context.Connection.Id);
                }
            }

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

/// <summary>
/// Audio recorder for debugging - saves incoming audio to WAV files
/// </summary>
class AudioRecorder : IDisposable
{
    private readonly List<byte[]> _audioChunks = new();
    private readonly string _sessionId;
    private readonly int _sampleRate;
    private readonly int _bitsPerSample;
    private readonly int _channels;
    private long _totalBytes = 0;
    private bool _disposed = false;

    public AudioRecorder(string sessionId, int sampleRate, int bitsPerSample, int channels)
    {
        _sessionId = sessionId;
        _sampleRate = sampleRate;
        _bitsPerSample = bitsPerSample;
        _channels = channels;
    }

    public void AddAudioData(byte[] data)
    {
        if (_disposed) return;

        lock (_audioChunks)
        {
            _audioChunks.Add(data.ToArray());
            _totalBytes += data.Length;
        }
    }

    public async Task SaveToFileAsync()
    {
        if (_disposed) return;

        byte[] allAudioData;
        lock (_audioChunks)
        {
            allAudioData = new byte[_totalBytes];
            int offset = 0;
            foreach (var chunk in _audioChunks)
            {
                Array.Copy(chunk, 0, allAudioData, offset, chunk.Length);
                offset += chunk.Length;
            }
        }

        // Create WAV file
        var wavData = WavParser.CreateWavData(allAudioData, _sampleRate, (ushort)_channels, (ushort)_bitsPerSample);

        // Save to file
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var directory = "debug-audio";
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, $"audio_{_sessionId}_{timestamp}.wav");
        await File.WriteAllBytesAsync(filePath, wavData);

        Log.Information("Saved debug audio to {FilePath} ({ByteCount} bytes, {DurationMs}ms)",
            filePath, allAudioData.Length, (_totalBytes / (_sampleRate * _bitsPerSample / 8 * _channels) * 1000));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _audioChunks.Clear();
        }
    }
}
