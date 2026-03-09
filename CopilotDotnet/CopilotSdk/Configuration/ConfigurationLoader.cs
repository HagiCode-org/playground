using Microsoft.Extensions.Configuration;

namespace CopilotSdk.Configuration;

public static class ConfigurationLoader
{
    public static CopilotPlaygroundSettings Load(
        string? configPath = null,
        IReadOnlyDictionary<string, string?>? envOverride = null)
    {
        var effectivePath = string.IsNullOrWhiteSpace(configPath) ? "appsettings.json" : configPath;

        var builder = new ConfigurationBuilder();
        if (File.Exists(effectivePath))
        {
            builder.AddJsonFile(effectivePath, optional: false, reloadOnChange: false);
        }

        builder.AddEnvironmentVariables(prefix: "COPILOT_");

        if (envOverride is not null)
        {
            builder.AddInMemoryCollection(envOverride);
        }

        var configuration = builder.Build();
        var settings = configuration.Get<CopilotPlaygroundSettings>() ?? new CopilotPlaygroundSettings();

        if (string.IsNullOrWhiteSpace(settings.GitHubToken))
        {
            settings.GitHubToken = ReadFromOverrideOrEnvironment("GITHUB_TOKEN", envOverride);
        }

        if (string.IsNullOrWhiteSpace(settings.WorkingDirectory))
        {
            settings.WorkingDirectory = Directory.GetCurrentDirectory();
        }

        return settings;
    }

    private static string? ReadFromOverrideOrEnvironment(
        string key,
        IReadOnlyDictionary<string, string?>? envOverride)
    {
        if (envOverride is not null && envOverride.TryGetValue(key, out var value))
        {
            return value;
        }

        return Environment.GetEnvironmentVariable(key);
    }
}
