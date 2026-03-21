namespace KimiAcpSdk.Tests;

public sealed class RunArtifactWriterTests
{
    [Fact]
    public async Task WriteAsync_PersistsSummaryMarkdownAndTranscript()
    {
        var root = Path.Combine(Path.GetTempPath(), "kimi-run-writer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var result = new KimiRunResult
            {
                ProfileName = "fixture",
                EffectiveLaunch = new KimiLaunchSummary(
                    "kimi",
                    ["acp", "--transport", "stdio"],
                    Directory.GetCurrentDirectory(),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["KIMI_TOKEN"] = "***REDACTED***",
                    },
                    root,
                    30),
                StartedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
                CompletedAt = DateTimeOffset.UtcNow,
                Features = [FeatureCheckResult.Passed("initialize", TimeSpan.FromSeconds(1), "ok")],
                Transcript = [new TranscriptEntry(DateTimeOffset.UtcNow, TranscriptChannel.Inbound, "//ready")],
                PromptText = "hello",
                Initialize = new KimiInitializeResult(false, [new KimiAuthMethod("token")], JsonDocument.Parse("{}").RootElement),
                Session = new KimiSessionStartResult("session-1", JsonDocument.Parse("{}").RootElement),
            };

            var artifact = await new RunArtifactWriter().WriteAsync(result);

            Assert.True(File.Exists(artifact.SummaryPath));
            Assert.True(File.Exists(artifact.MarkdownReportPath));
            Assert.True(File.Exists(artifact.TranscriptLogPath));
            Assert.Null(artifact.DiagnosticsPath);
            Assert.Contains("***REDACTED***", await File.ReadAllTextAsync(artifact.SummaryPath));
            Assert.Contains("session-1", await File.ReadAllTextAsync(artifact.MarkdownReportPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_PersistsDiagnostics_WhenRunFails()
    {
        var root = Path.Combine(Path.GetTempPath(), "kimi-run-writer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var result = new KimiRunResult
            {
                ProfileName = "fixture",
                EffectiveLaunch = new KimiLaunchSummary(
                    "kimi",
                    ["acp"],
                    Directory.GetCurrentDirectory(),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    root,
                    30),
                StartedAt = DateTimeOffset.UtcNow.AddSeconds(-2),
                CompletedAt = DateTimeOffset.UtcNow,
                Features = [FeatureCheckResult.Failed("session/prompt", TimeSpan.FromSeconds(1), "no usable text")],
                Transcript = [new TranscriptEntry(DateTimeOffset.UtcNow, TranscriptChannel.Stderr, "boom")],
                PromptText = "hello",
                FailureStage = "session/prompt",
                FailureMessage = "Prompt completed without usable text.",
                StandardError = "boom",
            };

            var artifact = await new RunArtifactWriter().WriteAsync(result);

            Assert.NotNull(artifact.DiagnosticsPath);
            Assert.True(File.Exists(artifact.DiagnosticsPath!));
            Assert.True(File.Exists(artifact.StandardErrorPath!));
            var diagnostics = await File.ReadAllTextAsync(artifact.DiagnosticsPath!);
            Assert.Contains("session/prompt", diagnostics);
            Assert.Contains("Prompt completed without usable text.", diagnostics);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
