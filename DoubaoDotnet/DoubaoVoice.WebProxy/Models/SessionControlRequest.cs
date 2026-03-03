using System.Text.Json.Serialization;

namespace DoubaoVoice.WebProxy.Models;

/// <summary>
/// Command types for session control
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ControlCommand
{
    /// <summary>
    /// Start recognition
    /// </summary>
    [JsonPropertyName("startRecognition")]
    StartRecognition,

    /// <summary>
    /// End recognition
    /// </summary>
    [JsonPropertyName("endRecognition")]
    EndRecognition,

    /// <summary>
    /// Pause recognition
    /// </summary>
    [JsonPropertyName("pauseRecognition")]
    PauseRecognition,

    /// <summary>
    /// Resume recognition
    /// </summary>
    [JsonPropertyName("resumeRecognition")]
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
