namespace KimiAcpSdk.Configuration;

public sealed class KimiArtifactOptions
{
    public string RunStorePath { get; set; } = "./.kimi-acp-runs";

    public KimiArtifactOptions Clone()
    {
        return new KimiArtifactOptions
        {
            RunStorePath = RunStorePath,
        };
    }
}
