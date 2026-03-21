namespace KimiAcpSdk.Transport;

public interface IAcpTransport : IAsyncDisposable
{
    void Start();

    Task SendAsync(string payload, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken = default);
}
