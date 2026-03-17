namespace HermesAcpSdk.Runtime;

public sealed class HermesRunRequest
{
    public string ProfileName { get; init; } = string.Empty;

    public string? Prompt { get; init; }

    public IReadOnlyList<string> ArgumentOverrides { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string?> EnvironmentOverrides { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public string? WorkingDirectoryOverride { get; init; }

    public string? ArtifactOutputOverride { get; init; }

    public string? AuthMethodOverride { get; init; }
}
