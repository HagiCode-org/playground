using CodeBuddySdk.Configuration;
using CodeBuddySdk.Runtime;

namespace CodeBuddySdk.Scenarios.Definitions;

public sealed class ToolEventObservationScenario : IScenarioDefinition
{
    public string Name => "tool-event-observation";

    public string Description => "Use deterministic fixture events to validate event normalization and transcript capture.";

    public ExecutionMode Mode => ExecutionMode.Fixture;

    public Task<ScenarioOutcome> ExecuteAsync(ScenarioContext context, CancellationToken cancellationToken)
    {
        var request = new CodeBuddyExecutionRequest
        {
            ScenarioName = Name,
            Prompt = "Inspect README and report findings.",
            Mode = Mode,
            Timeout = TimeSpan.FromSeconds(context.Options.CommandTimeoutSeconds),
        };

        var events = new[]
        {
            new RawProcessEvent(DateTimeOffset.UtcNow, "stdout", "{\"type\":\"text\",\"content\":\"Planning analysis\"}"),
            new RawProcessEvent(DateTimeOffset.UtcNow.AddMilliseconds(5), "stdout", "{\"type\":\"tool_call\",\"name\":\"read_text_file\",\"arguments\":{\"path\":\"README.md\"}}"),
            new RawProcessEvent(DateTimeOffset.UtcNow.AddMilliseconds(10), "stdout", "{\"type\":\"final\",\"content\":\"Observation complete\"}"),
        };

        var result = context.Client.NormalizeFixture(request, new RawProcessResult
        {
            ExitCode = 0,
            Duration = TimeSpan.FromMilliseconds(80),
            StdOut = string.Join(Environment.NewLine, events.Select(static x => x.Text)),
            Events = events,
            CommandDescription = "fixture://tool-event-observation",
        });

        var passed = result.Success
            && result.Events.Any(static x => x.Kind == "tool_call")
            && result.Transcript.Contains("tool_call", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(new ScenarioOutcome
        {
            Name = Name,
            Description = Description,
            Mode = Mode,
            Status = passed ? ScenarioStatus.Passed : ScenarioStatus.Failed,
            Summary = passed ? "Fixture event observation captured tool events and transcript output." : result.FailureMessage ?? "Expected tool event was not captured.",
            ExecutionResult = result,
        });
    }
}
