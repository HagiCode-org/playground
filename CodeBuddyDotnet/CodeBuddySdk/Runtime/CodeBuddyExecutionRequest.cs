using CodeBuddySdk.Configuration;

namespace CodeBuddySdk.Runtime;

public sealed class CodeBuddyExecutionRequest
{
    public required string ScenarioName { get; init; }

    public required string Prompt { get; init; }

    public ExecutionMode Mode { get; init; }

    public TimeSpan Timeout { get; init; }
}
