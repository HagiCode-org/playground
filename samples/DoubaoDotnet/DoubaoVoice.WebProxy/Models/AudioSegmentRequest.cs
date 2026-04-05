namespace DoubaoVoice.WebProxy.Models;

/// <summary>
/// Request for audio segment data from clients
/// </summary>
public class AudioSegmentRequest
{
    /// <summary>
    /// Audio data in base64 format or binary
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Duration of the audio segment in milliseconds
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Timestamp when the audio was recorded
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// Indicates if this is the last segment
    /// </summary>
    public bool IsLastSegment { get; set; }
}
