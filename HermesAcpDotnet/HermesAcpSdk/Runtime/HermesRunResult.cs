using HermesAcpSdk.Configuration;
using HermesAcpSdk.Protocol;
using HermesAcpSdk.Reporting;
using HermesAcpSdk.Transport;

namespace HermesAcpSdk.Runtime;

public sealed class HermesRunResult
{
    public required string ProfileName { get; init; }

    public required HermesLaunchSummary EffectiveLaunch { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }

    public required IReadOnlyList<FeatureCheckResult> Features { get; init; }

    public required IReadOnlyList<TranscriptEntry> Transcript { get; init; }

    public string? PromptText { get; init; }

    public HermesInitializeResult? Initialize { get; init; }

    public HermesAuthenticationResult? Authentication { get; init; }

    public HermesSessionStartResult? Session { get; init; }

    public HermesPromptResult? Prompt { get; init; }

    public string? FailureStage { get; init; }

    public string? FailureMessage { get; init; }

    public string? StandardError { get; init; }

    public HermesRunArtifact? Artifact { get; set; }

    public TimeSpan Duration => CompletedAt - StartedAt;
}
