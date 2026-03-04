using DoubaoVoice.WebProxy.Models;
using DoubaoVoice.WebProxy.Services;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;

namespace DoubaoVoice.WebProxy.Handlers;

/// <summary>
/// WebSocket handler for DoubaoVoice proxy
/// </summary>
public class DoubaoWebSocketHandler
{
    private readonly IDoubaoSessionManager _sessionManager;
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
    private readonly ConcurrentDictionary<string, DoubaoClientWrapper> _clientWrappers = new();

    public DoubaoWebSocketHandler(IDoubaoSessionManager sessionManager)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _sessionManager.SetMessageSender(SendWebSocketMessageAsync);
    }

    /// <summary>
    /// Handles a WebSocket connection
    /// </summary>
    public async Task HandleAsync(HttpContext context)
    {
        var connectionId = context.Connection.Id;
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        _connections[connectionId] = webSocket;

        Log.Information("WebSocket connection accepted: {ConnectionId}", connectionId);

        try
        {
            // Create session
            var session = _sessionManager.CreateSession(connectionId);

            // Send connected status
            await SendStatusAsync(connectionId, "connected", session.SessionId);

            // Start message loop
            await ReceiveMessagesAsync(connectionId, webSocket, session);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling WebSocket connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "Connection error: " + ex.Message);
        }
        finally
        {
            await CleanupConnectionAsync(connectionId);
        }
    }

    /// <summary>
    /// Receives and processes messages from the WebSocket
    /// </summary>
    private async Task ReceiveMessagesAsync(string connectionId, WebSocket webSocket, DoubaoSession session)
    {
        var buffer = new byte[8192];
        var messageBuffer = new List<byte>();

        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                Log.Information("WebSocket close requested for connection {ConnectionId}", connectionId);
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                // Handle binary audio data
                var audioData = buffer.Take(result.Count).ToArray();
                await HandleAudioMessageAsync(connectionId, session, audioData, result.EndOfMessage);
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                // Accumulate text messages
                messageBuffer.AddRange(buffer.Take(result.Count));

                if (result.EndOfMessage)
                {
                    var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    await HandleTextMessageAsync(connectionId, session, json);
                    messageBuffer.Clear();
                }
            }
        }
    }

    /// <summary>
    /// Handles a text message (control commands)
    /// </summary>
    private async Task HandleTextMessageAsync(string connectionId, DoubaoSession session, string json)
    {
        try
        {
            var message = ControlMessage.FromJson(json);
            if (message == null)
            {
                await SendErrorAsync(connectionId, "Invalid message format");
                return;
            }

            Log.Debug("Received message type {Type} from connection {ConnectionId}", message.Type, connectionId);

            switch (message.Type)
            {
                case MessageType.Config:
                    await HandleConfigMessageAsync(connectionId, session, message);
                    break;

                case MessageType.Control:
                    await HandleControlMessageAsync(connectionId, session, message);
                    break;

                case MessageType.Audio:
                    // JSON audio messages not supported, use binary
                    await SendErrorAsync(connectionId, "Audio data should be sent as binary");
                    break;

                default:
                    Log.Warning("Unhandled message type: {Type}", message.Type);
                    break;
            }
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Failed to parse message from connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "Invalid JSON format");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling message from connection {ConnectionId}", connectionId);
            await SendErrorAsync(connectionId, "Error processing message");
        }
    }

    /// <summary>
    /// Handles a control message
    /// </summary>
    private async Task HandleControlMessageAsync(string connectionId, DoubaoSession session, ControlMessage message)
    {
        if (message.Payload is not SessionControlRequest controlRequest)
        {
            await SendErrorAsync(connectionId, "Invalid control request");
            return;
        }

        Log.Information("Control command: {Command} for connection {ConnectionId}", controlRequest.Command, connectionId);

        switch (controlRequest.Command)
        {
            case ControlCommand.StartRecognition:
                await StartRecognitionAsync(connectionId, session);
                break;

            case ControlCommand.EndRecognition:
                await EndRecognitionAsync(connectionId, session);
                break;

            case ControlCommand.PauseRecognition:
                await PauseRecognitionAsync(connectionId, session);
                break;

            case ControlCommand.ResumeRecognition:
                await ResumeRecognitionAsync(connectionId, session);
                break;

            default:
                await SendErrorAsync(connectionId, $"Unknown control command: {controlRequest.Command}");
                break;
        }
    }

    /// <summary>
    /// Handles a config message from client
    /// </summary>
    private async Task HandleConfigMessageAsync(string connectionId, DoubaoSession session, ControlMessage message)
    {
        if (message.Payload is not ClientConfigDto config)
        {
            await SendErrorAsync(connectionId, "Invalid config payload");
            return;
        }

        try
        {
            config.Validate();
            _sessionManager.SetClientConfig(connectionId, config);
            await SendStatusAsync(connectionId, "config_received", session.SessionId);
            Log.Information("Client config received for session {SessionId}", session.SessionId);
        }
        catch (ArgumentException ex)
        {
            await SendErrorAsync(connectionId, ex.Message);
        }
    }

    /// <summary>
    /// Handles binary audio data
    /// </summary>
    private async Task HandleAudioMessageAsync(string connectionId, DoubaoSession session, byte[] audioData, bool isLastSegment)
    {
        if (session.State == SessionState.Closed || session.State == SessionState.Error)
        {
            Log.Warning("Cannot add audio: session {SessionId} is in {State} state", session.SessionId, session.State);
            return;
        }

        await _sessionManager.AddAudioAsync(connectionId, audioData);

        // Forward audio to Doubao client
        if (_clientWrappers.TryGetValue(connectionId, out var wrapper))
        {
            await wrapper.SendAudioAsync(audioData, isLastSegment);
        }

        session.UpdateActivity();
    }

    /// <summary>
    /// Starts recognition
    /// </summary>
    private async Task StartRecognitionAsync(string connectionId, DoubaoSession session)
    {
        if (session.State == SessionState.Recognizing)
        {
            await SendErrorAsync(connectionId, "Recognition already in progress");
            return;
        }

        // Check if client config is provided
        if (session.ClientConfig == null)
        {
            await SendErrorAsync(connectionId, "Client configuration not set. Please send config message first.");
            return;
        }

        try
        {
            // Create Doubao client wrapper with client config
            var wrapper = new DoubaoClientWrapper(session.ClientConfig, session.SessionId);
            _clientWrappers[connectionId] = wrapper;

            // Subscribe to events
            wrapper.OnResultReceived += async (sender, args) =>
            {
                var result = ConvertToResultDto(args);
                await _sessionManager.SendResultAsync(connectionId, result);
            };

            wrapper.OnError += async (sender, args) =>
            {
                await _sessionManager.SendErrorAsync(connectionId, args.ErrorMessage);
            };

            // Connect to Doubao
            await wrapper.ConnectAsync();

            _sessionManager.UpdateSessionState(connectionId, SessionState.Recognizing);
            await SendStatusAsync(connectionId, "recognizing", session.SessionId);

            Log.Information("Recognition started for session {SessionId}", session.SessionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start recognition for session {SessionId}", session.SessionId);
            await SendErrorAsync(connectionId, "Failed to start recognition: " + ex.Message);
            _sessionManager.UpdateSessionState(connectionId, SessionState.Error);
        }
    }

    /// <summary>
    /// Ends recognition
    /// </summary>
    private async Task EndRecognitionAsync(string connectionId, DoubaoSession session)
    {
        if (_clientWrappers.TryRemove(connectionId, out var wrapper))
        {
            await wrapper.DisconnectAsync();
        }

        _sessionManager.UpdateSessionState(connectionId, SessionState.Closed);
        await SendStatusAsync(connectionId, "closed", session.SessionId);

        Log.Information("Recognition ended for session {SessionId}", session.SessionId);
    }

    /// <summary>
    /// Pauses recognition
    /// </summary>
    private async Task PauseRecognitionAsync(string connectionId, DoubaoSession session)
    {
        if (session.State != SessionState.Recognizing)
        {
            await SendErrorAsync(connectionId, "Recognition is not in progress");
            return;
        }

        session.IsPaused = true;
        _sessionManager.UpdateSessionState(connectionId, SessionState.Paused);
        await SendStatusAsync(connectionId, "paused", session.SessionId);

        Log.Information("Recognition paused for session {SessionId}", session.SessionId);
    }

    /// <summary>
    /// Resumes recognition
    /// </summary>
    private async Task ResumeRecognitionAsync(string connectionId, DoubaoSession session)
    {
        if (session.State != SessionState.Paused)
        {
            await SendErrorAsync(connectionId, "Recognition is not paused");
            return;
        }

        session.IsPaused = false;
        _sessionManager.UpdateSessionState(connectionId, SessionState.Recognizing);
        await SendStatusAsync(connectionId, "recognizing", session.SessionId);

        Log.Information("Recognition resumed for session {SessionId}", session.SessionId);
    }

    /// <summary>
    /// Sends a status message
    /// </summary>
    private async Task SendStatusAsync(string connectionId, string status, string sessionId)
    {
        var message = ControlMessage.CreateStatus(status, sessionId);
        await SendWebSocketMessageAsync(connectionId, message);
    }

    /// <summary>
    /// Sends an error message
    /// </summary>
    private async Task SendErrorAsync(string connectionId, string errorMessage, string errorCode = "")
    {
        var message = ControlMessage.CreateError(errorMessage, errorCode);
        await SendWebSocketMessageAsync(connectionId, message);
    }

    /// <summary>
    /// Sends a WebSocket message
    /// </summary>
    public async Task SendWebSocketMessageAsync(string connectionId, ControlMessage message)
    {
        if (!_connections.TryGetValue(connectionId, out var webSocket))
        {
            Log.Warning("Cannot send message: connection {ConnectionId} not found", connectionId);
            return;
        }

        if (webSocket.State != WebSocketState.Open)
        {
            Log.Warning("Cannot send message: connection {ConnectionId} is not open (state: {State})", connectionId, webSocket.State);
            return;
        }

        var json = message.ToJson();
        var buffer = Encoding.UTF8.GetBytes(json);
        await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);

        Log.Debug("Sent message to connection {ConnectionId}: {MessageType}", connectionId, message.Type);
    }

    /// <summary>
    /// Cleans up a connection
    /// </summary>
    private async Task CleanupConnectionAsync(string connectionId)
    {
        Log.Information("Cleaning up connection {ConnectionId}", connectionId);

        // Remove Doubao client wrapper
        if (_clientWrappers.TryRemove(connectionId, out var wrapper))
        {
            await wrapper.DisposeAsync();
        }

        // Remove WebSocket connection
        if (_connections.TryRemove(connectionId, out var webSocket))
        {
            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                catch { }
            }
            webSocket.Dispose();
        }

        // Remove session
        _sessionManager.RemoveSession(connectionId);
    }

    /// <summary>
    /// Converts SDK result to DTO
    /// </summary>
    private RecognitionResultDto ConvertToResultDto(DoubaoVoice.SDK.ResultReceivedEventArgs args)
    {
        return new RecognitionResultDto
        {
            Text = args.Result.Text,
            Confidence = 1.0f, // SDK doesn't provide confidence, use default
            Duration = args.Result.AudioDuration,
            IsFinal = args.IsFinal,
            Utterances = args.Result.Utterances.Select(u => new UtteranceDto
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
    }
}

/// <summary>
/// Wrapper for DoubaoVoiceClient
/// </summary>
internal class DoubaoClientWrapper : IDisposable
{
    private readonly DoubaoVoice.SDK.DoubaoVoiceClient _client;
    private readonly string _sessionId;
    private readonly CancellationTokenSource _receiveCts = new();
    private bool _disposed;

    public event EventHandler<DoubaoVoice.SDK.ResultReceivedEventArgs>? OnResultReceived;
    public event EventHandler<DoubaoVoice.SDK.ErrorEventArgs>? OnError;

    public DoubaoClientWrapper(ClientConfigDto config, string sessionId)
    {
        _sessionId = sessionId;
        var voiceConfig = config.ToDoubaoVoiceConfig();
        _client = new DoubaoVoice.SDK.DoubaoVoiceClient(voiceConfig);

        _client.OnResultReceived += (s, e) => OnResultReceived?.Invoke(s, e);
        _client.OnError += (s, e) => OnError?.Invoke(s, e);
    }

    public async Task ConnectAsync()
    {
        await _client.ConnectAsync();
        await _client.SendFullClientRequest();

        // Start receiving messages in background
        _ = Task.Run(() => _client.ReceiveMessagesAsync(_receiveCts.Token));
    }

    public async Task SendAudioAsync(byte[] audioData, bool isLastSegment)
    {
        await _client.SendAudioSegment(audioData, isLastSegment);
    }

    public async Task DisconnectAsync()
    {
        await _client.DisconnectAsync();
        _receiveCts.Cancel();
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await DisconnectAsync();
        _client.Dispose();
        _receiveCts.Dispose();
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }
}
