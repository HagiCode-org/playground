using DoubaoVoice.WebProxy.Models;

namespace DoubaoVoice.WebProxy.Services;

/// <summary>
/// Represents a Doubao recognition session
/// </summary>
public class DoubaoSession
{
    /// <summary>
    /// Unique session identifier
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Connection ID (WebSocket connection)
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// Time when session was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time when session was last active
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current state of the session
    /// </summary>
    public SessionState State { get; set; } = SessionState.Created;

    /// <summary>
    /// Audio buffer for the session
    /// </summary>
    public AudioBuffer AudioBuffer { get; set; } = new();

    /// <summary>
    /// Result aggregator for the session
    /// </summary>
    public ResultAggregator ResultAggregator { get; set; } = new();

    /// <summary>
    /// Whether the session is paused
    /// </summary>
    public bool IsPaused { get; set; } = false;

    /// <summary>
    /// Client configuration (appId, accessToken, etc.)
    /// </summary>
    public ClientConfigDto? ClientConfig { get; set; }

    /// <summary>
    /// Updates the last activity time
    /// </summary>
    public void UpdateActivity()
    {
        LastActivityAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Session state enumeration
/// </summary>
public enum SessionState
{
    /// <summary>
    /// Session has been created but not started
    /// </summary>
    Created,

    /// <summary>
    /// Recognition is in progress
    /// </summary>
    Recognizing,

    /// <summary>
    /// Recognition is paused
    /// </summary>
    Paused,

    /// <summary>
    /// Session is closing
    /// </summary>
    Closing,

    /// <summary>
    /// Session has been closed
    /// </summary>
    Closed,

    /// <summary>
    /// Session encountered an error
    /// </summary>
    Error
}

/// <summary>
/// Interface for Doubao session management
/// </summary>
public interface IDoubaoSessionManager
{
    /// <summary>
    /// Sets the message sender for WebSocket communication
    /// </summary>
    void SetMessageSender(WebSocketMessageSender sender);

    /// <summary>
    /// Creates a new session
    /// </summary>
    DoubaoSession CreateSession(string connectionId);

    /// <summary>
    /// Gets a session by connection ID
    /// </summary>
    DoubaoSession? GetSession(string connectionId);

    /// <summary>
    /// Gets a session by session ID
    /// </summary>
    DoubaoSession? GetSessionById(string sessionId);

    /// <summary>
    /// Removes a session
    /// </summary>
    bool RemoveSession(string connectionId);

    /// <summary>
    /// Gets all active sessions
    /// </summary>
    IReadOnlyList<DoubaoSession> GetActiveSessions();

    /// <summary>
    /// Updates the session state
    /// </summary>
    void UpdateSessionState(string connectionId, SessionState state);

    /// <summary>
    /// Adds audio data to a session
    /// </summary>
    Task AddAudioAsync(string connectionId, byte[] audioData);

    /// <summary>
    /// Sends a recognition result to the WebSocket handler
    /// </summary>
    Task SendResultAsync(string connectionId, RecognitionResultDto result);

    /// <summary>
    /// Sends an error to the WebSocket handler
    /// </summary>
    Task SendErrorAsync(string connectionId, string errorMessage, string errorCode = "");

    /// <summary>
    /// Cleans up inactive sessions
    /// </summary>
    Task CleanupInactiveSessions(TimeSpan timeout);

    /// <summary>
    /// Sets the client configuration for a session
    /// </summary>
    void SetClientConfig(string connectionId, ClientConfigDto config);

    /// <summary>
    /// Event raised when a session is created
    /// </summary>
    event EventHandler<SessionCreatedEventArgs>? SessionCreated;

    /// <summary>
    /// Event raised when a session is removed
    /// </summary>
    event EventHandler<SessionRemovedEventArgs>? SessionRemoved;
}

/// <summary>
/// Delegate for sending WebSocket messages
/// </summary>
public delegate Task WebSocketMessageSender(string connectionId, ControlMessage message);

/// <summary>
/// Event arguments for session created
/// </summary>
public class SessionCreatedEventArgs : EventArgs
{
    public DoubaoSession Session { get; set; } = null!;
}

/// <summary>
/// Event arguments for session removed
/// </summary>
public class SessionRemovedEventArgs : EventArgs
{
    public string ConnectionId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}
