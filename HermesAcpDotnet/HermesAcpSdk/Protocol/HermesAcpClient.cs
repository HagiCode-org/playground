using HermesAcpSdk.Configuration;
using HermesAcpSdk.Transport;

namespace HermesAcpSdk.Protocol;

public sealed class HermesAcpClient : IAsyncDisposable
{
    private readonly StdioAcpTransport _transport;
    private readonly TimeSpan _timeout;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TaskCompletionSource<bool> _readySignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _promptSync = new();
    private PromptCollector? _activePrompt;
    private int _requestId;
    private Task? _pumpTask;

    public HermesAcpClient(StdioAcpTransport transport, TimeSpan timeout)
    {
        _transport = transport;
        _timeout = timeout;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _pumpTask ??= Task.Run(PumpAsync);
        await Task.Yield();
    }

    public async Task<HermesInitializeResult> InitializeAsync(HermesLaunchProfile profile, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        var payload = HermesAcpMessageFactory.CreateInitializeRequest(requestId, profile);
        var response = await SendRequestAsync(requestId, payload, cancellationToken);
        return HermesAcpMessageParser.ParseInitializeResult(response);
    }

    public async Task<HermesAuthenticationResult> AuthenticateAsync(HermesAuthenticationOptions authentication, string methodId, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        var payload = HermesAcpMessageFactory.CreateAuthenticateRequest(requestId, authentication, methodId);
        var response = await SendRequestAsync(requestId, payload, cancellationToken);
        return HermesAcpMessageParser.ParseAuthenticateResult(methodId, response);
    }

    public async Task<HermesSessionStartResult> CreateSessionAsync(HermesLaunchProfile profile, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        var payload = HermesAcpMessageFactory.CreateSessionNewRequest(requestId, profile);
        var response = await SendRequestAsync(requestId, payload, cancellationToken);
        return HermesAcpMessageParser.ParseSessionStart(response);
    }

    public async Task<HermesPromptResult> PromptAsync(string sessionId, string promptText, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        lock (_promptSync)
        {
            _activePrompt = new PromptCollector(sessionId);
        }

        var payload = HermesAcpMessageFactory.CreatePromptRequest(requestId, sessionId, promptText);
        var response = await SendRequestAsync(requestId, payload, cancellationToken);
        PromptCollector? collector;
        lock (_promptSync)
        {
            collector = _activePrompt;
            _activePrompt = null;
        }

        var updates = collector?.Snapshot() ?? Array.Empty<HermesPromptUpdate>();
        var finalText = HermesAcpMessageParser.ExtractPromptText(response) ?? BuildPromptTextFromUpdates(updates);
        var stopReason = HermesAcpMessageParser.ExtractStopReason(response);
        return new HermesPromptResult(sessionId, finalText, stopReason, updates, response.GetProperty("result").Clone());
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

        _shutdown.Dispose();
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (var rawMessage in _transport.ReadAllAsync(_shutdown.Token))
            {
                if (!HermesAcpMessageParser.TryParseFrame(rawMessage, out var frame))
                {
                    continue;
                }

                switch (frame.Kind)
                {
                    case HermesFrameKind.Control when string.Equals(frame.ControlMessage, "//ready", StringComparison.Ordinal):
                        _readySignal.TrySetResult(true);
                        break;
                    case HermesFrameKind.Response:
                    case HermesFrameKind.Error:
                        _readySignal.TrySetResult(true);
                        CompletePendingRequest(frame);
                        break;
                    case HermesFrameKind.Notification when string.Equals(frame.Method, "session/update", StringComparison.Ordinal):
                        _readySignal.TrySetResult(true);
                        CapturePromptUpdate(frame.Payload);
                        break;
                }
            }
        }
        catch (Exception exception)
        {
            _readySignal.TrySetException(exception);
            foreach (var pendingRequest in _pendingRequests.Values)
            {
                pendingRequest.TrySetException(exception);
            }
        }
    }

    private async Task<JsonElement> SendRequestAsync(int requestId, string payload, CancellationToken cancellationToken)
    {
        var completionSource = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingRequests.TryAdd(requestId, completionSource))
        {
            throw new HermesProtocolException($"Duplicate request id {requestId}.");
        }

        await _transport.SendAsync(payload, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        timeoutCts.CancelAfter(_timeout);
        using var registration = timeoutCts.Token.Register(() => completionSource.TrySetCanceled(timeoutCts.Token));

        try
        {
            return await completionSource.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _pendingRequests.TryRemove(requestId, out _);
            throw new HermesProtocolException($"Hermes request {requestId} timed out.", exception);
        }
    }

    private void CompletePendingRequest(HermesInboundFrame frame)
    {
        if (!frame.RequestId.HasValue || !_pendingRequests.TryRemove(frame.RequestId.Value, out var completionSource))
        {
            return;
        }

        if (frame.Kind == HermesFrameKind.Error)
        {
            var error = frame.Payload.GetProperty("error");
            var message = error.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString() ?? "Unknown Hermes error."
                : "Unknown Hermes error.";
            completionSource.TrySetException(new HermesProtocolException(message));
            return;
        }

        completionSource.TrySetResult(frame.Payload);
    }

    private void CapturePromptUpdate(JsonElement payload)
    {
        PromptCollector? collector;
        lock (_promptSync)
        {
            collector = _activePrompt;
        }

        if (collector is null)
        {
            return;
        }

        var update = HermesAcpMessageParser.ParsePromptUpdate(payload);
        if (update is not null)
        {
            collector.Add(update);
        }
    }

    private int NextRequestId()
    {
        return Interlocked.Increment(ref _requestId);
    }

    private static string? BuildPromptTextFromUpdates(IReadOnlyList<HermesPromptUpdate> updates)
    {
        var messageChunks = updates
            .Where(update =>
                !string.IsNullOrWhiteSpace(update.Text) &&
                (update.Kind.Contains("message", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(update.Kind, "assistant", StringComparison.OrdinalIgnoreCase)))
            .Select(update => update.Text!.Trim())
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .ToArray();

        if (messageChunks.Length == 0)
        {
            return null;
        }

        return string.Join(Environment.NewLine, messageChunks);
    }

    private sealed class PromptCollector
    {
        private readonly List<HermesPromptUpdate> _updates = [];
        private readonly Lock _sync = new();

        public PromptCollector(string sessionId)
        {
            SessionId = sessionId;
        }

        public string SessionId { get; }

        public void Add(HermesPromptUpdate update)
        {
            lock (_sync)
            {
                _updates.Add(update);
            }
        }

        public IReadOnlyList<HermesPromptUpdate> Snapshot()
        {
            lock (_sync)
            {
                return _updates.ToArray();
            }
        }
    }
}
