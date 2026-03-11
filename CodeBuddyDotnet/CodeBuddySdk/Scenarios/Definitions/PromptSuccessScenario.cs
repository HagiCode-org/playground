using CodeBuddySdk.Configuration;
using CodeBuddySdk.Runtime;

namespace CodeBuddySdk.Scenarios.Definitions;

public sealed class PromptSuccessScenario : IScenarioDefinition
{
    public string Name => "prompt-success";

    public string Description => "Send a real prompt to CodeBuddy and require non-empty output.";

    public ExecutionMode Mode => ExecutionMode.Live;

    public async Task<ScenarioOutcome> ExecuteAsync(ScenarioContext context, CancellationToken cancellationToken)
    {
        var request = new CodeBuddyExecutionRequest
        {
            ScenarioName = Name,
            Prompt = "Reply with READY CODEBUDDY and one short sentence about the current workspace.",
            Mode = Mode,
            Timeout = TimeSpan.FromSeconds(context.Options.CommandTimeoutSeconds),
        };

        var result = await context.Client.ExecuteLiveAsync(context.Options, request, cancellationToken);
        var passed = result.Success && !string.IsNullOrWhiteSpace(result.FinalContent);

        return new ScenarioOutcome
        {
            Name = Name,
            Description = Description,
            Mode = Mode,
            Status = passed ? ScenarioStatus.Passed : ScenarioStatus.Failed,
            Summary = passed ? "Live prompt returned non-empty content." : result.FailureMessage ?? "Live prompt execution failed.",
            ExecutionResult = result,
        };
    }
}
