using CodeBuddySdk.Configuration;

namespace CodeBuddySdk.Scenarios;

public interface IScenarioDefinition
{
    string Name { get; }

    string Description { get; }

    ExecutionMode Mode { get; }

    Task<ScenarioOutcome> ExecuteAsync(ScenarioContext context, CancellationToken cancellationToken);
}
