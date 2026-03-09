using CopilotSdk.Auth;

namespace CopilotSdk.Models;

public sealed record CopilotGatewayRequest(
    string CorrelationId,
    string Model,
    string Prompt,
    TimeSpan Timeout,
    string? CliPath,
    string? CliUrl,
    string WorkingDirectory,
    CopilotCredential Credential,
    bool Streaming);
