using IFlowSdk.Exceptions;
using IFlowSdk.Models;
using System.Net.Sockets;

namespace IFlowSdk.Runtime;

public sealed class IFlowProcessManager : IAsyncDisposable
{
    private readonly List<string> _stdoutLines = new();
    private readonly List<string> _stderrLines = new();
    private readonly object _gate = new();
    private Process? _process;
    private Task? _stdoutTask;
    private Task? _stderrTask;

    public int? Port { get; private set; }

    public string? ExecutablePath { get; private set; }

    public int? ProcessId => _process?.Id;

    public bool OwnsProcess => _process is not null && !_process.HasExited;

    public async Task<string> StartAsync(IFlowOptions options, CancellationToken cancellationToken = default)
    {
        if (_process is not null && !_process.HasExited && Port.HasValue)
        {
            return $"ws://localhost:{Port.Value}/acp";
        }

        ExecutablePath = ResolveExecutablePath(options.ExecutablePath);
        Port = FindAvailablePort(options.ProcessStartPort);

        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--experimental-acp");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(Port.Value.ToString());

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!_process.Start())
        {
            throw new IFlowProcessException("Failed to start iFlow process.");
        }

        _stdoutTask = Task.Run(() => DrainAsync(_process.StandardOutput, _stdoutLines, cancellationToken));
        _stderrTask = Task.Run(() => DrainAsync(_process.StandardError, _stderrLines, cancellationToken));

        await WaitForPortAsync(Port.Value, options.Timeout, cancellationToken);
        return $"ws://localhost:{Port.Value}/acp";
    }

    public async Task StopAsync()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        catch
        {
        }
    }

    public string GetDiagnostics()
    {
        lock (_gate)
        {
            var stdout = string.Join(Environment.NewLine, _stdoutLines.TakeLast(50));
            var stderr = string.Join(Environment.NewLine, _stderrLines.TakeLast(50));
            return $"Executable: {ExecutablePath}{Environment.NewLine}Port: {Port}{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_stdoutTask is not null)
        {
            await _stdoutTask;
        }

        if (_stderrTask is not null)
        {
            await _stderrTask;
        }

        _process?.Dispose();
    }

    public static string ResolveExecutablePath(string? overridePath = null)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (File.Exists(overridePath))
            {
                return overridePath;
            }

            throw new IFlowProcessException($"Configured iFlow executable does not exist: {overridePath}");
        }

        var envOverride = Environment.GetEnvironmentVariable("IFLOW_EXECUTABLE");
        if (!string.IsNullOrWhiteSpace(envOverride) && File.Exists(envOverride))
        {
            return envOverride;
        }

        var path = FindOnPath("iflow");
        if (path is not null)
        {
            return path;
        }

        throw new IFlowProcessException("Unable to find `iflow` on PATH. Set IFLOW_EXECUTABLE or provide IFlowOptions.ExecutablePath.");
    }

    public static int FindAvailablePort(int startPort, int maxAttempts = 100)
    {
        for (var port = startPort; port < startPort + maxAttempts; port++)
        {
            if (IsPortAvailable(port))
            {
                return port;
            }
        }

        throw new IFlowProcessException($"No available TCP port found in range {startPort}-{startPort + maxAttempts - 1}.");
    }

    public static bool IsEndpointListening(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        using var client = new TcpClient();
        try
        {
            var task = client.ConnectAsync(uri.Host, uri.Port);
            return task.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(System.Net.IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindOnPath(string executableName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", string.Empty }
            : new[] { string.Empty };

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, executableName + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private async Task WaitForPortAsync(int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - startedAt < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_process is not null && _process.HasExited)
            {
                throw new IFlowProcessException($"iFlow exited before ACP was ready.{Environment.NewLine}{GetDiagnostics()}");
            }

            if (IsEndpointListening($"ws://localhost:{port}/acp"))
            {
                return;
            }

            await Task.Delay(200, cancellationToken);
        }

        throw new IFlowProcessException($"Timed out waiting for iFlow ACP to listen on port {port}.{Environment.NewLine}{GetDiagnostics()}");
    }

    private void CaptureLine(List<string> target, string line)
    {
        lock (_gate)
        {
            target.Add(line);
            if (target.Count > 200)
            {
                target.RemoveAt(0);
            }
        }
    }

    private async Task DrainAsync(StreamReader reader, List<string> target, CancellationToken cancellationToken)
    {
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            CaptureLine(target, line);
        }
    }
}
