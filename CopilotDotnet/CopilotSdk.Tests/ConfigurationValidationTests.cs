using CopilotSdk.Configuration;
using FluentAssertions;

namespace CopilotSdk.Tests;

public sealed class ConfigurationValidationTests
{
    [Fact]
    public void Validate_ShouldFail_WhenModelIsMissing()
    {
        var settings = new CopilotPlaygroundSettings
        {
            Model = string.Empty,
            RunStorePath = "/tmp/runs",
            UseLoggedInUser = true,
            TimeoutSeconds = 60,
        };

        var errors = settings.Validate();

        errors.Should().ContainSingle(x => x.Contains("Model is required", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ShouldFail_WhenRunStorePathIsMissing()
    {
        var settings = new CopilotPlaygroundSettings
        {
            Model = "gpt-5",
            RunStorePath = string.Empty,
            UseLoggedInUser = true,
            TimeoutSeconds = 60,
        };

        var errors = settings.Validate();

        errors.Should().ContainSingle(x => x.Contains("RunStorePath is required", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenTokenModeIsConfigured()
    {
        var settings = new CopilotPlaygroundSettings
        {
            Model = "gpt-5",
            RunStorePath = "/tmp/runs",
            UseLoggedInUser = false,
            GitHubToken = "ghp_dummy",
            TimeoutSeconds = 60,
        };

        var errors = settings.Validate();

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Load_ShouldApplyEnvironmentOverrides()
    {
        var env = new Dictionary<string, string?>
        {
            ["Model"] = "claude-sonnet-4.5",
            ["RunStorePath"] = "/tmp/override-runs",
            ["UseLoggedInUser"] = "true",
            ["TimeoutSeconds"] = "30",
        };

        var settings = ConfigurationLoader.Load(configPath: null, envOverride: env);

        settings.Model.Should().Be("claude-sonnet-4.5");
        settings.RunStorePath.Should().Be("/tmp/override-runs");
        settings.TimeoutSeconds.Should().Be(30);
    }
}
