namespace KimiAcpSdk.Configuration;

public sealed class KimiAuthenticationOptions
{
    public string? PreferredMethodId { get; set; }

    public Dictionary<string, string?> MethodInfo { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public KimiAuthenticationOptions Clone()
    {
        return new KimiAuthenticationOptions
        {
            PreferredMethodId = PreferredMethodId,
            MethodInfo = new Dictionary<string, string?>(MethodInfo, StringComparer.OrdinalIgnoreCase),
        };
    }

    public string? ResolveMethodId(IReadOnlyList<Protocol.KimiAuthMethod> advertisedMethods)
    {
        if (!string.IsNullOrWhiteSpace(PreferredMethodId) &&
            advertisedMethods.Any(method => string.Equals(method.Id, PreferredMethodId, StringComparison.OrdinalIgnoreCase)))
        {
            return PreferredMethodId;
        }

        return advertisedMethods.FirstOrDefault()?.Id;
    }
}
