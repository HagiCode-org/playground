using DoubaoVoice.SDK;

namespace DoubaoVoice.WebProxy.Services.Configuration;

/// <summary>
/// Configuration options for DoubaoVoice.WebProxy
/// </summary>
public class DoubaoProxyOptions : IDoubaoProxyOptions
{
    // Doubao API credentials
    public string AppId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async";
    public string ResourceId { get; set; } = "volc.bigasr.sauc.duration";

    // Audio settings
    public int SampleRate { get; set; } = 16000;
    public int BitsPerSample { get; set; } = 16;
    public int Channels { get; set; } = 1;
    public string AudioFormat { get; set; } = "wav";
    public string AudioCodec { get; set; } = "raw";

    // Recognition settings
    public string ModelName { get; set; } = "bigmodel";
    public bool EnableITN { get; set; } = true;
    public bool EnablePunctuation { get; set; } = true;
    public bool EnableDDC { get; set; } = true;
    public bool ShowUtterances { get; set; } = true;
    public bool EnableNonstream { get; set; } = false;

    // User settings
    public string Uid { get; set; } = "demo_uid";
    public string Did { get; set; } = string.Empty;
    public string Platform { get; set; } = "dotnet";
    public string SdkVersion { get; set; } = "1.0.0";
    public string AppVersion { get; set; } = "1.0.0";

    // Buffer settings
    public int BufferSize { get; set; } = 10;
    public int BufferTimeoutMs { get; set; } = 5000;
    public int ChunkSizeBytes { get; set; } = 3200;

    // Result processing
    public float ConfidenceThreshold { get; set; } = 0.5f;

    // Server settings
    public string ListenUrl { get; set; } = "http://localhost:5000";

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

        if (BufferSize <= 0)
            throw new ArgumentException("BufferSize must be greater than 0", nameof(BufferSize));

        if (ChunkSizeBytes <= 0)
            throw new ArgumentException("ChunkSizeBytes must be greater than 0", nameof(ChunkSizeBytes));
    }

    /// <summary>
    /// Creates a DoubaoVoiceConfig from this options instance
    /// </summary>
    public DoubaoVoiceConfig ToDoubaoVoiceConfig()
    {
        return new DoubaoVoiceConfig
        {
            AppId = AppId,
            AccessToken = AccessToken,
            ServiceUrl = ServiceUrl,
            ResourceId = ResourceId,
            SampleRate = SampleRate,
            BitsPerSample = BitsPerSample,
            Channels = Channels,
            AudioFormat = AudioFormat,
            AudioCodec = AudioCodec,
            ModelName = ModelName,
            EnableITN = EnableITN,
            EnablePunctuation = EnablePunctuation,
            EnableDDC = EnableDDC,
            ShowUtterances = ShowUtterances,
            EnableNonstream = EnableNonstream,
            Uid = Uid,
            Did = Did,
            Platform = Platform,
            SdkVersion = SdkVersion,
            AppVersion = AppVersion
        };
    }
}
