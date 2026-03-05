namespace DoubaoVoice.SDK;

/// <summary>
/// Configuration class for DoubaoVoice SDK
/// </summary>
public class DoubaoVoiceConfig
{
    /// <summary>
    /// App ID for authentication
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Access Token for authentication
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Service URL for WebSocket connection
    /// v3 API: Bi-directional streaming (optimized) - recommended
    /// </summary>
    public string ServiceUrl { get; set; } = "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async";

    /// <summary>
    /// Resource ID for the API
    /// </summary>
    public string ResourceId { get; set; } = "volc.bigasr.sauc.duration";

    /// <summary>
    /// Audio sample rate in Hz (default: 16000)
    /// </summary>
    public int SampleRate { get; set; } = 16000;

    /// <summary>
    /// Audio bit depth (default: 16)
    /// </summary>
    public int BitsPerSample { get; set; } = 16;

    /// <summary>
    /// Number of audio channels (default: 1 for mono)
    /// </summary>
    public int Channels { get; set; } = 1;

    /// <summary>
    /// Audio format (default: "wav")
    /// </summary>
    public string AudioFormat { get; set; } = "wav";

    /// <summary>
    /// Audio codec (default: "raw")
    /// </summary>
    public string AudioCodec { get; set; } = "raw";

    /// <summary>
    /// Model name to use (default: "bigmodel")
    /// </summary>
    public string ModelName { get; set; } = "bigmodel";

    /// <summary>
    /// Enable ITN (Inverse Text Normalization)
    /// </summary>
    public bool EnableITN { get; set; } = true;

    /// <summary>
    /// Enable punctuation in results
    /// </summary>
    public bool EnablePunctuation { get; set; } = true;

    /// <summary>
    /// Enable DDC (Domain Adaptation for Dialogue)
    /// </summary>
    public bool EnableDDC { get; set; } = true;

    /// <summary>
    /// Show utterances in results
    /// </summary>
    public bool ShowUtterances { get; set; } = true;

    /// <summary>
    /// Enable non-stream mode
    /// </summary>
    public bool EnableNonstream { get; set; } = false;

    /// <summary>
    /// User ID for tracking
    /// </summary>
    public string Uid { get; set; } = "demo_uid";

    /// <summary>
    /// Device ID for tracking
    /// </summary>
    public string Did { get; set; } = string.Empty;

    /// <summary>
    /// Platform identifier
    /// </summary>
    public string Platform { get; set; } = "dotnet";

    /// <summary>
    /// SDK version
    /// </summary>
    public string SdkVersion { get; set; } = "1.0.0";

    /// <summary>
    /// App version
    /// </summary>
    public string AppVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Hotword contexts for improving recognition accuracy
    /// Each string represents a hotword context that will be passed to the API
    /// </summary>
    public List<string> HotwordContexts { get; set; } = new();

    /// <summary>
    /// Boosting table ID from self-learning platform
    /// </summary>
    public string? BoostingTableId { get; set; }

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(AppId))
            throw new ArgumentException("AppId is required", nameof(AppId));

        if (string.IsNullOrEmpty(AccessToken))
            throw new ArgumentException("AccessToken is required", nameof(AccessToken));

        if (string.IsNullOrEmpty(ServiceUrl))
            throw new ArgumentException("ServiceUrl is required", nameof(ServiceUrl));

        if (SampleRate <= 0)
            throw new ArgumentException("SampleRate must be greater than 0", nameof(SampleRate));

        if (BitsPerSample <= 0)
            throw new ArgumentException("BitsPerSample must be greater than 0", nameof(BitsPerSample));

        if (Channels <= 0)
            throw new ArgumentException("Channels must be greater than 0", nameof(Channels));
    }
}