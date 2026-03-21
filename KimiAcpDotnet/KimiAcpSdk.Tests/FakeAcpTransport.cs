namespace KimiAcpSdk.Tests;

internal sealed class FakeAcpTransport : IAcpTransport
{
    private readonly Func<string, IReadOnlyList<string>> _responder;
    private readonly Channel<string> _messages = Channel.CreateUnbounded<string>();

    public FakeAcpTransport(Func<string, IReadOnlyList<string>> responder)
    {
        _responder = responder;
    }

    public List<string> SentPayloads { get; } = [];

    public void Start()
    {
    }

    public Task SendAsync(string payload, CancellationToken cancellationToken = default)
    {
        SentPayloads.Add(payload);

        foreach (var response in _responder(payload))
        {
            _messages.Writer.TryWrite(response);
        }

        return Task.CompletedTask;
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

    public ValueTask DisposeAsync()
    {
        _messages.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
