using IFlowSdk.Exceptions;
using IFlowSdk.Models;
using IFlowSdk.Protocol;
using IFlowSdk.Runtime;
using IFlowSdk.Transport;

namespace IFlowSdk.Client;

public class IFlowClient : IAsyncDisposable
{
    private readonly Channel<Message> _messageChannel;
    private readonly ConcurrentDictionary<string, int> _toolConfirmationRequestIds = new(StringComparer.Ordinal);
    private readonly Channel<RawMessage>? _rawChannel;
    private WebSocketTransport? _transport;
    private AcpProtocolClient? _protocol;
    private IFlowProcessManager? _processManager;
    private bool _ownsProcess;

    public IFlowClient(IFlowOptions? options = null)
    {
        Options = options ?? new IFlowOptions();
        Options.Validate();

        _messageChannel = Channel.CreateBounded<Message>(new BoundedChannelOptions(Options.MaxMessageQueueSize)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = Options.QueueOverflowStrategy switch
            {
                QueueOverflowStrategy.DropOldest => BoundedChannelFullMode.DropOldest,
                QueueOverflowStrategy.DropNewest => BoundedChannelFullMode.DropWrite,
                QueueOverflowStrategy.Throw => BoundedChannelFullMode.Wait,
                _ => BoundedChannelFullMode.DropOldest,
            },
        });

        if (CaptureRawMessages)
        {
            _rawChannel = Channel.CreateUnbounded<RawMessage>();
        }
    }

    protected virtual bool CaptureRawMessages => false;

    public IFlowOptions Options { get; }

    public bool IsConnected { get; private set; }

    public string? SessionId { get; private set; }

    public string? ConnectionUrl { get; private set; }

    public int? ProcessId => _processManager?.ProcessId;

    public bool OwnsProcess => _ownsProcess;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        var resolvedUrl = Options.ResolvedUrl;
        if (Options.AutoStartProcess && ShouldAutoStart(resolvedUrl))
        {
            _processManager = new IFlowProcessManager();
            resolvedUrl = await _processManager.StartAsync(Options, cancellationToken);
            _ownsProcess = true;
        }

        ConnectionUrl = resolvedUrl;
        _transport = new WebSocketTransport(resolvedUrl, Options.Timeout);
        _protocol = new AcpProtocolClient(_transport, Options.Timeout);
        _protocol.FrameReceived += HandleInboundFrameAsync;

        await _protocol.StartAsync(cancellationToken);

        var initialize = await _protocol.InitializeAsync(Options, cancellationToken);
        if (!initialize.IsAuthenticated)
        {
            await _protocol.AuthenticateAsync(Options.AuthMethodId ?? "iflow", Options.AuthMethodInfo, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(Options.SessionId))
        {
            await _protocol.LoadSessionAsync(Options, Options.SessionId, cancellationToken);
            SessionId = Options.SessionId;
        }
        else
        {
            var session = await _protocol.CreateSessionAsync(Options, cancellationToken);
            SessionId = session.SessionId;
        }

        IsConnected = true;
    }

    public async Task DisconnectAsync()
    {
        if (_protocol is not null)
        {
            await _protocol.DisposeAsync();
            _protocol = null;
        }

        if (_ownsProcess && _processManager is not null)
        {
            await _processManager.DisposeAsync();
            _processManager = null;
        }

        _messageChannel.Writer.TryComplete();
        _rawChannel?.Writer.TryComplete();
        IsConnected = false;
    }

    public async Task LoadSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _protocol!.LoadSessionAsync(Options, sessionId, cancellationToken);
        SessionId = sessionId;
    }

    public async Task SendMessageAsync(string text, IReadOnlyList<string>? files = null, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (string.IsNullOrWhiteSpace(SessionId))
        {
            throw new IFlowConnectionException("No active iFlow session is available.");
        }

        var prompt = AcpMessageSerializer.BuildPrompt(text, files);
        await _protocol!.SendPromptAsync(SessionId, prompt, cancellationToken);
    }

    public async Task InterruptAsync(CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (string.IsNullOrWhiteSpace(SessionId))
        {
            throw new IFlowConnectionException("No active iFlow session is available.");
        }

        await _protocol!.CancelSessionAsync(SessionId, cancellationToken);
    }

    public IAsyncEnumerable<Message> ReceiveMessagesAsync(CancellationToken cancellationToken = default)
    {
        return _messageChannel.Reader.ReadAllAsync(cancellationToken);
    }

    public async Task RespondToToolConfirmationAsync(int requestId, string optionId, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _protocol!.RespondToPermissionRequestAsync(requestId, optionId, cancelled: false, cancellationToken);
    }

    public async Task CancelToolConfirmationAsync(int requestId, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _protocol!.RespondToPermissionRequestAsync(requestId, optionId: null, cancelled: true, cancellationToken);
    }

    public async Task ApproveToolCallAsync(string toolId, ToolCallConfirmationOutcome outcome = ToolCallConfirmationOutcome.ProceedOnce, CancellationToken cancellationToken = default)
    {
        if (!_toolConfirmationRequestIds.TryGetValue(toolId, out var requestId))
        {
            throw new IFlowProtocolException($"Unknown tool confirmation for tool call {toolId}.");
        }

        await RespondToToolConfirmationAsync(requestId, outcome.ToOptionId(), cancellationToken);
    }

    public async Task RejectToolCallAsync(string toolId, CancellationToken cancellationToken = default)
    {
        if (!_toolConfirmationRequestIds.TryGetValue(toolId, out var requestId))
        {
            throw new IFlowProtocolException($"Unknown tool confirmation for tool call {toolId}.");
        }

        await CancelToolConfirmationAsync(requestId, cancellationToken);
    }

    public virtual ValueTask DisposeAsync()
    {
        return new ValueTask(DisconnectAsync());
    }

    public ValueTask<RawMessage> WaitForRawMessageAsync(CancellationToken cancellationToken = default)
    {
        if (_rawChannel is null)
        {
            throw new InvalidOperationException("Raw capture is not enabled for this client.");
        }

        return _rawChannel.Reader.ReadAsync(cancellationToken);
    }

    protected async ValueTask PublishRawMessageAsync(RawMessage rawMessage)
    {
        if (_rawChannel is null)
        {
            return;
        }

        await _rawChannel.Writer.WriteAsync(rawMessage);
    }

    protected async ValueTask HandleInboundFrameAsync(AcpInboundFrame frame)
    {
        var message = MapFrameToMessage(frame);
        await PublishRawMessageAsync(new RawMessage(
            frame.RawText,
            frame.Payload.ValueKind == JsonValueKind.Undefined ? null : frame.Payload,
            frame.Kind.ToString(),
            frame.Kind == AcpFrameKind.Control,
            message));

        if (message is null)
        {
            return;
        }

        if (message is ToolConfirmationRequestMessage confirmation)
        {
            _toolConfirmationRequestIds[confirmation.ToolCall.Id] = confirmation.RequestId;
        }

        await _messageChannel.Writer.WriteAsync(message);
    }

    protected virtual Message? MapFrameToMessage(AcpInboundFrame frame)
    {
        return frame.Kind switch
        {
            AcpFrameKind.SessionUpdate => MapSessionUpdate(frame),
            AcpFrameKind.PermissionRequest => MapPermissionRequest(frame),
            AcpFrameKind.Response => MapResponse(frame),
            AcpFrameKind.Error => MapError(frame),
            AcpFrameKind.LegacyToolCall => MapLegacyToolCall(frame),
            AcpFrameKind.LegacyToolUpdate => MapLegacyToolUpdate(frame),
            AcpFrameKind.LegacyTaskFinish => new TaskFinishMessage(),
            _ => null,
        };
    }

    private static Message? MapSessionUpdate(AcpInboundFrame frame)
    {
        if (!frame.Payload.TryGetProperty("update", out var update))
        {
            return null;
        }

        var agentId = update.TryGetProperty("agentId", out var agentIdElement) ? agentIdElement.GetString() : null;
        var agentInfo = !string.IsNullOrWhiteSpace(agentId) ? new AgentInfo(agentId) : null;
        var updateType = frame.SessionUpdateType;

        return updateType switch
        {
            "agent_message_chunk" => ParseAssistantChunk(update, agentId, agentInfo, thought: false),
            "agent_thought_chunk" => ParseAssistantChunk(update, agentId, agentInfo, thought: true),
            "user_message_chunk" => ParseUserChunk(update, agentId, agentInfo),
            "tool_call" => ParseToolCall(update, agentId, agentInfo),
            "tool_call_update" => ParseToolResult(update, agentId, agentInfo),
            "plan" => ParsePlan(update),
            _ => null,
        };
    }

    private static Message? MapPermissionRequest(AcpInboundFrame frame)
    {
        var toolCall = frame.Payload.TryGetProperty("toolCall", out var toolCallElement)
            ? ToolCall.FromJson(toolCallElement)
            : new ToolCall(string.Empty, string.Empty);
        var options = frame.Payload.TryGetProperty("options", out var optionsElement) && optionsElement.ValueKind == JsonValueKind.Array
            ? optionsElement.EnumerateArray().Select(PermissionOption.FromJson).ToArray()
            : Array.Empty<PermissionOption>();
        var sessionId = frame.Payload.TryGetProperty("sessionId", out var sessionElement) ? sessionElement.GetString() ?? string.Empty : string.Empty;
        return new ToolConfirmationRequestMessage(sessionId, toolCall, options, frame.RequestId ?? 0);
    }

    private static Message? MapResponse(AcpInboundFrame frame)
    {
        if (!frame.Payload.TryGetProperty("result", out var result))
        {
            return null;
        }

        if (result.TryGetProperty("stopReason", out var stopReason))
        {
            return new TaskFinishMessage(ToolCallJson.ParseStopReason(stopReason.GetString()));
        }

        return null;
    }

    private static Message MapError(AcpInboundFrame frame)
    {
        var error = frame.Payload.GetProperty("error");
        var details = error.TryGetProperty("data", out var data) && data.TryGetProperty("details", out var detailsElement)
            ? detailsElement.GetString()
            : null;
        return new ErrorMessage(
            error.TryGetProperty("code", out var code) ? code.GetInt32() : -1,
            error.TryGetProperty("message", out var message) ? message.GetString() ?? "Unknown iFlow error." : "Unknown iFlow error.",
            details);
    }

    private static Message? MapLegacyToolCall(AcpInboundFrame frame)
    {
        return new ToolCallMessage(
            frame.Payload.TryGetProperty("toolCallId", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            frame.Payload.TryGetProperty("title", out var title) ? title.GetString() ?? "Tool" : "Tool",
            new Icon("emoji", "🔧"),
            ToolCallJson.ParseStatus(AcpMessageParser.GetNestedString(frame.Payload, "status")));
    }

    private static Message? MapLegacyToolUpdate(AcpInboundFrame frame)
    {
        return new ToolResultMessage(
            frame.Payload.TryGetProperty("toolCallId", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            ToolCallJson.ParseStatus(AcpMessageParser.GetNestedString(frame.Payload, "status")));
    }

    private static AssistantMessage? ParseAssistantChunk(JsonElement update, string? agentId, AgentInfo? agentInfo, bool thought)
    {
        if (!update.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!content.TryGetProperty("type", out var contentType) || contentType.GetString() != "text")
        {
            return null;
        }

        var text = content.TryGetProperty("text", out var textElement) ? textElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return thought
            ? new AssistantMessage(new AssistantMessageChunk(Thought: text), agentId, agentInfo)
            : new AssistantMessage(new AssistantMessageChunk(Text: text), agentId, agentInfo);
    }

    private static UserMessage? ParseUserChunk(JsonElement update, string? agentId, AgentInfo? agentInfo)
    {
        if (!update.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var text = content.TryGetProperty("text", out var textElement) ? textElement.GetString() : null;
        return string.IsNullOrWhiteSpace(text)
            ? null
            : new UserMessage(new[] { new UserMessageChunk(Text: text) }, agentId, agentInfo);
    }

    private static ToolCallMessage ParseToolCall(JsonElement update, string? agentId, AgentInfo? agentInfo)
    {
        var args = update.TryGetProperty("args", out var argsElement) && argsElement.ValueKind == JsonValueKind.Object
            ? argsElement.EnumerateObject().ToDictionary(static item => item.Name, static item => item.Value.Clone())
            : null;
        return new ToolCallMessage(
            update.TryGetProperty("toolCallId", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            update.TryGetProperty("title", out var title) ? title.GetString() ?? "Tool" : "Tool",
            new Icon("emoji", "🔧"),
            ToolCallJson.ParseStatus(update.TryGetProperty("status", out var status) ? status.GetString() : null),
            update.TryGetProperty("toolName", out var toolName) ? toolName.GetString() : null,
            AgentId: agentId,
            AgentInfo: agentInfo,
            Args: args);
    }

    private static ToolResultMessage ParseToolResult(JsonElement update, string? agentId, AgentInfo? agentInfo)
    {
        ToolCallContent? content = null;
        IReadOnlyDictionary<string, JsonElement>? args = null;
        if (update.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array && contentArray.GetArrayLength() > 0)
        {
            var first = contentArray[0];
            if (first.TryGetProperty("content", out var contentValue))
            {
                content = contentValue.ValueKind == JsonValueKind.Object
                    ? ToolCallJson.ParseContent(contentValue)
                    : new ToolCallContent(first.TryGetProperty("type", out var type) ? type.GetString() ?? "markdown" : "markdown", Markdown: contentValue.ToString());
            }

            if (first.TryGetProperty("args", out var argsElement) && argsElement.ValueKind == JsonValueKind.Object)
            {
                args = argsElement.EnumerateObject().ToDictionary(static item => item.Name, static item => item.Value.Clone());
            }
        }

        return new ToolResultMessage(
            update.TryGetProperty("toolCallId", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            ToolCallJson.ParseStatus(update.TryGetProperty("status", out var status) ? status.GetString() : null),
            update.TryGetProperty("toolName", out var toolName) ? toolName.GetString() : null,
            content,
            AgentId: agentId,
            AgentInfo: agentInfo,
            Args: args);
    }

    private static PlanMessage? ParsePlan(JsonElement update)
    {
        if (!update.TryGetProperty("entries", out var entriesElement) || entriesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var entries = entriesElement.EnumerateArray().Select(static entry => new PlanEntry(
            entry.TryGetProperty("content", out var content) ? content.GetString() ?? string.Empty : string.Empty,
            entry.TryGetProperty("priority", out var priority) ? priority.GetString() ?? "medium" : "medium",
            entry.TryGetProperty("status", out var status) ? status.GetString() ?? "pending" : "pending")).ToArray();

        return entries.Length == 0 ? null : new PlanMessage(entries);
    }

    private void EnsureConnected()
    {
        if (!IsConnected || _protocol is null)
        {
            throw new IFlowConnectionException("IFlowClient is not connected. Call ConnectAsync() first.");
        }
    }

    private bool ShouldAutoStart(string resolvedUrl)
    {
        var explicitUrlProvided = !string.IsNullOrWhiteSpace(Options.Url);
        if (explicitUrlProvided)
        {
            return false;
        }

        return !IFlowProcessManager.IsEndpointListening(resolvedUrl);
    }
}
