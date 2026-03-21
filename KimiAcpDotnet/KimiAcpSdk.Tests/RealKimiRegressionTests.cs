namespace KimiAcpSdk.Tests;

public sealed class RealKimiRegressionTests
{
    [Fact]
    public async Task PingPrompt_ReturnsPong_WhenRealKimiRegressionIsEnabled()
    {
        if (!IntegrationTestSupport.IsOptedIn("KIMI_REAL_REGRESSION"))
        {
            return;
        }

        var runStore = IntegrationTestSupport.CreateTempDirectory("kimi-real-regression");
        var shouldKeepArtifacts = IntegrationTestSupport.IsOptedIn("KIMI_KEEP_ARTIFACTS");
        var passed = false;

        try
        {
            var result = await RunAgainstRealKimiAsync(
                runStore,
                "Reply with exactly PONG.",
                timeoutSeconds: 120,
                CancellationToken.None);

            AssertSuccessfulRun(result, runStore);
            Assert.Equal("PONG", result.Prompt?.FinalText);
            Assert.Equal("end_turn", result.Prompt?.StopReason);
            Assert.Contains(result.Features, feature => feature.FeatureId == "session/prompt" && feature.Status == FeatureStatus.Passed);
            passed = true;
        }
        finally
        {
            if (passed && !shouldKeepArtifacts)
            {
                IntegrationTestSupport.DeleteDirectoryIfExists(runStore);
            }
        }
    }

    [Fact]
    public async Task RepositoryAnalysis_ReturnsRepoSpecificPaths_WhenRealKimiRegressionIsEnabled()
    {
        if (!IntegrationTestSupport.IsOptedIn("KIMI_REAL_REGRESSION"))
        {
            return;
        }

        var runStore = IntegrationTestSupport.CreateTempDirectory("kimi-real-regression");
        var shouldKeepArtifacts = IntegrationTestSupport.IsOptedIn("KIMI_KEEP_ARTIFACTS");

        const string prompt = """
            请用中文分析当前代码库，并满足以下要求：
            1. 输出要尽量简洁。
            2. 必须明确提到这些实际路径：AGENTS.md、repos/hagicode-core、repos/hagicode-desktop、repos/web。
            3. 说明仓库用途，并给一个新贡献者阅读这些路径的理由。
            """;
        var passed = false;

        try
        {
            var result = await RunAgainstRealKimiAsync(
                runStore,
                prompt,
                timeoutSeconds: 240,
                CancellationToken.None);

            AssertSuccessfulRun(result, runStore);
            Assert.Equal("end_turn", result.Prompt?.StopReason);
            Assert.False(string.IsNullOrWhiteSpace(result.Prompt?.FinalText));

            var content = result.Prompt!.FinalText!;
            Assert.Contains("AGENTS.md", content, StringComparison.Ordinal);
            Assert.Contains("repos/hagicode-core", content, StringComparison.Ordinal);
            Assert.Contains("repos/hagicode-desktop", content, StringComparison.Ordinal);
            Assert.Contains("repos/web", content, StringComparison.Ordinal);
            Assert.True(content.Length >= 80, $"Expected a meaningful repository analysis, but got: {content}");

            var reportContent = await File.ReadAllTextAsync(result.Artifact!.MarkdownReportPath, CancellationToken.None);
            Assert.Contains("Feature Matrix", reportContent, StringComparison.Ordinal);
            Assert.Contains("session/new", reportContent, StringComparison.Ordinal);
            passed = true;
        }
        finally
        {
            if (passed && !shouldKeepArtifacts)
            {
                IntegrationTestSupport.DeleteDirectoryIfExists(runStore);
            }
        }
    }

    private static async Task<KimiRunResult> RunAgainstRealKimiAsync(
        string runStore,
        string prompt,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var monorepoRoot = IntegrationTestSupport.FindMonorepoRoot();
        var executable = Environment.GetEnvironmentVariable("KIMI_REAL_EXECUTABLE") ?? "kimi";
        var args = (Environment.GetEnvironmentVariable("KIMI_REAL_ARGS") ?? "acp")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.True(IntegrationTestSupport.CanRun(executable, "--help"), $"Unable to launch Kimi executable '{executable}'.");

        var options = new KimiAcpOptions
        {
            ActiveProfile = "real-kimi",
            Profiles = new Dictionary<string, KimiLaunchProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["real-kimi"] = new KimiLaunchProfile
                {
                    ExecutablePath = executable,
                    Arguments = [.. args],
                    WorkingDirectory = monorepoRoot,
                    DefaultPrompt = prompt,
                    TimeoutSeconds = timeoutSeconds,
                    Authentication = new KimiAuthenticationOptions(),
                    SessionDefaults = new KimiSessionDefaults
                    {
                        ModeId = "analysis",
                        Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["approvalPolicy"] = "never",
                            ["workspaceScope"] = "monorepo",
                        },
                    },
                    Artifacts = new KimiArtifactOptions
                    {
                        RunStorePath = runStore,
                    },
                },
            },
        };
        options.Normalize(monorepoRoot);

        return await new KimiValidationRunner(options).RunAsync(
            new KimiRunRequest
            {
                ProfileName = "real-kimi",
                Prompt = prompt,
            },
            cancellationToken);
    }

    private static void AssertSuccessfulRun(KimiRunResult result, string runStore)
    {
        Assert.Equal("real-kimi", result.ProfileName);
        Assert.NotNull(result.Session);
        Assert.NotNull(result.Artifact);
        Assert.True(File.Exists(result.Artifact!.SummaryPath), $"Expected summary artifact in '{runStore}'.");
        Assert.True(File.Exists(result.Artifact.MarkdownReportPath), $"Expected markdown report in '{runStore}'.");
        Assert.True(File.Exists(result.Artifact.TranscriptLogPath), $"Expected transcript log in '{runStore}'.");
        Assert.DoesNotContain(result.Features, feature => feature.Status == FeatureStatus.Failed);
        Assert.Contains(result.Features, feature => feature.FeatureId == "initialize" && feature.Status == FeatureStatus.Passed);
        Assert.Contains(result.Features, feature => feature.FeatureId == "session/new" && feature.Status == FeatureStatus.Passed);
        Assert.True(result.Transcript.Count >= 4, $"Expected transcript frames to be captured in '{runStore}'.");
    }
}
