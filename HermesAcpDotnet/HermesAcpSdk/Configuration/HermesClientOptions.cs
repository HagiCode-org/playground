namespace HermesAcpSdk.Configuration;

public sealed class HermesClientOptions
{
    public string Name { get; set; } = "HermesAcpDotnet";

    public string Version { get; set; } = "0.1.0";

    public int ProtocolVersion { get; set; } = 1;

    public HermesClientOptions Clone()
    {
        return new HermesClientOptions
        {
            Name = Name,
            Version = Version,
            ProtocolVersion = ProtocolVersion,
        };
    }
}
