using CodeBuddySdk.Scenarios.Definitions;

namespace CodeBuddySdk.Scenarios;

public static class ScenarioCatalog
{
    public static IReadOnlyList<IScenarioDefinition> CreateDefault()
    {
        return
        [
            new StartupSmokeScenario(),
            new ToolEventObservationScenario(),
            new AuthClassificationScenario(),
            new PromptSuccessScenario(),
            new TimeoutClassificationScenario(),
        ];
    }
}
