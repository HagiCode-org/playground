namespace CopilotSdk.Models;

public sealed record CopilotNormalizedResponse(
    string CorrelationId,
    bool Success,
    string Content,
    CopilotErrorCategory? ErrorCategory,
    string? ErrorMessage,
    bool RetriedAfterRefresh,
    bool Streaming,
    TimeSpan Duration);
