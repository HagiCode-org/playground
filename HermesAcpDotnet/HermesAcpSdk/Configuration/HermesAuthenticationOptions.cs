namespace HermesAcpSdk.Configuration;

public sealed class HermesAuthenticationOptions
{
    public string? PreferredMethodId { get; set; }

    public Dictionary<string, string?> MethodInfo { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public HermesAuthenticationOptions Clone()
    {
        return new HermesAuthenticationOptions
        {
            PreferredMethodId = PreferredMethodId,
            MethodInfo = new Dictionary<string, string?>(MethodInfo, StringComparer.OrdinalIgnoreCase),
        };
    }

    public string? ResolveMethodId(IReadOnlyList<Protocol.HermesAuthMethod> advertisedMethods)
    {
        if (!string.IsNullOrWhiteSpace(PreferredMethodId) &&
            advertisedMethods.Any(method => string.Equals(method.Id, PreferredMethodId, StringComparison.OrdinalIgnoreCase)))
        {
            return PreferredMethodId;
        }

        return advertisedMethods.FirstOrDefault()?.Id;
    }
}
