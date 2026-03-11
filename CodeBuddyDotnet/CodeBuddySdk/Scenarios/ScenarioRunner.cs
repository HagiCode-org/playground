using CodeBuddySdk.Configuration;

namespace CodeBuddySdk.Scenarios;

public sealed class ScenarioRunner
{
    private readonly IReadOnlyList<IScenarioDefinition> _scenarios;
    private readonly ScenarioContext _context;

    public ScenarioRunner(IReadOnlyList<IScenarioDefinition> scenarios, ScenarioContext context)
    {
        _scenarios = scenarios;
        _context = context;
    }

    public async Task<IReadOnlyList<ScenarioOutcome>> RunAsync(ScenarioSelectionOptions selection, CancellationToken cancellationToken)
    {
        var requestedNames = selection.ScenarioNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedScenarios = _scenarios
            .Where(scenario => scenario.Mode == selection.Mode)
            .Where(scenario => requestedNames.Count == 0 || requestedNames.Contains(scenario.Name))
            .ToArray();

        var outcomes = new List<ScenarioOutcome>(selectedScenarios.Length);
        foreach (var scenario in selectedScenarios)
        {
            ScenarioOutcome outcome;
            if (scenario.Mode == ExecutionMode.Live && !_context.Options.EnableLiveScenarios)
            {
                outcome = new ScenarioOutcome
                {
                    Name = scenario.Name,
                    Description = scenario.Description,
                    Mode = scenario.Mode,
                    Status = ScenarioStatus.Skipped,
                    Summary = "Live scenarios are disabled. Set EnableLiveScenarios=true to opt in.",
                };
            }
            else
            {
                try
                {
                    outcome = await scenario.ExecuteAsync(_context, cancellationToken);
                }
                catch (Exception ex)
                {
                    outcome = new ScenarioOutcome
                    {
                        Name = scenario.Name,
                        Description = scenario.Description,
                        Mode = scenario.Mode,
                        Status = ScenarioStatus.Failed,
                        Summary = ex.Message,
                    };
                }
            }

            outcome.ArtifactRecord = await _context.ArtifactWriter.WriteAsync(_context.Options, outcome, cancellationToken);
            outcomes.Add(outcome);
        }

        return outcomes;
    }
}
