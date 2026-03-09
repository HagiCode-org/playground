namespace CopilotSdk.Configuration;

public sealed class CopilotPlaygroundSettings
{
    public string Model { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string RunStorePath { get; set; } = string.Empty;

    public string? CliPath { get; set; }

    public string? CliUrl { get; set; }

    public bool UseLoggedInUser { get; set; } = true;

    public string? GitHubToken { get; set; }

    public bool UseSqlite { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 90;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Model))
        {
            errors.Add("Model is required. Set COPILOT_MODEL or configure Model in appsettings.json.");
        }

        if (string.IsNullOrWhiteSpace(RunStorePath))
        {
            errors.Add("RunStorePath is required. Set COPILOT_RUN_STORE_PATH or configure RunStorePath in appsettings.json.");
        }

        if (!UseLoggedInUser && string.IsNullOrWhiteSpace(GitHubToken))
        {
            errors.Add("GitHubToken is required when UseLoggedInUser is false.");
        }

        if (TimeoutSeconds <= 0)
        {
            errors.Add("TimeoutSeconds must be greater than zero.");
        }

        return errors;
    }
}
