using IFlowSdk.Client;
using IFlowSdk.Models;

namespace IFlowSdk.Tests;

public sealed class IntegrationScenarioTests
{
    [Fact]
    public async Task LocalIFlowLifecycle_WorksWhenOptedIn()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFLOW_INTEGRATION"), "1", StringComparison.Ordinal))
        {
            return;
        }

        await using var client = new IFlowClient(new IFlowOptions
        {
            Timeout = TimeSpan.FromSeconds(30),
            AutoStartProcess = true,
        });

        await client.ConnectAsync();
        Assert.False(string.IsNullOrWhiteSpace(client.SessionId));

        await client.SendMessageAsync("Reply with the single word OK");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var message in client.ReceiveMessagesAsync(cts.Token))
        {
            if (message is AssistantMessage or ErrorMessage or TaskFinishMessage)
            {
                Assert.NotNull(message);
                break;
            }
        }
    }
}
