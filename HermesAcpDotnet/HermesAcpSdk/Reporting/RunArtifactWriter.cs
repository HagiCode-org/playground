using System.Text.Json.Serialization;
using HermesAcpSdk.Runtime;

namespace HermesAcpSdk.Reporting;

public sealed class RunArtifactWriter
{
    private static readonly JsonSerializerOptions ArtifactJsonOptions = new(HermesAcpSdk.Protocol.HermesJson.Default)
    {
        WriteIndented = true,
    };

    static RunArtifactWriter()
    {
        ArtifactJsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<HermesRunArtifact> WriteAsync(HermesRunResult result, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(result.EffectiveLaunch.ArtifactOutputPath);
        var runDirectory = Path.Combine(result.EffectiveLaunch.ArtifactOutputPath, $"{result.CompletedAt:yyyyMMddTHHmmssfff}-{Sanitize(result.ProfileName)}");
        Directory.CreateDirectory(runDirectory);

        var summaryPath = Path.Combine(runDirectory, "summary.json");
        var reportPath = Path.Combine(runDirectory, "report.md");
        var transcriptLogPath = Path.Combine(runDirectory, "transcript.log");
        var transcriptJsonPath = Path.Combine(runDirectory, "transcript.json");
        string? standardErrorPath = null;

        await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(result, ArtifactJsonOptions), cancellationToken);
        await File.WriteAllTextAsync(reportPath, MarkdownReportWriter.Build(result), cancellationToken);
        await File.WriteAllTextAsync(transcriptLogPath, string.Join(Environment.NewLine, result.Transcript.Select(static entry => $"[{entry.Timestamp:O}] {entry.Channel}: {entry.Content}")), cancellationToken);
        await File.WriteAllTextAsync(transcriptJsonPath, JsonSerializer.Serialize(result.Transcript, ArtifactJsonOptions), cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            standardErrorPath = Path.Combine(runDirectory, "stderr.log");
            await File.WriteAllTextAsync(standardErrorPath, result.StandardError, cancellationToken);
        }

        return new HermesRunArtifact(runDirectory, summaryPath, reportPath, transcriptLogPath, transcriptJsonPath, standardErrorPath);
    }

    private static string Sanitize(string value)
    {
        return string.Concat(value.Select(static character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-'));
    }
}
