namespace HermesAcpSdk.Configuration;

public sealed class HermesLaunchProfile
{
    public string ExecutablePath { get; set; } = "hermes";

    public List<string> Arguments { get; set; } = [];

    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();

    public Dictionary<string, string?> EnvironmentVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string DefaultPrompt { get; set; } = "Summarize the repository layout.";

    public int TimeoutSeconds { get; set; } = 30;

    public HermesClientOptions Client { get; set; } = new();

    public HermesAuthenticationOptions Authentication { get; set; } = new();

    public HermesSessionDefaults SessionDefaults { get; set; } = new();

    public HermesArtifactOptions Artifacts { get; set; } = new();

    public HermesLaunchProfile Clone()
    {
        return new HermesLaunchProfile
        {
            ExecutablePath = ExecutablePath,
            Arguments = [.. Arguments],
            WorkingDirectory = WorkingDirectory,
            EnvironmentVariables = new Dictionary<string, string?>(EnvironmentVariables, StringComparer.OrdinalIgnoreCase),
            DefaultPrompt = DefaultPrompt,
            TimeoutSeconds = TimeoutSeconds,
            Client = Client.Clone(),
            Authentication = Authentication.Clone(),
            SessionDefaults = SessionDefaults.Clone(),
            Artifacts = Artifacts.Clone(),
        };
    }

    public void ResolvePaths(string basePath)
    {
        if (!Path.IsPathRooted(WorkingDirectory))
        {
            WorkingDirectory = Path.GetFullPath(WorkingDirectory, basePath);
        }

        if (!Path.IsPathRooted(Artifacts.RunStorePath))
        {
            Artifacts.RunStorePath = Path.GetFullPath(Artifacts.RunStorePath, basePath);
        }
    }

    public HermesLaunchSummary ToSummary()
    {
        return new HermesLaunchSummary(
            ExecutablePath,
            Arguments.ToArray(),
            WorkingDirectory,
            EnvironmentVariables.ToDictionary(static pair => pair.Key, static pair => RedactSensitiveValue(pair.Key, pair.Value), StringComparer.OrdinalIgnoreCase),
            Artifacts.RunStorePath,
            TimeoutSeconds);
    }

    private static string RedactSensitiveValue(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var upperKey = key.ToUpperInvariant();
        if (upperKey.Contains("KEY", StringComparison.Ordinal) ||
            upperKey.Contains("TOKEN", StringComparison.Ordinal) ||
            upperKey.Contains("SECRET", StringComparison.Ordinal) ||
            upperKey.Contains("PASSWORD", StringComparison.Ordinal))
        {
            return "***REDACTED***";
        }

        return value;
    }
}
