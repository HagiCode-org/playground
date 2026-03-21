namespace KimiAcpSdk.Protocol;

public enum KimiFrameKind
{
    Control,
    Response,
    Error,
    Notification,
    Unknown,
}

public sealed record KimiInboundFrame(
    KimiFrameKind Kind,
    string RawText,
    JsonElement Payload,
    int? RequestId = null,
    string? Method = null,
    string? ControlMessage = null);

public sealed record KimiAuthMethod(string Id, string? Name = null, string? Description = null);

public sealed record KimiInitializeResult(
    bool IsAuthenticated,
    IReadOnlyList<KimiAuthMethod> AuthMethods,
    JsonElement RawResult);

public sealed record KimiAuthenticationResult(
    string MethodId,
    bool Succeeded,
    JsonElement RawResult);

public sealed record KimiSessionStartResult(
    string SessionId,
    JsonElement RawResult);

public sealed record KimiPromptUpdate(
    string Kind,
    string? Text,
    JsonElement Payload);

public sealed record KimiPromptResult(
    string SessionId,
    string? FinalText,
    string? StopReason,
    IReadOnlyList<KimiPromptUpdate> Updates,
    JsonElement RawResult);
