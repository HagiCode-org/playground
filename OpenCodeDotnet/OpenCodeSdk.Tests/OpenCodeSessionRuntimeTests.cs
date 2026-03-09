namespace OpenCodeSdk.Tests;

public sealed class OpenCodeSessionRuntimeTests
{
    [Fact]
    public async Task DedicatedProcessOptions_CanStartTwoIsolatedProcessesInParallel()
    {
        var firstScript = FakeOpenCodeScript.Create(printReady: true);
        var secondScript = FakeOpenCodeScript.Create(printReady: true);

        await using var first = await OpenCodeProcessManager.StartAsync(new OpenCodeProcessOptions
        {
            ExecutablePath = firstScript.Path,
            StartupTimeout = TimeSpan.FromSeconds(5),
        });
        await using var second = await OpenCodeProcessManager.StartAsync(new OpenCodeProcessOptions
        {
            ExecutablePath = secondScript.Path,
            StartupTimeout = TimeSpan.FromSeconds(5),
        });

        Assert.NotEqual(first.Port, second.Port);
        Assert.NotEqual(first.ProcessId, second.ProcessId);
    }

    [Fact]
    public async Task Integration_SessionLifecycle_IsOptIn()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("OPENCODE_DOTNET_RUN_INTEGRATION"), "1", StringComparison.Ordinal))
        {
            return;
        }

        await using var handle = await OpenCodeSessionRuntime.StartAsync(new OpenCodeSessionOptions
        {
            Directory = "/home/newbe36524/repos/newbe36524/hagicode-mono",
            SessionTitle = "OpenCodeDotnet integration",
            Process = new OpenCodeProcessOptions
            {
                StartupTimeout = TimeSpan.FromSeconds(20),
            },
        });

        var response = await handle.PromptAsync("Reply with the single word READY.");
        Assert.Contains("READY", response.GetTextContent(), StringComparison.OrdinalIgnoreCase);
    }
}
