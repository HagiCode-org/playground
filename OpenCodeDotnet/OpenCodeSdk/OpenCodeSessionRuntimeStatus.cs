namespace OpenCodeSdk;

public sealed class OpenCodeSessionRuntimeStatus
{
    public string SessionId { get; init; } = string.Empty;

    public string? SessionTitle { get; init; }

    public Uri BaseUri { get; init; } = new("http://127.0.0.1:4096");

    public int? ProcessId { get; init; }

    public bool OwnsProcess { get; init; }

    public int MessageCount { get; init; }
}
