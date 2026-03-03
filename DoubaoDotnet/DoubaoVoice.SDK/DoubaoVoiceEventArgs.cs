namespace DoubaoVoice.SDK;

/// <summary>
/// Base class for event arguments
/// </summary>
public class DoubaoVoiceEventArgs : EventArgs
{
    /// <summary>
    /// Timestamp when the event occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for connection established event
/// </summary>
public class ConnectedEventArgs : DoubaoVoiceEventArgs
{
}

/// <summary>
/// Event arguments for connection disconnected event
/// </summary>
public class DisconnectedEventArgs : DoubaoVoiceEventArgs
{
    /// <summary>
    /// Reason for disconnection
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Event arguments for error event
/// </summary>
public class ErrorEventArgs : DoubaoVoiceEventArgs
{
    /// <summary>
    /// Error message
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Exception that caused the error
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Error code from server
    /// </summary>
    public int ErrorCode { get; set; }

    /// <summary>
    /// Indicates if this is an authentication error
    /// </summary>
    public bool IsAuthenticationError { get; set; }
}

/// <summary>
/// Represents a word in the recognition result
/// </summary>
public class RecognitionWord
{
    /// <summary>
    /// The word text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Start time in milliseconds
    /// </summary>
    public int StartTime { get; set; }

    /// <summary>
    /// End time in milliseconds
    /// </summary>
    public int EndTime { get; set; }
}

/// <summary>
/// Represents an utterance in the recognition result
/// </summary>
public class RecognitionUtterance
{
    /// <summary>
    /// The utterance text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Start time in milliseconds
    /// </summary>
    public int StartTime { get; set; }

    /// <summary>
    /// End time in milliseconds
    /// </summary>
    public int EndTime { get; set; }

    /// <summary>
    /// Whether this utterance is definite (finalized)
    /// </summary>
    public bool Definite { get; set; }

    /// <summary>
    /// Words in the utterance
    /// </summary>
    public List<RecognitionWord> Words { get; set; } = new();
}

/// <summary>
/// Represents the recognition result
/// </summary>
public class RecognitionResult
{
    /// <summary>
    /// The full recognized text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Audio duration in milliseconds
    /// </summary>
    public int AudioDuration { get; set; }

    /// <summary>
    /// Individual utterances
    /// </summary>
    public List<RecognitionUtterance> Utterances { get; set; } = new();
}

/// <summary>
/// Event arguments for result received event
/// </summary>
public class ResultReceivedEventArgs : DoubaoVoiceEventArgs
{
    /// <summary>
    /// The recognition result
    /// </summary>
    public RecognitionResult Result { get; set; } = new();

    /// <summary>
    /// Indicates if this is the final result
    /// </summary>
    public bool IsFinal { get; set; }
}

/// <summary>
/// Event arguments for recognition completed event
/// </summary>
public class RecognitionCompletedEventArgs : DoubaoVoiceEventArgs
{
    /// <summary>
    /// The final recognition result
    /// </summary>
    public RecognitionResult Result { get; set; } = new();

    /// <summary>
    /// Total number of segments processed
    /// </summary>
    public int TotalSegments { get; set; }
}