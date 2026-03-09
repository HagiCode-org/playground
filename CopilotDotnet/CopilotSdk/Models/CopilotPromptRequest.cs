namespace CopilotSdk.Models;

public sealed record CopilotPromptRequest(
    string CorrelationId,
    string Model,
    string Prompt,
    TimeSpan Timeout,
    bool Streaming);
