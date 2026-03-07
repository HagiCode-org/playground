namespace DoubaoVoice.WebProxy.Models;

/// <summary>
/// DTO for recognition results
/// </summary>
public class RecognitionResultDto
{
    /// <summary>
    /// The recognized text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score of the recognition
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Audio duration in milliseconds
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// Indicates if this is a final result
    /// </summary>
    public bool IsFinal { get; set; }

    /// <summary>
    /// Indicates whether this result is a definite utterance from non-stream re-recognition
    /// </summary>
    public bool Definite { get; set; }

    /// <summary>
    /// List of utterances
    /// </summary>
    public List<UtteranceDto> Utterances { get; set; } = new();

    /// <summary>
    /// Timestamp of the result
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
