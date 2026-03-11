namespace CodeBuddySdk.Configuration;

public sealed class ScenarioSelectionOptions
{
    public ExecutionMode Mode { get; set; } = ExecutionMode.Fixture;

    public List<string> ScenarioNames { get; set; } = [];
}
