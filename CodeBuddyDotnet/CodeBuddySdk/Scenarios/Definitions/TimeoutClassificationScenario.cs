using CodeBuddySdk.Configuration;
using CodeBuddySdk.Runtime;

namespace CodeBuddySdk.Scenarios.Definitions;

public sealed class TimeoutClassificationScenario : IScenarioDefinition
{
    public string Name => "timeout-classification";

    public string Description => "Force a very small timeout in live mode and verify the result is classified as timeout.";

    public ExecutionMode Mode => ExecutionMode.Live;

    public async Task<ScenarioOutcome> ExecuteAsync(ScenarioContext context, CancellationToken cancellationToken)
    {
        var request = new CodeBuddyExecutionRequest
        {
            ScenarioName = Name,
            Prompt = "This request intentionally uses a tiny timeout. If you see this, answer with TIMEOUT TEST.",
            Mode = Mode,
            Timeout = TimeSpan.FromMilliseconds(1),
        };

        var result = await context.Client.ExecuteLiveAsync(context.Options, request, cancellationToken);
        var passed = !result.Success && result.FailureCategory == ProcessFailureCategory.Timeout;

        return new ScenarioOutcome
        {
            Name = Name,
            Description = Description,
            Mode = Mode,
            Status = passed ? ScenarioStatus.Passed : ScenarioStatus.Failed,
            Summary = passed ? "Timeouts are classified predictably in live mode." : $"Expected timeout classification but received {result.FailureCategory}.",
            ExecutionResult = result,
        };
    }
}
