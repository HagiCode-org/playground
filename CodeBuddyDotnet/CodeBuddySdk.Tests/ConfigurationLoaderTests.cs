using CodeBuddySdk.Configuration;

namespace CodeBuddySdk.Tests;

public sealed class ConfigurationLoaderTests
{
    [Fact]
    public void Load_ResolvesRelativePathsFromCurrentDirectory()
    {
        var tempDirectory = CreateTempDirectory();
        var originalDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDirectory);

        try
        {
            File.WriteAllText(
                Path.Combine(tempDirectory, "appsettings.json"),
                """
                {
                  "CliPath": "codebuddy",
                  "WorkingDirectory": ".",
                  "RunStorePath": "./artifacts",
                  "Selection": {
                    "Mode": "Fixture"
                  }
                }
                """);

            var options = ConfigurationLoader.Load();

            Assert.Equal(tempDirectory, options.WorkingDirectory);
            Assert.Equal(Path.Combine(tempDirectory, "artifacts"), options.RunStorePath);
            Assert.Equal(ExecutionMode.Fixture, options.Selection.Mode);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "codebuddy-dotnet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
