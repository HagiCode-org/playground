namespace CodeBuddySdk.Configuration;

public sealed class CodeBuddyRunOptions
{
    public string CliPath { get; set; } = "codebuddy";

    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();

    public string RunStorePath { get; set; } = "./.codebuddy-runs";

    public bool EnableLiveScenarios { get; set; }

    public PromptTransport PromptTransport { get; set; } = PromptTransport.Stdin;

    public List<string> Arguments { get; set; } = [];

    public int StartupTimeoutSeconds { get; set; } = 10;

    public int CommandTimeoutSeconds { get; set; } = 60;

    public Dictionary<string, string> EnvironmentVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ScenarioSelectionOptions Selection { get; set; } = new();

    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(CliPath))
        {
            errors.Add("CliPath is required.");
        }

        if (string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            errors.Add("WorkingDirectory is required.");
        }
        else if (!Directory.Exists(WorkingDirectory))
        {
            errors.Add($"WorkingDirectory does not exist: {WorkingDirectory}");
        }

        if (string.IsNullOrWhiteSpace(RunStorePath))
        {
            errors.Add("RunStorePath is required.");
        }

        if (StartupTimeoutSeconds <= 0)
        {
            errors.Add("StartupTimeoutSeconds must be greater than 0.");
        }

        if (CommandTimeoutSeconds <= 0)
        {
            errors.Add("CommandTimeoutSeconds must be greater than 0.");
        }

        if (Selection is null)
        {
            errors.Add("Selection is required.");
        }

        if (PromptTransport == PromptTransport.Arguments
            && Arguments.Count > 0
            && !Arguments.Any(static arg => arg.Contains("{prompt}", StringComparison.Ordinal) || arg.Contains("{promptFile}", StringComparison.Ordinal)))
        {
            errors.Add("Arguments mode requires at least one argument containing {prompt} or {promptFile}.");
        }

        return errors;
    }

    public string ResolveRunStorePath()
    {
        return Path.GetFullPath(RunStorePath, Directory.GetCurrentDirectory());
    }
}
