namespace KimiAcpSdk.Configuration;

public sealed class KimiClientOptions
{
    public string Name { get; set; } = "KimiAcpDotnet";

    public string Version { get; set; } = "0.1.0";

    public int ProtocolVersion { get; set; } = 1;

    public KimiClientOptions Clone()
    {
        return new KimiClientOptions
        {
            Name = Name,
            Version = Version,
            ProtocolVersion = ProtocolVersion,
        };
    }
}
