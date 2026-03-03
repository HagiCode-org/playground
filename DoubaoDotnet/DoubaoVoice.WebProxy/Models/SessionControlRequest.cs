namespace DoubaoVoice.WebProxy.Models;

/// <summary>
/// Command types for session control
/// </summary>
public enum ControlCommand
{
    /// <summary>
    /// Start recognition
    /// </summary>
    StartRecognition,

    /// <summary>
    /// End recognition
    /// </summary>
    EndRecognition,

    /// <summary>
    /// Pause recognition
    /// </summary>
    PauseRecognition,

    /// <summary>
    /// Resume recognition
    /// </summary>
    ResumeRecognition
}

/// <summary>
/// DTO for session control requests
/// </summary>
public class SessionControlRequest
{
    /// <summary>
    /// The command to execute
    /// </summary>
    public ControlCommand Command { get; set; }

    /// <summary>
    /// Optional parameters for the command
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}
