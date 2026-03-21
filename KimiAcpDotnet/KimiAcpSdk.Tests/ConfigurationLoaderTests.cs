namespace KimiAcpSdk.Tests;

public sealed class ConfigurationLoaderTests
{
    [Fact]
    public void Load_BindsProfilesAndResolvesRelativePaths()
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
                  "ActiveProfile": "fixture",
                  "Profiles": {
                    "fixture": {
                      "ExecutablePath": "kimi",
                      "Arguments": ["acp", "--transport", "stdio"],
                      "WorkingDirectory": "../workspace",
                      "Artifacts": {
                        "RunStorePath": "./artifacts"
                      },
                      "SessionDefaults": {
                        "Model": "kimi-default",
                        "ModeId": "analysis"
                      }
                    }
                  }
                }
                """);

            var options = ConfigurationLoader.Load();
            var profile = options.ResolveProfile(new KimiRunRequest { ProfileName = "fixture" });

            Assert.Equal("kimi", profile.ExecutablePath);
            Assert.Equal(Path.GetFullPath("../workspace", tempDirectory), profile.WorkingDirectory);
            Assert.Equal(Path.Combine(tempDirectory, "artifacts"), profile.Artifacts.RunStorePath);
            Assert.Equal("kimi-default", profile.SessionDefaults.Model);
            Assert.Equal("analysis", profile.SessionDefaults.ModeId);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "kimi-acp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
