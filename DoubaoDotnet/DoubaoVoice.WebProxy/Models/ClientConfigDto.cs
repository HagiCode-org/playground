using System.Text.Json.Serialization;

namespace DoubaoVoice.WebProxy.Models;

/// <summary>
/// DTO for client configuration parameters
/// </summary>
public class ClientConfigDto
{
    /// <summary>
    /// Doubao API App ID
    /// </summary>
    [JsonPropertyName("appId")]
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Doubao API Access Token
    /// </summary>
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Service URL for WebSocket connection
    /// </summary>
    [JsonPropertyName("serviceUrl")]
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Resource ID for the API
    /// </summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    /// <summary>
    /// Audio sample rate in Hz
    /// </summary>
    [JsonPropertyName("sampleRate")]
    public int? SampleRate { get; set; }

    /// <summary>
    /// Audio bit depth
    /// </summary>
    [JsonPropertyName("bitsPerSample")]
    public int? BitsPerSample { get; set; }

    /// <summary>
    /// Number of audio channels
    /// </summary>
    [JsonPropertyName("channels")]
    public int? Channels { get; set; }

    /// <summary>
    /// Audio format
    /// </summary>
    [JsonPropertyName("audioFormat")]
    public string? AudioFormat { get; set; }

    /// <summary>
    /// Audio codec
    /// </summary>
    [JsonPropertyName("audioCodec")]
    public string? AudioCodec { get; set; }

    /// <summary>
    /// Model name to use
    /// </summary>
    [JsonPropertyName("modelName")]
    public string? ModelName { get; set; }

    /// <summary>
    /// Enable ITN (Inverse Text Normalization)
    /// </summary>
    [JsonPropertyName("enableITN")]
    public bool? EnableITN { get; set; }

    /// <summary>
    /// Enable punctuation in results
    /// </summary>
    [JsonPropertyName("enablePunctuation")]
    public bool? EnablePunctuation { get; set; }

    /// <summary>
    /// Enable DDC (Domain Adaptation for Dialogue)
    /// </summary>
    [JsonPropertyName("enableDDC")]
    public bool? EnableDDC { get; set; }

    /// <summary>
    /// Show utterances in results
    /// </summary>
    [JsonPropertyName("showUtterances")]
    public bool? ShowUtterances { get; set; }

    /// <summary>
    /// Enable non-stream mode
    /// </summary>
    [JsonPropertyName("enableNonstream")]
    public bool? EnableNonstream { get; set; }

    /// <summary>
    /// Audio buffer size (max segments)
    /// </summary>
    [JsonPropertyName("bufferSize")]
    public int? BufferSize { get; set; }

    /// <summary>
    /// Audio buffer timeout in milliseconds
    /// </summary>
    [JsonPropertyName("bufferTimeoutMs")]
    public int? BufferTimeoutMs { get; set; }

    /// <summary>
    /// Confidence threshold for filtering results (0-1)
    /// </summary>
    [JsonPropertyName("confidenceThreshold")]
    public float? ConfidenceThreshold { get; set; }

    /// <summary>
    /// Chunk size in bytes for sending audio
    /// </summary>
    [JsonPropertyName("chunkSizeBytes")]
    public int? ChunkSizeBytes { get; set; }

    /// <summary>
    /// User ID for tracking
    /// </summary>
    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    /// <summary>
    /// Device ID for tracking
    /// </summary>
    [JsonPropertyName("did")]
    public string? Did { get; set; }

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(AppId))
            throw new ArgumentException("AppId is required", nameof(AppId));

        if (string.IsNullOrEmpty(AccessToken))
            throw new ArgumentException("AccessToken is required", nameof(AccessToken));
    }

    /// <summary>
    /// Creates a DoubaoVoiceConfig from this DTO
    /// </summary>
    public DoubaoVoice.SDK.DoubaoVoiceConfig ToDoubaoVoiceConfig()
    {
        return new DoubaoVoice.SDK.DoubaoVoiceConfig
        {
            AppId = AppId,
            AccessToken = AccessToken,
            ServiceUrl = ServiceUrl ?? "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async",
            ResourceId = ResourceId ?? "volc.bigasr.sauc.duration",
            SampleRate = SampleRate ?? 16000,
            BitsPerSample = BitsPerSample ?? 16,
            Channels = Channels ?? 1,
            AudioFormat = AudioFormat ?? "wav",
            AudioCodec = AudioCodec ?? "raw",
            ModelName = ModelName ?? "bigmodel",
            EnableITN = EnableITN ?? true,
            EnablePunctuation = EnablePunctuation ?? true,
            EnableDDC = EnableDDC ?? true,
            ShowUtterances = ShowUtterances ?? true,
            EnableNonstream = EnableNonstream ?? false,
            Uid = Uid ?? "demo_uid",
            Did = Did ?? string.Empty,
            Platform = "dotnet",
            SdkVersion = "1.0.0",
            AppVersion = "1.0.0"
        };
    }
}
