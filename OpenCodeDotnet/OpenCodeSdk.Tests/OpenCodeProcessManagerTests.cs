using System.Diagnostics;

namespace OpenCodeSdk.Tests;

public sealed class OpenCodeProcessManagerTests
{
    [Fact]
    public void TryParseListeningUrl_ReturnsUriFromStartupLine()
    {
        var ok = OpenCodeProcessManager.TryParseListeningUrl(
            "opencode server listening on http://127.0.0.1:42111",
            out var uri);

        Assert.True(ok);
        Assert.NotNull(uri);
        Assert.Equal("http://127.0.0.1:42111/", uri!.ToString());
    }

    [Fact]
    public async Task StartAsync_ThrowsTimeoutWhenServerNeverBecomesReady()
    {
        var script = FakeOpenCodeScript.Create(printReady: false);
        await Assert.ThrowsAsync<TimeoutException>(() => OpenCodeProcessManager.StartAsync(new OpenCodeProcessOptions
        {
            ExecutablePath = script.Path,
            StartupTimeout = TimeSpan.FromMilliseconds(500),
        }));
    }

    [Fact]
    public async Task ProcessHandle_DisposeKillsChildProcess()
    {
        var script = FakeOpenCodeScript.Create(printReady: true);
        var handle = await OpenCodeProcessManager.StartAsync(new OpenCodeProcessOptions
        {
            ExecutablePath = script.Path,
            StartupTimeout = TimeSpan.FromSeconds(5),
        });

        var processId = handle.ProcessId;
        await handle.DisposeAsync();

        Process? process = null;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
        }

        Assert.True(process is null || process.HasExited);
    }
}

internal static class FakeOpenCodeScript
{
    public static (string Path, string Directory) Create(bool printReady)
    {
        var directory = Path.Combine(Path.GetTempPath(), "opencode-dotnet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "fake-opencode.sh");
        var content = string.Join('\n',
        [
            "#!/usr/bin/env bash",
            $"print_ready={(printReady ? 1 : 0)}",
            "port=4096",
            "for arg in \"$@\"; do",
            "  case $arg in",
            "    --port=*) port=${arg#--port=} ;;",
            "  esac",
            "done",
            "if [ \"$print_ready\" = \"1\" ]; then",
            "  echo \"opencode server listening on http://127.0.0.1:$port\"",
            "fi",
            "trap 'exit 0' TERM INT",
            "while true; do sleep 1; done",
        ]);
        File.WriteAllText(path, content + "\n");
        using var chmod = Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            ArgumentList = { "+x", path },
        });
        chmod!.WaitForExit();
        return (path, directory);
    }
}
