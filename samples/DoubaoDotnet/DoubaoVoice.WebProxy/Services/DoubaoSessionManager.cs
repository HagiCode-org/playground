using DoubaoVoice.WebProxy.Models;
using System.Collections.Concurrent;
using Serilog;

namespace DoubaoVoice.WebProxy.Services;

/// <summary>
/// Manages Doubao recognition sessions
/// </summary>
public class DoubaoSessionManager : IDoubaoSessionManager, IDisposable
{
    private readonly ConcurrentDictionary<string, DoubaoSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _connectionToSession = new();
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    private bool _disposed;

    // WebSocket handler reference for sending messages
    private WebSocketMessageSender? _messageSender;

    public event EventHandler<SessionCreatedEventArgs>? SessionCreated;
    public event EventHandler<SessionRemovedEventArgs>? SessionRemoved;

    public DoubaoSessionManager()
    {
    }

    /// <summary>
    /// Sets the message sender for WebSocket communication
    /// </summary>
    public void SetMessageSender(WebSocketMessageSender sender)
    {
        _messageSender = sender;
    }

    public DoubaoSession CreateSession(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
            throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));

        var session = new DoubaoSession
        {
            ConnectionId = connectionId,
            SessionId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            State = SessionState.Created
        };

        // Configure audio buffer with client config if available
        if (session.ClientConfig != null)
        {
            session.AudioBuffer.MaxSize = session.ClientConfig.BufferSize ?? 10;
            session.AudioBuffer.TimeoutMs = session.ClientConfig.BufferTimeoutMs ?? 5000;
            session.ResultAggregator.ConfidenceThreshold = session.ClientConfig.ConfidenceThreshold ?? 0.5f;
        }

        _sessions[connectionId] = session;
        _connectionToSession[connectionId] = session.SessionId;

        Log.Information("Session created: {SessionId} for connection {ConnectionId}", session.SessionId, connectionId);
        SessionCreated?.Invoke(this, new SessionCreatedEventArgs { Session = session });

        return session;
    }

    public DoubaoSession? GetSession(string connectionId)
    {
        _sessions.TryGetValue(connectionId, out var session);
        return session;
    }

    public DoubaoSession? GetSessionById(string sessionId)
    {
        return _sessions.Values.FirstOrDefault(s => s.SessionId == sessionId);
    }

    public bool RemoveSession(string connectionId)
    {
        if (_sessions.TryRemove(connectionId, out var session))
        {
            _connectionToSession.TryRemove(connectionId, out _);

            Log.Information("Session removed: {SessionId} for connection {ConnectionId}", session.SessionId, connectionId);
            SessionRemoved?.Invoke(this, new SessionRemovedEventArgs
            {
                ConnectionId = connectionId,
                SessionId = session.SessionId
            });

            return true;
        }
        return false;
    }

    public IReadOnlyList<DoubaoSession> GetActiveSessions()
    {
        return _sessions.Values.Where(s => s.State != SessionState.Closed && s.State != SessionState.Error).ToList().AsReadOnly();
    }

    public void UpdateSessionState(string connectionId, SessionState state)
    {
        var session = GetSession(connectionId);
        if (session != null)
        {
            session.State = state;
            session.UpdateActivity();
            Log.Debug("Session {SessionId} state updated to {State}", session.SessionId, state);
        }
    }

    public Task AddAudioAsync(string connectionId, byte[] audioData)
    {
        var session = GetSession(connectionId);
        if (session == null)
        {
            Log.Warning("Cannot add audio: session not found for connection {ConnectionId}", connectionId);
            return Task.CompletedTask;
        }

        if (session.State == SessionState.Paused)
        {
            Log.Debug("Session {SessionId} is paused, audio not added", session.SessionId);
            return Task.CompletedTask;
        }

        session.AudioBuffer.Add(audioData);
        session.UpdateActivity();

        Log.Debug("Audio added to session {SessionId}, buffer size: {Count}", session.SessionId, session.AudioBuffer.Count);
        return Task.CompletedTask;
    }

    public Task SendResultAsync(string connectionId, RecognitionResultDto result)
    {
        if (_messageSender == null)
        {
            Log.Warning("Cannot send result: message sender not configured");
            return Task.CompletedTask;
        }

        var session = GetSession(connectionId);
        if (session == null)
        {
            Log.Warning("Cannot send result: session not found for connection {ConnectionId}", connectionId);
            return Task.CompletedTask;
        }

        session.ResultAggregator.AddResult(result);
        session.UpdateActivity();

        var message = ControlMessage.CreateResult(result);
        return _messageSender?.Invoke(connectionId, message) ?? Task.CompletedTask;
    }

    public Task SendErrorAsync(string connectionId, string errorMessage, string errorCode = "")
    {
        if (_messageSender == null)
        {
            Log.Warning("Cannot send error: message sender not configured");
            return Task.CompletedTask;
        }

        Log.Error("Sending error to connection {ConnectionId}: {ErrorMessage}", connectionId, errorMessage);
        var message = ControlMessage.CreateError(errorMessage, errorCode);
        return _messageSender?.Invoke(connectionId, message) ?? Task.CompletedTask;
    }

    public async Task CleanupInactiveSessions(TimeSpan timeout)
    {
        await _cleanupLock.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            var inactiveConnections = _sessions
                .Where(kvp => (now - kvp.Value.LastActivityAt) > timeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var connectionId in inactiveConnections)
            {
                Log.Information("Cleaning up inactive session for connection {ConnectionId}", connectionId);
                RemoveSession(connectionId);
            }

            if (inactiveConnections.Count > 0)
            {
                Log.Information("Cleaned up {Count} inactive sessions", inactiveConnections.Count);
            }
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    public void SetClientConfig(string connectionId, ClientConfigDto config)
    {
        var session = GetSession(connectionId);
        if (session == null)
        {
            Log.Warning("Cannot set config: session not found for connection {ConnectionId}", connectionId);
            return;
        }

        config.Validate();
        session.ClientConfig = config;
        session.UpdateActivity();

        Log.Information("Client config set for session {SessionId}: AppId={AppId}",
            session.SessionId, config.AppId);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var session in _sessions.Values)
        {
            session.AudioBuffer.Clear();
            session.ResultAggregator.Clear();
        }

        _sessions.Clear();
        _connectionToSession.Clear();
        _cleanupLock.Dispose();
    }
}
