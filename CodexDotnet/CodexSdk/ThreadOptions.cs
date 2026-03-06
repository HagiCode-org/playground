namespace CodexSdk;

public sealed class ThreadOptions
{
    public string? Model { get; init; }

    public string? SandboxMode { get; init; }

    public string? WorkingDirectory { get; init; }

    public bool? SkipGitRepoCheck { get; init; }

    public string? ModelReasoningEffort { get; init; }

    public bool? NetworkAccessEnabled { get; init; }

    public string? WebSearchMode { get; init; }

    public bool? WebSearchEnabled { get; init; }

    public string? ApprovalPolicy { get; init; }

    public IReadOnlyList<string>? AdditionalDirectories { get; init; }
}
