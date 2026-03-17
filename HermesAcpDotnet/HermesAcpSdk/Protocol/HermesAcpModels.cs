namespace HermesAcpSdk.Protocol;

public enum HermesFrameKind
{
    Control,
    Response,
    Error,
    Notification,
    Unknown,
}

public sealed record HermesInboundFrame(
    HermesFrameKind Kind,
    string RawText,
    JsonElement Payload,
    int? RequestId = null,
    string? Method = null,
    string? ControlMessage = null);

public sealed record HermesAuthMethod(string Id, string? Name = null, string? Description = null);

public sealed record HermesInitializeResult(
    bool IsAuthenticated,
    IReadOnlyList<HermesAuthMethod> AuthMethods,
    JsonElement RawResult);

public sealed record HermesAuthenticationResult(
    string MethodId,
    bool Succeeded,
    JsonElement RawResult);

public sealed record HermesSessionStartResult(
    string SessionId,
    JsonElement RawResult);

public sealed record HermesPromptUpdate(
    string Kind,
    string? Text,
    JsonElement Payload);

public sealed record HermesPromptResult(
    string SessionId,
    string? FinalText,
    string? StopReason,
    IReadOnlyList<HermesPromptUpdate> Updates,
    JsonElement RawResult);
