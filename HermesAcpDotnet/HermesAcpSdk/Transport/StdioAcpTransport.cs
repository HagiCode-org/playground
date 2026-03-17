namespace HermesAcpSdk.Transport;

public sealed class StdioAcpTransport : IAsyncDisposable
{
    private readonly HermesProcessRunner _processRunner;
    private readonly RawTranscriptCapture _transcript;
    private readonly Channel<string> _messages = Channel.CreateUnbounded<string>();
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _pumpTask;

    public StdioAcpTransport(HermesProcessRunner processRunner, RawTranscriptCapture transcript)
    {
        _processRunner = processRunner;
        _transcript = transcript;
    }

    public void Start()
    {
        _pumpTask ??= Task.Run(PumpAsync);
    }

    public async Task SendAsync(string payload, CancellationToken cancellationToken = default)
    {
        _transcript.Record(TranscriptChannel.Outbound, payload);
        await _processRunner.StandardInput.WriteLineAsync(payload.AsMemory(), cancellationToken);
        await _processRunner.StandardInput.FlushAsync(cancellationToken);
    }

    public async IAsyncEnumerable<string> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await _messages.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_messages.Reader.TryRead(out var message))
            {
                yield return message;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _messages.Writer.TryComplete();
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
            while (!_shutdown.IsCancellationRequested)
            {
                var line = await _processRunner.StandardOutput.ReadLineAsync();
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                _transcript.Record(TranscriptChannel.Inbound, line);
                await _messages.Writer.WriteAsync(line, _shutdown.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _messages.Writer.TryComplete();
        }
    }
}
