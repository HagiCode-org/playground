using DoubaoVoice.WebProxy.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoubaoVoice.WebProxy.Services;

/// <summary>
/// Aggregates and filters recognition results
/// </summary>
public class ResultAggregator
{
    private readonly List<RecognitionResultDto> _results = new();
    private DateTime _windowStartTime = DateTime.UtcNow;
    private readonly HashSet<string> _seenTexts = new();

    /// <summary>
    /// Confidence threshold for filtering results
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Time window for aggregation in milliseconds
    /// </summary>
    public int TimeWindowMs { get; set; } = 2000;

    /// <summary>
    /// Gets all aggregated results
    /// </summary>
    public IReadOnlyList<RecognitionResultDto> Results => _results.AsReadOnly();

    /// <summary>
    /// Clears all aggregated results
    /// </summary>
    public void Clear()
    {
        _results.Clear();
        _seenTexts.Clear();
        _windowStartTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a result to the aggregator
    /// </summary>
    public void AddResult(RecognitionResultDto result)
    {
        if (result == null)
            return;

        // Filter by confidence threshold
        if (result.Confidence < ConfidenceThreshold)
            return;

        // Filter duplicates
        var normalizedText = NormalizeText(result.Text);
        if (_seenTexts.Contains(normalizedText))
            return;

        _seenTexts.Add(normalizedText);
        _results.Add(result);

        // Update time window
        if ((DateTime.UtcNow - _windowStartTime).TotalMilliseconds > TimeWindowMs)
        {
            _windowStartTime = DateTime.UtcNow;
            _seenTexts.Clear();
        }
    }

    /// <summary>
    /// Aggregates results within the current time window
    /// </summary>
    public RecognitionResultDto? Aggregate()
    {
        if (_results.Count == 0)
            return null;

        // Get results within time window
        var windowResults = _results
            .Where(r => (DateTime.UtcNow - r.Timestamp).TotalMilliseconds <= TimeWindowMs)
            .ToList();

        if (windowResults.Count == 0)
            return null;

        // Aggregate by taking the latest result with highest confidence
        var bestResult = windowResults.OrderByDescending(r => r.Confidence).First();

        // Merge utterances
        var mergedUtterances = new List<UtteranceDto>();
        var utteranceMap = new Dictionary<int, UtteranceDto>();

        foreach (var result in windowResults)
        {
            foreach (var utterance in result.Utterances)
            {
                if (!utteranceMap.ContainsKey(utterance.StartTime))
                {
                    utteranceMap[utterance.StartTime] = utterance;
                }
                else if (utterance.Definite && !utteranceMap[utterance.StartTime].Definite)
                {
                    utteranceMap[utterance.StartTime] = utterance;
                }
            }
        }

        mergedUtterances.AddRange(utteranceMap.OrderBy(u => u.Key).Select(u => u.Value));

        return new RecognitionResultDto
        {
            Text = bestResult.Text,
            Confidence = windowResults.Average(r => r.Confidence),
            Duration = windowResults.Sum(r => r.Duration),
            IsFinal = windowResults.Any(r => r.IsFinal),
            Utterances = mergedUtterances,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets the result as plain text
    /// </summary>
    public string ToPlainText()
    {
        return Aggregate()?.Text ?? "";
    }

    /// <summary>
    /// Gets the result as JSON
    /// </summary>
    public string ToJson()
    {
        var result = Aggregate();
        if (result == null)
            return "{}";

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(result, options);
    }

    /// <summary>
    /// Gets formatted result with timestamps
    /// </summary>
    public string ToFormattedText()
    {
        var result = Aggregate();
        if (result == null)
            return "";

        var formatted = $"[{result.Timestamp:HH:mm:ss}] {result.Text}";
        if (result.Utterances.Count > 0)
        {
            formatted += Environment.NewLine;
            foreach (var utterance in result.Utterances)
            {
                formatted += $"  [{utterance.StartTime}ms - {utterance.EndTime}ms] {utterance.Text}" + Environment.NewLine;
            }
        }
        return formatted;
    }

    /// <summary>
    /// Normalizes text for duplicate detection
    /// </summary>
    private string NormalizeText(string text)
    {
        return text?.Trim().ToLowerInvariant() ?? "";
    }

    /// <summary>
    /// Gets the count of results in the current window
    /// </summary>
    public int GetWindowResultCount()
    {
        return _results
            .Count(r => (DateTime.UtcNow - r.Timestamp).TotalMilliseconds <= TimeWindowMs);
    }

    /// <summary>
    /// Removes old results outside the time window
    /// </summary>
    public void RemoveOldResults()
    {
        _results.RemoveAll(r => (DateTime.UtcNow - r.Timestamp).TotalMilliseconds > TimeWindowMs);
    }
}
