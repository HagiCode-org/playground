using KimiAcpSdk.Configuration;
using KimiAcpSdk.Protocol;
using KimiAcpSdk.Reporting;
using KimiAcpSdk.Transport;

namespace KimiAcpSdk.Runtime;

public sealed class KimiRunResult
{
    public required string ProfileName { get; init; }

    public required KimiLaunchSummary EffectiveLaunch { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }

    public required IReadOnlyList<FeatureCheckResult> Features { get; init; }

    public required IReadOnlyList<TranscriptEntry> Transcript { get; init; }

    public string? PromptText { get; init; }

    public KimiInitializeResult? Initialize { get; init; }

    public KimiAuthenticationResult? Authentication { get; init; }

    public KimiSessionStartResult? Session { get; init; }

    public KimiPromptResult? Prompt { get; init; }

    public string? FailureStage { get; init; }

    public string? FailureMessage { get; init; }

    public string? StandardError { get; init; }

    public KimiRunArtifact? Artifact { get; set; }

    public TimeSpan Duration => CompletedAt - StartedAt;
}
