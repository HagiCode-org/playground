namespace DoubaoVoice.WebProxy.Services.Configuration;

/// <summary>
/// Interface for Doubao proxy configuration options
/// </summary>
public interface IDoubaoProxyOptions
{
    // Doubao API credentials
    string AppId { get; set; }
    string AccessToken { get; set; }
    string ServiceUrl { get; set; }
    string ResourceId { get; set; }

    // Audio settings
    int SampleRate { get; set; }
    int BitsPerSample { get; set; }
    int Channels { get; set; }
    string AudioFormat { get; set; }
    string AudioCodec { get; set; }

    // Recognition settings
    string ModelName { get; set; }
    bool EnableITN { get; set; }
    bool EnablePunctuation { get; set; }
    bool EnableDDC { get; set; }
    bool ShowUtterances { get; set; }
    bool EnableNonstream { get; set; }

    // User settings
    string Uid { get; set; }
    string Did { get; set; }
    string Platform { get; set; }
    string SdkVersion { get; set; }
    string AppVersion { get; set; }

    // Buffer settings
    int BufferSize { get; set; }
    int BufferTimeoutMs { get; set; }
    int ChunkSizeBytes { get; set; }

    // Result processing
    float ConfidenceThreshold { get; set; }

    // Server settings
    string ListenUrl { get; set; }

    /// <summary>
    /// Validates the configuration
    /// </summary>
    void Validate();

    /// <summary>
    /// Creates a DoubaoVoiceConfig from this options instance
    /// </summary>
    DoubaoVoice.SDK.DoubaoVoiceConfig ToDoubaoVoiceConfig();
}
