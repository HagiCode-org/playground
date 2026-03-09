using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenCodeSdk;

public static class OpenCodeProcessManager
{
    private static readonly Regex ListeningRegex = new(@"opencode server listening.* on\s+(https?://[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static async Task<OpenCodeProcessHandle> StartAsync(
        OpenCodeProcessOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new OpenCodeProcessOptions();

        var executable = string.IsNullOrWhiteSpace(options.ExecutablePath)
            ? ResolveExecutablePath()
            : options.ExecutablePath!;
        var port = options.Port ?? GetFreeTcpPort();
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("serve");
        startInfo.ArgumentList.Add($"--hostname={options.Hostname}");
        startInfo.ArgumentList.Add($"--port={port}");

        if (!string.IsNullOrWhiteSpace(options.LogLevel))
        {
            startInfo.ArgumentList.Add($"--log-level={options.LogLevel}");
        }

        if (options.EnvironmentVariables is not null)
        {
            foreach (var pair in options.EnvironmentVariables)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        if (options.Config is not null)
        {
            startInfo.Environment["OPENCODE_CONFIG_CONTENT"] = options.Config.ToJsonString();
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var output = new StringBuilder();
        var readySource = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start OpenCode executable '{executable}'.");
        }

        var stdoutPump = PumpAsync(process.StandardOutput, output, line =>
        {
            if (TryParseListeningUrl(line, out var uri) && uri is not null)
            {
                readySource.TrySetResult(uri);
            }
        }, cancellationToken);
        var stderrPump = PumpAsync(process.StandardError, output, _ => { }, cancellationToken);
        var exitedTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(options.StartupTimeout, cancellationToken);

        var completed = await Task.WhenAny(readySource.Task, exitedTask, timeoutTask);
        if (completed == readySource.Task)
        {
            return new OpenCodeProcessHandle(process, readySource.Task.Result, port, output, stdoutPump, stderrPump);
        }

        TryKill(process);
        await Task.WhenAll(SuppressAsync(stdoutPump), SuppressAsync(stderrPump));

        if (completed == timeoutTask)
        {
            throw new TimeoutException($"Timed out waiting for OpenCode to listen after {options.StartupTimeout}. Output: {output}");
        }

        throw new InvalidOperationException($"OpenCode process exited before it was ready. Output: {output}");
    }

    public static bool TryParseListeningUrl(string line, out Uri? uri)
    {
        var match = ListeningRegex.Match(line);
        if (match.Success && Uri.TryCreate(match.Groups[1].Value, UriKind.Absolute, out var parsed))
        {
            uri = parsed;
            return true;
        }

        uri = null;
        return false;
    }

    private static async Task PumpAsync(
        StreamReader reader,
        StringBuilder output,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line is null)
            {
                break;
            }

            lock (output)
            {
                output.AppendLine(line);
            }

            onLine(line);
        }
    }

    private static async Task SuppressAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string ResolveExecutablePath()
    {
        var overridePath = Environment.GetEnvironmentVariable("OPENCODE_EXECUTABLE");
        return string.IsNullOrWhiteSpace(overridePath) ? "opencode" : overridePath;
    }
}

public sealed class OpenCodeProcessHandle : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _output;
    private readonly Task _stdoutPump;
    private readonly Task _stderrPump;
    private int _disposed;

    internal OpenCodeProcessHandle(
        Process process,
        Uri baseUri,
        int port,
        StringBuilder output,
        Task stdoutPump,
        Task stderrPump)
    {
        _process = process;
        BaseUri = baseUri;
        Port = port;
        _output = output;
        _stdoutPump = stdoutPump;
        _stderrPump = stderrPump;
    }

    public Uri BaseUri { get; }

    public int Port { get; }

    public int ProcessId => _process.Id;

    public string CapturedOutput
    {
        get
        {
            lock (_output)
            {
                return _output.ToString();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }

        try
        {
            await _process.WaitForExitAsync();
        }
        catch
        {
        }

        try
        {
            await Task.WhenAll(_stdoutPump, _stderrPump);
        }
        catch
        {
        }

        _process.Dispose();
    }
}
