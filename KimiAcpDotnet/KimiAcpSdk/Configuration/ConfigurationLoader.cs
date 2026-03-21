using Microsoft.Extensions.Configuration;

namespace KimiAcpSdk.Configuration;

public static class ConfigurationLoader
{
    public static KimiAcpOptions Load(string? configPath = null)
    {
        var basePath = Directory.GetCurrentDirectory();
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables(prefix: "KIMI_ACP_");

        if (!string.IsNullOrWhiteSpace(configPath))
        {
            var resolvedPath = Path.IsPathRooted(configPath)
                ? configPath
                : Path.GetFullPath(configPath, basePath);
            builder.AddJsonFile(resolvedPath, optional: false);
        }

        var configuration = builder.Build();
        var options = new KimiAcpOptions();
        configuration.Bind(options);
        options.Normalize(basePath);
        return options;
    }
}
