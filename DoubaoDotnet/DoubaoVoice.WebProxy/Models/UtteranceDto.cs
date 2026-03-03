namespace DoubaoVoice.WebProxy.Models;

/// <summary>
/// DTO for utterance data
/// </summary>
public class UtteranceDto
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
    public List<WordDto> Words { get; set; } = new();
}

/// <summary>
/// DTO for word data
/// </summary>
public class WordDto
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
