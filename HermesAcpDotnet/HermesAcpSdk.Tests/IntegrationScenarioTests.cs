namespace HermesAcpSdk.Tests;

public sealed class IntegrationScenarioTests
{
    [Fact]
    public async Task LiveAdapter_BootstrapsAndPromptsWhenOptedIn()
    {
        if (!IntegrationTestSupport.IsOptedIn("HERMES_INTEGRATION"))
        {
            return;
        }

        var configuration = ResolveIntegrationLaunch();
        if (configuration is null)
        {
            return;
        }

        var tempArtifacts = IntegrationTestSupport.CreateTempDirectory("hermes-live-runs");

        try
        {
            var options = new HermesAcpOptions
            {
                ActiveProfile = "integration",
                Profiles = new Dictionary<string, HermesLaunchProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["integration"] = new HermesLaunchProfile
                    {
                        ExecutablePath = configuration.Executable,
                        Arguments = [.. configuration.Arguments],
                        WorkingDirectory = Directory.GetCurrentDirectory(),
                        DefaultPrompt = "Return a short readiness acknowledgement.",
                        Artifacts = new HermesArtifactOptions
                        {
                            RunStorePath = tempArtifacts,
                        },
                    },
                },
            };
            options.Normalize(Directory.GetCurrentDirectory());

            var result = await new HermesValidationRunner(options).RunAsync(new HermesRunRequest
            {
                ProfileName = "integration",
                Prompt = "Return the phrase 'adapter ok'.",
            });

            Assert.DoesNotContain(result.Features, feature => feature.Status == FeatureStatus.Failed);
            Assert.Equal("integration", result.ProfileName);
            Assert.NotNull(result.Session);
            Assert.Equal("adapter ok", result.Prompt?.FinalText);
            Assert.NotNull(result.Artifact);
            Assert.True(File.Exists(result.Artifact!.SummaryPath));
        }
        finally
        {
            Directory.Delete(tempArtifacts, recursive: true);
        }
    }

    private static IntegrationLaunchConfiguration? ResolveIntegrationLaunch()
    {
        var configuredExecutable = Environment.GetEnvironmentVariable("HERMES_INTEGRATION_EXECUTABLE");
        if (!string.IsNullOrWhiteSpace(configuredExecutable))
        {
            var args = (Environment.GetEnvironmentVariable("HERMES_INTEGRATION_ARGS") ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new IntegrationLaunchConfiguration(configuredExecutable, args);
        }

        if (!IntegrationTestSupport.CanRun("python3", "--version"))
        {
            return null;
        }

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "fake_hermes_adapter.py");
        return new IntegrationLaunchConfiguration("python3", [scriptPath]);
    }

    private sealed record IntegrationLaunchConfiguration(string Executable, IReadOnlyList<string> Arguments);
}
