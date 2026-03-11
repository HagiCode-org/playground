using CodeBuddySdk.Artifacts;
using CodeBuddySdk.Configuration;
using CodeBuddySdk.Runtime;

namespace CodeBuddySdk.Scenarios;

public sealed class ScenarioContext
{
    public required CodeBuddyRunOptions Options { get; init; }

    public required CodeBuddyProcessClient Client { get; init; }

    public required RunArtifactWriter ArtifactWriter { get; init; }
}
