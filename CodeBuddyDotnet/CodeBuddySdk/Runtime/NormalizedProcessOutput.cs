namespace CodeBuddySdk.Runtime;

public sealed class NormalizedProcessOutput
{
    public string FinalContent { get; init; } = string.Empty;

    public IReadOnlyList<NormalizedEvent> Events { get; init; } = Array.Empty<NormalizedEvent>();

    public string Transcript { get; init; } = string.Empty;
}
