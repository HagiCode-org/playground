using CodeBuddySdk.Artifacts;
using CodeBuddySdk.Configuration;
using CodeBuddySdk.Runtime;
using CodeBuddySdk.Scenarios;

namespace CodeBuddySdk.Tests;

public sealed class ArtifactWriterTests
{
    [Fact]
    public async Task WriteAsync_CreatesSummaryAndTranscript()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var writer = new RunArtifactWriter();
            var options = new CodeBuddyRunOptions
            {
                WorkingDirectory = tempDirectory,
                RunStorePath = Path.Combine(tempDirectory, "runs"),
            };
            var outcome = new ScenarioOutcome
            {
                Name = "startup-smoke",
                Description = "fixture",
                Mode = ExecutionMode.Fixture,
                Status = ScenarioStatus.Passed,
                Summary = "ok",
                ExecutionResult = new CodeBuddyExecutionResult
                {
                    ScenarioName = "startup-smoke",
                    Mode = ExecutionMode.Fixture,
                    Prompt = "READY",
                    Success = true,
                    FinalContent = "READY",
                    Duration = TimeSpan.FromMilliseconds(10),
                    Transcript = "[ts] stdout:text READY",
                },
            };

            var record = await writer.WriteAsync(options, outcome, CancellationToken.None);

            Assert.True(File.Exists(record.SummaryPath));
            Assert.True(File.Exists(record.TranscriptPath));
            var summary = await File.ReadAllTextAsync(record.SummaryPath);
            Assert.Contains("startup-smoke", summary, StringComparison.Ordinal);
        }
        finally
        {
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
