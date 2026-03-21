namespace KimiAcpSdk.Reporting;

public sealed record KimiRunArtifact(
    string RunDirectory,
    string SummaryPath,
    string MarkdownReportPath,
    string TranscriptLogPath,
    string TranscriptJsonPath,
    string? StandardErrorPath,
    string? DiagnosticsPath);
