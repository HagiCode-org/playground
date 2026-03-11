using CodeBuddySdk.Artifacts;
using CodeBuddySdk.Configuration;
using CodeBuddySdk.Runtime;

namespace CodeBuddySdk.Scenarios;

public sealed class ScenarioOutcome
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required ExecutionMode Mode { get; init; }

    public required ScenarioStatus Status { get; init; }

    public required string Summary { get; init; }

    public CodeBuddyExecutionResult? ExecutionResult { get; init; }

    public RunArtifactRecord? ArtifactRecord { get; set; }
}
