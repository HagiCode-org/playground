namespace HermesAcpSdk.Tests;

public sealed class RunArtifactWriterTests
{
    [Fact]
    public async Task WriteAsync_PersistsSummaryMarkdownAndTranscript()
    {
        var root = Path.Combine(Path.GetTempPath(), "hermes-run-writer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var result = new HermesRunResult
            {
                ProfileName = "fixture",
                EffectiveLaunch = new HermesLaunchSummary(
                    "hermes",
                    ["acp", "--transport", "stdio"],
                    Directory.GetCurrentDirectory(),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["HERMES_TOKEN"] = "***REDACTED***",
                    },
                    root,
                    30),
                StartedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
                CompletedAt = DateTimeOffset.UtcNow,
                Features = [FeatureCheckResult.Passed("initialize", TimeSpan.FromSeconds(1), "ok")],
                Transcript = [new TranscriptEntry(DateTimeOffset.UtcNow, TranscriptChannel.Inbound, "//ready")],
                PromptText = "hello",
                Initialize = new HermesInitializeResult(false, [new HermesAuthMethod("token")], JsonDocument.Parse("{}").RootElement),
                Session = new HermesSessionStartResult("session-1", JsonDocument.Parse("{}").RootElement),
            };

            var artifact = await new RunArtifactWriter().WriteAsync(result);

            Assert.True(File.Exists(artifact.SummaryPath));
            Assert.True(File.Exists(artifact.MarkdownReportPath));
            Assert.True(File.Exists(artifact.TranscriptLogPath));
            Assert.Contains("***REDACTED***", await File.ReadAllTextAsync(artifact.SummaryPath));
            Assert.Contains("session-1", await File.ReadAllTextAsync(artifact.MarkdownReportPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
