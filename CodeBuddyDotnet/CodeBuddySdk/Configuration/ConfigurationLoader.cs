using Microsoft.Extensions.Configuration;

namespace CodeBuddySdk.Configuration;

public static class ConfigurationLoader
{
    public static CodeBuddyRunOptions Load(string? configPath = null)
    {
        var basePath = Directory.GetCurrentDirectory();
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables(prefix: "CODEBUDDY_");

        if (!string.IsNullOrWhiteSpace(configPath))
        {
            var resolvedPath = Path.IsPathRooted(configPath)
                ? configPath
                : Path.GetFullPath(configPath, basePath);

            builder.AddJsonFile(resolvedPath, optional: false);
        }

        var configuration = builder.Build();
        var options = new CodeBuddyRunOptions();
        configuration.Bind(options);

        options.Selection ??= new ScenarioSelectionOptions();
        options.Arguments ??= [];
        options.EnvironmentVariables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        options.Selection.ScenarioNames ??= [];

        if (!Path.IsPathRooted(options.WorkingDirectory))
        {
            options.WorkingDirectory = Path.GetFullPath(options.WorkingDirectory, basePath);
        }

        if (!Path.IsPathRooted(options.RunStorePath))
        {
            options.RunStorePath = Path.GetFullPath(options.RunStorePath, basePath);
        }

        return options;
    }
}
