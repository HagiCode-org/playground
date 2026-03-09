namespace OpenCodeSdk;

public sealed class OpenCodeSessionOptions
{
    public Uri? BaseUri { get; init; }

    public string? Directory { get; init; }

    public string? Workspace { get; init; }

    public string? SessionTitle { get; init; }

    public OpenCodeProcessOptions Process { get; init; } = new();
}
