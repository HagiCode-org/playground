using IFlowSdk.Exceptions;
using IFlowSdk.Models;
using IFlowSdk.Transport;

namespace IFlowSdk.Protocol;

public sealed record InitializeResult(bool IsAuthenticated, IReadOnlyList<AuthMethodDescriptor> AuthMethods, bool LoadSessionSupported);

public sealed record AuthMethodDescriptor(string Id, string Name, string? Description);

public sealed record SessionStartResult(string SessionId, string? CurrentModeId, string? CurrentModelId);

public sealed class AcpProtocolClient : IAsyncDisposable
{
    private readonly WebSocketTransport _transport;
    private readonly TimeSpan _timeout;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private readonly ConcurrentDictionary<int, byte> _pendingPermissionRequests = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TaskCompletionSource<bool> _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _requestId;
    private Task? _pumpTask;

    public AcpProtocolClient(WebSocketTransport transport, TimeSpan timeout)
    {
        _transport = transport;
        _timeout = timeout;
    }

    public event Func<string, ValueTask>? RawMessageReceived;

    public event Func<AcpInboundFrame, ValueTask>? FrameReceived;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _transport.ConnectAsync(cancellationToken);
        _pumpTask = Task.Run(PumpAsync);
        await WaitForReadyAsync(cancellationToken);
    }

    public async Task<InitializeResult> InitializeAsync(IFlowOptions options, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        var json = AcpMessageSerializer.CreateInitializeRequest(requestId, options.McpServers, options.Hooks, options.Commands, options.Agents);
        var response = await SendRequestAsync(requestId, json, cancellationToken);
        var result = response.GetProperty("result");
        var authMethods = result.TryGetProperty("authMethods", out var methods)
            ? methods.EnumerateArray().Select(static method => new AuthMethodDescriptor(
                method.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                method.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                method.TryGetProperty("description", out var description) ? description.GetString() : null)).ToArray()
            : Array.Empty<AuthMethodDescriptor>();
        var loadSessionSupported = result.TryGetProperty("agentCapabilities", out var capabilities) &&
            capabilities.TryGetProperty("loadSession", out var loadSession) &&
            loadSession.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            loadSession.GetBoolean();
        var isAuthenticated = result.TryGetProperty("isAuthenticated", out var auth) &&
            auth.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            auth.GetBoolean();

        return new InitializeResult(isAuthenticated, authMethods, loadSessionSupported);
    }

    public async Task AuthenticateAsync(string methodId, AuthMethodInfo? methodInfo, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        var json = AcpMessageSerializer.CreateAuthenticateRequest(requestId, methodId, methodInfo);
        await SendRequestAsync(requestId, json, cancellationToken);
    }

    public async Task<SessionStartResult> CreateSessionAsync(IFlowOptions options, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        var json = AcpMessageSerializer.CreateSessionNewRequest(requestId, options.Cwd, options.McpServers, options.Hooks, options.Commands, options.Agents, options.SessionSettings);
        var response = await SendRequestAsync(requestId, json, cancellationToken);
        return ParseSessionStart(response.GetProperty("result"));
    }

    public async Task LoadSessionAsync(IFlowOptions options, string sessionId, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        var json = AcpMessageSerializer.CreateSessionLoadRequest(requestId, sessionId, options.Cwd, options.McpServers, options.Hooks, options.Commands, options.Agents, options.SessionSettings);
        await SendRequestAsync(requestId, json, cancellationToken);
    }

    public async Task<int> SendPromptAsync(string sessionId, IReadOnlyList<object> prompt, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        var json = AcpMessageSerializer.CreatePromptRequest(requestId, sessionId, prompt);
        await _transport.SendAsync(json, cancellationToken);
        return requestId;
    }

    public Task CancelSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        return _transport.SendAsync(AcpMessageSerializer.CreateCancelRequest(requestId, sessionId), cancellationToken);
    }

    public Task RespondToPermissionRequestAsync(int requestId, string? optionId, bool cancelled, CancellationToken cancellationToken = default)
    {
        if (!_pendingPermissionRequests.ContainsKey(requestId))
        {
            throw new IFlowProtocolException($"Unknown permission request ID: {requestId}");
        }

        _pendingPermissionRequests.TryRemove(requestId, out _);
        return _transport.SendAsync(AcpMessageSerializer.CreatePermissionResponse(requestId, optionId, cancelled), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        if (_pumpTask is not null)
        {
            try
            {
                await _pumpTask;
            }
            catch
            {
            }
        }

        await _transport.DisposeAsync();
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (var raw in _transport.ReadAllAsync(_shutdown.Token))
            {
                if (RawMessageReceived is not null)
                {
                    await RawMessageReceived(raw);
                }

                if (!AcpMessageParser.TryParseInbound(raw, out var frame))
                {
                    continue;
                }

                if (frame.Kind == AcpFrameKind.Control)
                {
                    if (string.Equals(frame.ControlMessage?.Trim(), "//ready", StringComparison.Ordinal))
                    {
                        _readyTcs.TrySetResult(true);
                    }

                    continue;
                }

                if (frame.Kind == AcpFrameKind.Response || frame.Kind == AcpFrameKind.Error)
                {
                    CompletePendingRequest(frame);
                }

                if (frame.Kind == AcpFrameKind.PermissionRequest && frame.RequestId.HasValue)
                {
                    _pendingPermissionRequests.TryAdd(frame.RequestId.Value, 0);
                }

                if (frame.Kind is AcpFrameKind.LegacyToolCall or AcpFrameKind.LegacyToolUpdate or AcpFrameKind.LegacyTaskFinish)
                {
                    await AcknowledgeLegacyCallAsync(frame);
                }

                if (FrameReceived is not null)
                {
                    await FrameReceived(frame);
                }
            }
        }
        catch (Exception ex)
        {
            _readyTcs.TrySetException(ex);
            foreach (var pending in _pendingRequests.Values)
            {
                pending.TrySetException(ex);
            }
        }
    }

    private void CompletePendingRequest(AcpInboundFrame frame)
    {
        if (!frame.RequestId.HasValue)
        {
            return;
        }

        if (!_pendingRequests.TryRemove(frame.RequestId.Value, out var completion))
        {
            return;
        }

        if (frame.Kind == AcpFrameKind.Error)
        {
            var error = frame.Payload.GetProperty("error");
            var message = error.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? "Unknown iFlow error." : "Unknown iFlow error.";
            if (error.TryGetProperty("data", out var data) && data.TryGetProperty("details", out var detailsElement) && !string.IsNullOrWhiteSpace(detailsElement.GetString()))
            {
                message = $"{message} {detailsElement.GetString()}";
            }

            completion.TrySetException(new IFlowProtocolException(message));
            return;
        }

        completion.TrySetResult(frame.Payload);
    }

    private async Task<JsonElement> SendRequestAsync(int requestId, string json, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingRequests.TryAdd(requestId, completion))
        {
            throw new IFlowProtocolException($"Duplicate request id {requestId}.");
        }

        await _transport.SendAsync(json, cancellationToken);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        timeoutCts.CancelAfter(_timeout);
        using var registration = timeoutCts.Token.Register(() => completion.TrySetCanceled(timeoutCts.Token));

        try
        {
            return await completion.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _pendingRequests.TryRemove(requestId, out _);
            throw new IFlowProtocolException($"Request {requestId} timed out.", ex);
        }
    }

    private async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        timeoutCts.CancelAfter(_timeout);
        try
        {
            await _readyTcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException ex)
        {
            throw new IFlowProtocolException("Timed out waiting for iFlow ACP //ready.", ex);
        }
    }

    private int NextRequestId()
    {
        return Interlocked.Increment(ref _requestId);
    }

    private static SessionStartResult ParseSessionStart(JsonElement result)
    {
        string? currentModeId = null;
        string? currentModelId = null;

        if (result.TryGetProperty("modes", out var modes) && modes.TryGetProperty("currentModeId", out var currentMode))
        {
            currentModeId = currentMode.GetString();
        }

        if (result.TryGetProperty("models", out var models) && models.TryGetProperty("currentModelId", out var currentModel))
        {
            currentModelId = currentModel.GetString();
        }

        return new SessionStartResult(
            result.TryGetProperty("sessionId", out var sessionId) ? sessionId.GetString() ?? string.Empty : string.Empty,
            currentModeId,
            currentModelId);
    }

    private Task AcknowledgeLegacyCallAsync(AcpInboundFrame frame)
    {
        if (!frame.RequestId.HasValue)
        {
            return Task.CompletedTask;
        }

        var ack = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = frame.RequestId.Value,
            ["result"] = null,
        });
        return _transport.SendAsync(ack);
    }
}
