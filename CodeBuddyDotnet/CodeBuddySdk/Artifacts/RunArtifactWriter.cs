using System.Text.Json;
using System.Text.Json.Serialization;
using CodeBuddySdk.Configuration;
using CodeBuddySdk.Scenarios;

namespace CodeBuddySdk.Artifacts;

public sealed class RunArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<RunArtifactRecord> WriteAsync(CodeBuddyRunOptions options, ScenarioOutcome outcome, CancellationToken cancellationToken)
    {
        var root = options.ResolveRunStorePath();
        Directory.CreateDirectory(root);

        var safeScenarioName = string.Concat(outcome.Name.Select(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-'));
        var runDirectory = Path.Combine(root, $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{safeScenarioName}");
        Directory.CreateDirectory(runDirectory);

        var summaryPath = Path.Combine(runDirectory, "summary.json");
        string? transcriptPath = null;
        string? errorPath = null;

        if (outcome.ExecutionResult is not null)
        {
            transcriptPath = Path.Combine(runDirectory, "transcript.log");
            await File.WriteAllTextAsync(transcriptPath, outcome.ExecutionResult.Transcript, cancellationToken);

            if (!string.IsNullOrWhiteSpace(outcome.ExecutionResult.StdErr))
            {
                errorPath = Path.Combine(runDirectory, "stderr.log");
                await File.WriteAllTextAsync(errorPath, outcome.ExecutionResult.StdErr, cancellationToken);
            }
        }

        var summaryPayload = new
        {
            outcome.Name,
            outcome.Description,
            outcome.Mode,
            outcome.Status,
            outcome.Summary,
            ArtifactRecord = new { RunDirectory = runDirectory, TranscriptPath = transcriptPath, ErrorPath = errorPath },
            Execution = outcome.ExecutionResult is null
                ? null
                : new
                {
                    outcome.ExecutionResult.Success,
                    outcome.ExecutionResult.FinalContent,
                    outcome.ExecutionResult.FailureCategory,
                    outcome.ExecutionResult.FailureMessage,
                    outcome.ExecutionResult.ExitCode,
                    DurationMs = (long)outcome.ExecutionResult.Duration.TotalMilliseconds,
                    EventCount = outcome.ExecutionResult.Events.Count,
                    outcome.ExecutionResult.CommandDescription,
                },
        };

        await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(summaryPayload, JsonOptions), cancellationToken);

        return new RunArtifactRecord
        {
            RunDirectory = runDirectory,
            SummaryPath = summaryPath,
            TranscriptPath = transcriptPath,
            ErrorPath = errorPath,
        };
    }
}
