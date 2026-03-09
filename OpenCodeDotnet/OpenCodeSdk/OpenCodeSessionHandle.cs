using OpenCodeSdk.Generated;

namespace OpenCodeSdk;

public sealed class OpenCodeSessionHandle : IAsyncDisposable
{
    private readonly OpenCodeProcessHandle? _processHandle;
    private int _disposed;

    internal OpenCodeSessionHandle(OpenCodeClient client, OpenCodeSession session, OpenCodeProcessHandle? processHandle)
    {
        Client = client;
        Session = session;
        _processHandle = processHandle;
    }

    public OpenCodeClient Client { get; }

    public OpenCodeSession Session { get; private set; }

    public bool OwnsProcess => _processHandle is not null;

    public int? ProcessId => _processHandle?.ProcessId;

    public async Task<OpenCodeMessageEnvelope> PromptAsync(
        string prompt,
        OpenCodeModelSelection? model = null,
        string? agent = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await Client.Session.PromptTextAsync(Session.Id, prompt, model, agent, cancellationToken);
    }

    public async Task<IReadOnlyList<OpenCodeMessageEnvelope>> GetMessagesAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await Client.Session.MessagesAsync(Session.Id, limit, cancellationToken);
    }

    public async Task<OpenCodeSessionRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var messages = await Client.Session.MessagesAsync(Session.Id, limit: 20, cancellationToken: cancellationToken);
        return new OpenCodeSessionRuntimeStatus
        {
            SessionId = Session.Id,
            SessionTitle = Session.Title,
            BaseUri = Client.BaseUri,
            ProcessId = ProcessId,
            OwnsProcess = OwnsProcess,
            MessageCount = messages.Count,
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (_processHandle is not null)
        {
            await _processHandle.DisposeAsync();
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            throw new ObjectDisposedException(nameof(OpenCodeSessionHandle));
        }
    }
}
