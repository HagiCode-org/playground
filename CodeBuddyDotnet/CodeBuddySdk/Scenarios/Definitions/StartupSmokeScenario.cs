using CodeBuddySdk.Configuration;
using CodeBuddySdk.Runtime;

namespace CodeBuddySdk.Scenarios.Definitions;

public sealed class StartupSmokeScenario : IScenarioDefinition
{
    public string Name => "startup-smoke";

    public string Description => "Validate configuration defaults and prove the fixture pipeline can produce a passing result.";

    public ExecutionMode Mode => ExecutionMode.Fixture;

    public Task<ScenarioOutcome> ExecuteAsync(ScenarioContext context, CancellationToken cancellationToken)
    {
        var validationErrors = context.Options.Validate();
        if (validationErrors.Count > 0)
        {
            return Task.FromResult(new ScenarioOutcome
            {
                Name = Name,
                Description = Description,
                Mode = Mode,
                Status = ScenarioStatus.Failed,
                Summary = string.Join(" ", validationErrors),
            });
        }

        var request = new CodeBuddyExecutionRequest
        {
            ScenarioName = Name,
            Prompt = "Reply with READY.",
            Mode = Mode,
            Timeout = TimeSpan.FromSeconds(context.Options.StartupTimeoutSeconds),
        };

        var rawResult = new RawProcessResult
        {
            ExitCode = 0,
            Duration = TimeSpan.FromMilliseconds(50),
            StdOut = "READY\nConfiguration validated.\n",
            Events =
            [
                new RawProcessEvent(DateTimeOffset.UtcNow, "stdout", "READY"),
                new RawProcessEvent(DateTimeOffset.UtcNow.AddMilliseconds(5), "stdout", "Configuration validated."),
            ],
            CommandDescription = "fixture://startup-smoke",
        };

        var result = context.Client.NormalizeFixture(request, rawResult);
        var passed = result.Success && result.FinalContent.Contains("READY", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(new ScenarioOutcome
        {
            Name = Name,
            Description = Description,
            Mode = Mode,
            Status = passed ? ScenarioStatus.Passed : ScenarioStatus.Failed,
            Summary = passed ? "Fixture startup validation passed." : result.FailureMessage ?? "Startup smoke fixture failed.",
            ExecutionResult = result,
        });
    }
}
