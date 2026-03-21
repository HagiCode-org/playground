using KimiAcpSdk.Configuration;
using KimiAcpSdk.Transport;

namespace KimiAcpSdk.Protocol;

public sealed class KimiAcpClient : IAsyncDisposable
{
    private readonly IAcpTransport _transport;
    private readonly TimeSpan _timeout;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TaskCompletionSource<bool> _readySignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _promptSync = new();
    private PromptCollector? _activePrompt;
    private int _requestId;
    private Task? _pumpTask;

    public KimiAcpClient(IAcpTransport transport, TimeSpan timeout)
    {
        _transport = transport;
        _timeout = timeout;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _pumpTask ??= Task.Run(PumpAsync);
        await Task.Yield();
    }

    public async Task<KimiInitializeResult> InitializeAsync(KimiLaunchProfile profile, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        var payload = KimiAcpMessageFactory.CreateInitializeRequest(requestId, profile);
        var response = await SendRequestAsync(requestId, payload, cancellationToken);
        return KimiAcpMessageParser.ParseInitializeResult(response);
    }

    public async Task<KimiAuthenticationResult> AuthenticateAsync(KimiAuthenticationOptions authentication, string methodId, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        var payload = KimiAcpMessageFactory.CreateAuthenticateRequest(requestId, authentication, methodId);
        var response = await SendRequestAsync(requestId, payload, cancellationToken);
        return KimiAcpMessageParser.ParseAuthenticateResult(methodId, response);
    }

    public async Task<KimiSessionStartResult> CreateSessionAsync(KimiLaunchProfile profile, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        var payload = KimiAcpMessageFactory.CreateSessionNewRequest(requestId, profile);
        var response = await SendRequestAsync(requestId, payload, cancellationToken);
        return KimiAcpMessageParser.ParseSessionStart(response);
    }

    public async Task<KimiPromptResult> PromptAsync(string sessionId, string promptText, CancellationToken cancellationToken = default)
    {
        var requestId = NextRequestId();
        lock (_promptSync)
        {
            _activePrompt = new PromptCollector(sessionId);
        }

        var payload = KimiAcpMessageFactory.CreatePromptRequest(requestId, sessionId, promptText);
        var response = await SendRequestAsync(requestId, payload, cancellationToken);
        PromptCollector? collector;
        lock (_promptSync)
        {
            collector = _activePrompt;
            _activePrompt = null;
        }

        var updates = collector?.Snapshot() ?? Array.Empty<KimiPromptUpdate>();
        var finalText = KimiAcpMessageParser.ExtractPromptText(response) ?? BuildPromptTextFromUpdates(updates);
        var stopReason = KimiAcpMessageParser.ExtractStopReason(response);
        return new KimiPromptResult(sessionId, finalText, stopReason, updates, response.GetProperty("result").Clone());
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
                if (!KimiAcpMessageParser.TryParseFrame(rawMessage, out var frame))
                {
                    continue;
                }

                switch (frame.Kind)
                {
                    case KimiFrameKind.Control when string.Equals(frame.ControlMessage, "//ready", StringComparison.Ordinal):
                        _readySignal.TrySetResult(true);
                        break;
                    case KimiFrameKind.Response:
                    case KimiFrameKind.Error:
                        _readySignal.TrySetResult(true);
                        CompletePendingRequest(frame);
                        break;
                    case KimiFrameKind.Notification when string.Equals(frame.Method, "session/update", StringComparison.Ordinal):
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
            throw new KimiProtocolException($"Duplicate request id {requestId}.");
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
            throw new KimiProtocolException($"Kimi request {requestId} timed out.", exception);
        }
    }

    private void CompletePendingRequest(KimiInboundFrame frame)
    {
        if (!frame.RequestId.HasValue || !_pendingRequests.TryRemove(frame.RequestId.Value, out var completionSource))
        {
            return;
        }

        if (frame.Kind == KimiFrameKind.Error)
        {
            var error = frame.Payload.GetProperty("error");
            var message = error.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString() ?? "Unknown Kimi error."
                : "Unknown Kimi error.";
            completionSource.TrySetException(new KimiProtocolException(message));
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

        var update = KimiAcpMessageParser.ParsePromptUpdate(payload);
        if (update is not null)
        {
            collector.Add(update);
        }
    }

    private int NextRequestId()
    {
        return Interlocked.Increment(ref _requestId);
    }

    private static string? BuildPromptTextFromUpdates(IReadOnlyList<KimiPromptUpdate> updates)
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
        private readonly List<KimiPromptUpdate> _updates = [];
        private readonly Lock _sync = new();

        public PromptCollector(string sessionId)
        {
            SessionId = sessionId;
        }

        public string SessionId { get; }

        public void Add(KimiPromptUpdate update)
        {
            lock (_sync)
            {
                _updates.Add(update);
            }
        }

        public IReadOnlyList<KimiPromptUpdate> Snapshot()
        {
            lock (_sync)
            {
                return _updates.ToArray();
            }
        }
    }
}
