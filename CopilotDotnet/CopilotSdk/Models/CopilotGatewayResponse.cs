namespace CopilotSdk.Models;

public sealed record CopilotGatewayResponse(
    IReadOnlyList<string> DeltaChunks,
    string? FinalContent,
    TimeSpan Duration,
    bool Streaming);
