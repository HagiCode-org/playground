using CodeBuddySdk.Configuration;

namespace CodeBuddySdk.Runtime;

public sealed class CodeBuddyExecutionResult
{
    public required string ScenarioName { get; init; }

    public required ExecutionMode Mode { get; init; }

    public required string Prompt { get; init; }

    public bool Success { get; init; }

    public string FinalContent { get; init; } = string.Empty;

    public ProcessFailureCategory FailureCategory { get; init; }

    public string? FailureMessage { get; init; }

    public int? ExitCode { get; init; }

    public TimeSpan Duration { get; init; }

    public IReadOnlyList<NormalizedEvent> Events { get; init; } = Array.Empty<NormalizedEvent>();

    public string Transcript { get; init; } = string.Empty;

    public string StdErr { get; init; } = string.Empty;

    public string CommandDescription { get; init; } = string.Empty;
}
