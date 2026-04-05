using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoubaoVoice.WebProxy.Services;

/// <summary>
/// Hotword configuration entry
/// </summary>
public class HotwordEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("contexts")]
    public List<string> Contexts { get; set; } = new();
}

/// <summary>
/// Root configuration for hotwords
/// </summary>
public class HotwordConfiguration
{
    [JsonPropertyName("hotwords")]
    public List<HotwordEntry> Hotwords { get; set; } = new();
}

/// <summary>
/// Interface for hotword management service
/// </summary>
public interface IHotwordService
{
    /// <summary>
    /// Gets hotword contexts by hotword ID
    /// </summary>
    /// <param name="hotwordId">The hotword identifier</param>
    /// <returns>List of hotword contexts, or empty list if not found</returns>
    List<string> GetHotwordContexts(string hotwordId);

    /// <summary>
    /// Checks if a hotword ID exists in the configuration
    /// </summary>
    /// <param name="hotwordId">The hotword identifier</param>
    /// <returns>True if exists, false otherwise</returns>
    bool HasHotword(string hotwordId);

    /// <summary>
    /// Gets all available hotword IDs
    /// </summary>
    /// <returns>List of hotword IDs</returns>
    List<string> GetAllHotwordIds();
}

/// <summary>
/// Service for loading and managing hotword configurations
/// </summary>
public class HotwordService : IHotwordService
{
    private readonly HotwordConfiguration _configuration;
    private readonly Dictionary<string, List<string>> _hotwordCache = new();
    private readonly ILogger<HotwordService> _logger;

    public HotwordService(IConfiguration configuration, ILogger<HotwordService> logger)
    {
        _logger = logger;

        // Load hotwords configuration
        var hotwordsFile = Path.Combine(AppContext.BaseDirectory, "hotwords.json");

        // Also check the current directory for development
        if (!File.Exists(hotwordsFile))
        {
            hotwordsFile = "hotwords.json";
        }

        if (File.Exists(hotwordsFile))
        {
            try
            {
                var json = File.ReadAllText(hotwordsFile);
                _configuration = JsonSerializer.Deserialize<HotwordConfiguration>(json) ?? new HotwordConfiguration();

                // Build cache for fast lookup
                foreach (var entry in _configuration.Hotwords)
                {
                    _hotwordCache[entry.Id] = entry.Contexts;
                }

                _logger.LogInformation("Loaded {Count} hotword configurations from {File}",
                    _configuration.Hotwords.Count, hotwordsFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load hotwords configuration from {File}", hotwordsFile);
                _configuration = new HotwordConfiguration();
            }
        }
        else
        {
            _logger.LogWarning("Hotwords configuration file not found at {File}", hotwordsFile);
            _configuration = new HotwordConfiguration();
        }
    }

    /// <inheritdoc/>
    public List<string> GetHotwordContexts(string hotwordId)
    {
        if (string.IsNullOrEmpty(hotwordId))
        {
            _logger.LogDebug("HotwordId is null or empty, returning empty list");
            return new List<string>();
        }

        if (_hotwordCache.TryGetValue(hotwordId, out var contexts))
        {
            _logger.LogDebug("Found hotword contexts for ID '{HotwordId}': {Count} contexts",
                hotwordId, contexts.Count);
            return contexts;
        }

        _logger.LogWarning("Hotword ID '{HotwordId}' not found in configuration", hotwordId);
        return new List<string>();
    }

    /// <inheritdoc/>
    public bool HasHotword(string hotwordId)
    {
        return !string.IsNullOrEmpty(hotwordId) && _hotwordCache.ContainsKey(hotwordId);
    }

    /// <inheritdoc/>
    public List<string> GetAllHotwordIds()
    {
        return _hotwordCache.Keys.ToList();
    }
}
