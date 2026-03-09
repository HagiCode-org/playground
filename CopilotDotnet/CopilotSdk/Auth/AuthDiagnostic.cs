namespace CopilotSdk.Auth;

public sealed record AuthDiagnostic(
    AuthFailureCategory Category,
    string Message,
    string CorrelationId,
    DateTimeOffset TimestampUtc);
