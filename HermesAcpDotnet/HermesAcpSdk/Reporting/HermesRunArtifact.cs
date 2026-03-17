namespace HermesAcpSdk.Reporting;

public sealed record HermesRunArtifact(
    string RunDirectory,
    string SummaryPath,
    string MarkdownReportPath,
    string TranscriptLogPath,
    string TranscriptJsonPath,
    string? StandardErrorPath);
