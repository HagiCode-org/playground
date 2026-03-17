namespace HermesAcpSdk.Configuration;

public sealed class HermesArtifactOptions
{
    public string RunStorePath { get; set; } = "./.hermes-acp-runs";

    public HermesArtifactOptions Clone()
    {
        return new HermesArtifactOptions
        {
            RunStorePath = RunStorePath,
        };
    }
}
