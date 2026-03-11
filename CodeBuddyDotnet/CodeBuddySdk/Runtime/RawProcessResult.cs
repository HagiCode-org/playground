namespace CodeBuddySdk.Runtime;

public sealed class RawProcessResult
{
    public int? ExitCode { get; init; }

    public bool TimedOut { get; init; }

    public string StdOut { get; init; } = string.Empty;

    public string StdErr { get; init; } = string.Empty;

    public IReadOnlyList<RawProcessEvent> Events { get; init; } = Array.Empty<RawProcessEvent>();

    public string CommandDescription { get; init; } = string.Empty;

    public TimeSpan Duration { get; init; }

    public string? StartFailureMessage { get; init; }

    public ProcessFailureCategory StartFailureCategory { get; init; } = ProcessFailureCategory.None;
}
