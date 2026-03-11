using System.Diagnostics;
using CodeBuddySdk.Configuration;
using CodeBuddySdk.Runtime;

namespace CodeBuddySdk.Tests;

public sealed class CodeBuddyProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_ReturnsTimeoutWhenProcessDoesNotExit()
    {
        var script = CreateShellScript(
            """
            #!/usr/bin/env bash
            trap 'exit 0' TERM INT
            while true; do sleep 1; done
            """);

        var runner = new CodeBuddyProcessRunner();
        var result = await runner.RunAsync(new ProcessRequest
        {
            ExecutablePath = script,
            WorkingDirectory = Path.GetDirectoryName(script)!,
            Arguments = Array.Empty<string>(),
            PromptTransport = PromptTransport.Stdin,
            InputText = "hello",
            EnvironmentVariables = new Dictionary<string, string>(),
            Timeout = TimeSpan.FromMilliseconds(200),
        }, CancellationToken.None);

        Assert.True(result.TimedOut);
    }

    [Fact]
    public void NormalizeFixture_ClassifiesAuthenticationErrors()
    {
        var client = new CodeBuddyProcessClient();
        var result = client.NormalizeFixture(new CodeBuddyExecutionRequest
        {
            ScenarioName = "auth-classification",
            Prompt = "fixture",
            Mode = ExecutionMode.Fixture,
            Timeout = TimeSpan.FromSeconds(1),
        }, new RawProcessResult
        {
            ExitCode = 1,
            StdErr = "Unauthorized token. Please login.",
            Events =
            [
                new RawProcessEvent(DateTimeOffset.UtcNow, "stderr", "Unauthorized token. Please login."),
            ],
        });

        Assert.False(result.Success);
        Assert.Equal(ProcessFailureCategory.Authentication, result.FailureCategory);
    }

    private static string CreateShellScript(string content)
    {
        var directory = Path.Combine(Path.GetTempPath(), "codebuddy-dotnet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "fake-codebuddy.sh");
        File.WriteAllText(path, content + Environment.NewLine);
        using var chmod = Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            ArgumentList = { "+x", path },
        });
        chmod!.WaitForExit();
        return path;
    }
}
