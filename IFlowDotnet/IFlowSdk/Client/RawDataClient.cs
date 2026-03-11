using IFlowSdk.Models;

namespace IFlowSdk.Client;

public sealed class RawDataClient : IFlowClient
{
    public RawDataClient(IFlowOptions? options = null) : base(options)
    {
    }

    protected override bool CaptureRawMessages => true;

    public async IAsyncEnumerable<RawMessage> ReceiveRawMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            RawMessage message;
            try
            {
                message = await WaitForRawMessageAsync(cancellationToken);
            }
            catch (ChannelClosedException)
            {
                yield break;
            }

            yield return message;
        }
    }
}
