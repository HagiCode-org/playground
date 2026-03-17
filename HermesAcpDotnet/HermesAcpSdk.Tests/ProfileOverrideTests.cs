namespace HermesAcpSdk.Tests;

public sealed class ProfileOverrideTests
{
    [Fact]
    public void ResolveProfile_AppliesArgumentAndEnvironmentOverrides()
    {
        var options = new HermesAcpOptions
        {
            ActiveProfile = "local",
            Profiles = new Dictionary<string, HermesLaunchProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["local"] = new HermesLaunchProfile
                {
                    Arguments = ["acp", "--transport", "stdio"],
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    EnvironmentVariables = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["HERMES_MODE"] = "default",
                    },
                    Artifacts = new HermesArtifactOptions
                    {
                        RunStorePath = Path.Combine(Path.GetTempPath(), "hermes-artifacts"),
                    },
                },
            },
        };
        options.Normalize(Directory.GetCurrentDirectory());

        var profile = options.ResolveProfile(new HermesRunRequest
        {
            ProfileName = "local",
            ArgumentOverrides = ["acp", "--transport", "stdio", "--model", "custom"],
            WorkingDirectoryOverride = Path.GetTempPath(),
            ArtifactOutputOverride = Path.Combine(Path.GetTempPath(), "custom-artifacts"),
            EnvironmentOverrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["HERMES_MODE"] = "custom",
                ["HERMES_TOKEN"] = "secret-token",
            },
        });

        Assert.Equal(["acp", "--transport", "stdio", "--model", "custom"], profile.Arguments);
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()), profile.WorkingDirectory);
        Assert.Equal(Path.Combine(Path.GetTempPath(), "custom-artifacts"), profile.Artifacts.RunStorePath);
        Assert.Equal("custom", profile.EnvironmentVariables["HERMES_MODE"]);
        Assert.Equal("***REDACTED***", profile.ToSummary().EnvironmentVariables["HERMES_TOKEN"]);
    }
}
