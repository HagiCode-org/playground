using IFlowSdk.Exceptions;

namespace IFlowSdk.Transport;

public sealed class WebSocketTransport : IAsyncDisposable
{
    private readonly Uri _uri;
    private readonly TimeSpan _timeout;
    private readonly Channel<string> _incomingChannel = Channel.CreateUnbounded<string>();
    private readonly CancellationTokenSource _shutdown = new();
    private ClientWebSocket? _socket;
    private Task? _receiveLoopTask;

    public WebSocketTransport(string url, TimeSpan timeout)
    {
        _uri = new Uri(url, UriKind.Absolute);
        _timeout = timeout;
    }

    public bool IsConnected => _socket?.State == WebSocketState.Open;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        _socket = new ClientWebSocket();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        linked.CancelAfter(_timeout);

        try
        {
            await _socket.ConnectAsync(_uri, linked.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IFlowConnectionException($"Timed out connecting to {_uri}.", ex);
        }
        catch (WebSocketException ex)
        {
            throw new IFlowConnectionException($"Failed to connect to {_uri}: {ex.Message}", ex);
        }

        _receiveLoopTask = Task.Run(ReceiveLoopAsync);
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (_socket is null || _socket.State != WebSocketState.Open)
        {
            throw new IFlowConnectionException("WebSocket transport is not connected.");
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _incomingChannel.Reader.ReadAllAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();

        if (_socket is not null)
        {
            if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None);
                }
                catch
                {
                }
            }

            _socket.Dispose();
        }

        if (_receiveLoopTask is not null)
        {
            try
            {
                await _receiveLoopTask;
            }
            catch
            {
            }
        }
    }

    private async Task ReceiveLoopAsync()
    {
        if (_socket is null)
        {
            return;
        }

        var buffer = new byte[16 * 1024];
        var builder = new StringBuilder();

        try
        {
            while (!_shutdown.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(buffer, _shutdown.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage)
                {
                    continue;
                }

                var message = builder.ToString();
                builder.Clear();
                await _incomingChannel.Writer.WriteAsync(message, _shutdown.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _incomingChannel.Writer.TryComplete(ex);
            return;
        }

        _incomingChannel.Writer.TryComplete();
    }
}
