namespace CodeBuddySdk.Artifacts;

public sealed class RunArtifactRecord
{
    public required string RunDirectory { get; init; }

    public required string SummaryPath { get; init; }

    public string? TranscriptPath { get; init; }

    public string? ErrorPath { get; init; }
}
