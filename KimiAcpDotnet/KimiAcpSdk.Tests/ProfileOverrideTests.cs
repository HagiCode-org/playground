namespace KimiAcpSdk.Tests;

public sealed class ProfileOverrideTests
{
    [Fact]
    public void ResolveProfile_AppliesArgumentAndEnvironmentOverrides()
    {
        var options = new KimiAcpOptions
        {
            ActiveProfile = "local",
            Profiles = new Dictionary<string, KimiLaunchProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["local"] = new KimiLaunchProfile
                {
                    Arguments = ["acp", "--transport", "stdio"],
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    EnvironmentVariables = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["KIMI_MODE"] = "default",
                    },
                    Artifacts = new KimiArtifactOptions
                    {
                        RunStorePath = Path.Combine(Path.GetTempPath(), "kimi-artifacts"),
                    },
                },
            },
        };
        options.Normalize(Directory.GetCurrentDirectory());

        var profile = options.ResolveProfile(new KimiRunRequest
        {
            ProfileName = "local",
            ArgumentOverrides = ["acp", "--transport", "stdio", "--model", "custom"],
            WorkingDirectoryOverride = Path.GetTempPath(),
            ArtifactOutputOverride = Path.Combine(Path.GetTempPath(), "custom-artifacts"),
            EnvironmentOverrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["KIMI_MODE"] = "custom",
                ["KIMI_TOKEN"] = "secret-token",
            },
        });

        Assert.Equal(["acp", "--transport", "stdio", "--model", "custom"], profile.Arguments);
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()), profile.WorkingDirectory);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "custom-artifacts"), profile.Artifacts.RunStorePath);
        Assert.Equal("custom", profile.EnvironmentVariables["KIMI_MODE"]);
        Assert.Equal("***REDACTED***", profile.ToSummary().EnvironmentVariables["KIMI_TOKEN"]);
    }
}
