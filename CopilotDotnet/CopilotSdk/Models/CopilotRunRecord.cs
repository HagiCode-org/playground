using CopilotSdk.Auth;

namespace CopilotSdk.Models;

public sealed record CopilotRunRecord(
    string CorrelationId,
    string Model,
    string Prompt,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    bool Success,
    string Content,
    CopilotErrorCategory? ErrorCategory,
    string? ErrorMessage,
    bool RetriedAfterRefresh,
    bool Streaming,
    long DurationMs,
    AuthFailureCategory? AuthFailureCategory,
    string? AuthFailureMessage);
