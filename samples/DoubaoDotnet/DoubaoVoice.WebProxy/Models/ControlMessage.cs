using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoubaoVoice.WebProxy.Models;

/// <summary>
/// Message types for WebSocket communication
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageType
{
    /// <summary>
    /// Audio segment data
    /// </summary>
    [JsonPropertyName("audio")]
    Audio,

    /// <summary>
    /// Session control command
    /// </summary>
    [JsonPropertyName("control")]
    Control,

    /// <summary>
    /// Recognition result
    /// </summary>
    [JsonPropertyName("result")]
    Result,

    /// <summary>
    /// Error message
    /// </summary>
    [JsonPropertyName("error")]
    Error,

    /// <summary>
    /// Connection status
    /// </summary>
    [JsonPropertyName("status")]
    Status,

    /// <summary>
    /// Client configuration parameters
    /// </summary>
    [JsonPropertyName("config")]
    Config
}

/// <summary>
/// Base message for WebSocket communication
/// </summary>
public class ControlMessage
{
    /// <summary>
    /// Type of the message
    /// </summary>
    public MessageType Type { get; set; }

    /// <summary>
    /// Message ID for tracking
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp of the message
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Payload data (audio, control, result, etc.)
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// Creates an audio message
    /// </summary>
    public static ControlMessage CreateAudio(byte[] audioData, bool isLastSegment = false)
    {
        return new ControlMessage
        {
            Type = MessageType.Audio,
            Payload = new AudioSegmentRequest
            {
                Data = audioData,
                DurationMs = 200, // Default segment duration
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                IsLastSegment = isLastSegment
            }
        };
    }

    /// <summary>
    /// Creates a control message
    /// </summary>
    public static ControlMessage CreateControl(ControlCommand command)
    {
        return new ControlMessage
        {
            Type = MessageType.Control,
            Payload = new SessionControlRequest { Command = command }
        };
    }

    /// <summary>
    /// Creates a result message
    /// </summary>
    public static ControlMessage CreateResult(RecognitionResultDto result)
    {
        return new ControlMessage
        {
            Type = MessageType.Result,
            Payload = result
        };
    }

    /// <summary>
    /// Creates an error message
    /// </summary>
    public static ControlMessage CreateError(string errorMessage, string errorCode = "")
    {
        return new ControlMessage
        {
            Type = MessageType.Error,
            Payload = new ErrorDto
            {
                ErrorMessage = errorMessage,
                ErrorCode = errorCode,
                Timestamp = DateTime.UtcNow
            }
        };
    }

    /// <summary>
    /// Creates a status message
    /// </summary>
    public static ControlMessage CreateStatus(string status, string sessionId)
    {
        return new ControlMessage
        {
            Type = MessageType.Status,
            Payload = new StatusDto
            {
                Status = status,
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow
            }
        };
    }

    /// <summary>
    /// Creates a config message from client
    /// </summary>
    public static ControlMessage CreateConfig(ClientConfigDto config)
    {
        return new ControlMessage
        {
            Type = MessageType.Config,
            Payload = config
        };
    }

    /// <summary>
    /// Serializes the message to JSON
    /// </summary>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Deserializes a JSON string to a message
    /// </summary>
    public static ControlMessage? FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        return JsonSerializer.Deserialize<ControlMessage>(json, options);
    }
}

/// <summary>
/// DTO for error messages
/// </summary>
public class ErrorDto
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// DTO for status messages
/// </summary>
public class StatusDto
{
    public string Status { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
