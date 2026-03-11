using CodeBuddySdk.Configuration;
using CodeBuddySdk.Runtime;

namespace CodeBuddySdk.Scenarios.Definitions;

public sealed class AuthClassificationScenario : IScenarioDefinition
{
    public string Name => "auth-classification";

    public string Description => "Use a fixture failure to validate authentication error classification without real credentials.";

    public ExecutionMode Mode => ExecutionMode.Fixture;

    public Task<ScenarioOutcome> ExecuteAsync(ScenarioContext context, CancellationToken cancellationToken)
    {
        var request = new CodeBuddyExecutionRequest
        {
            ScenarioName = Name,
            Prompt = "Fixture auth validation.",
            Mode = Mode,
            Timeout = TimeSpan.FromSeconds(context.Options.CommandTimeoutSeconds),
        };

        var result = context.Client.NormalizeFixture(request, new RawProcessResult
        {
            ExitCode = 1,
            Duration = TimeSpan.FromMilliseconds(30),
            StdErr = "Authentication failed: token expired. Run codebuddy login.",
            Events =
            [
                new RawProcessEvent(DateTimeOffset.UtcNow, "stderr", "Authentication failed: token expired. Run codebuddy login."),
            ],
            CommandDescription = "fixture://auth-classification",
        });

        var passed = !result.Success && result.FailureCategory == ProcessFailureCategory.Authentication;

        return Task.FromResult(new ScenarioOutcome
        {
            Name = Name,
            Description = Description,
            Mode = Mode,
            Status = passed ? ScenarioStatus.Passed : ScenarioStatus.Failed,
            Summary = passed ? "Authentication failures are normalized predictably." : "Authentication failure was not classified as expected.",
            ExecutionResult = result,
        });
    }
}
