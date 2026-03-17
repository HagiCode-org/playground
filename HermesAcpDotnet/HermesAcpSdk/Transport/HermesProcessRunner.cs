using HermesAcpSdk.Configuration;

namespace HermesAcpSdk.Transport;

public sealed class HermesProcessRunner : IAsyncDisposable
{
    private readonly Process _process;
    private readonly RawTranscriptCapture _transcript;
    private readonly StringBuilder _standardError = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _stderrPump;

    private HermesProcessRunner(Process process, RawTranscriptCapture transcript)
    {
        _process = process;
        _transcript = transcript;
        _stderrPump = Task.Run(PumpStandardErrorAsync);
    }

    public StreamWriter StandardInput => _process.StandardInput;

    public StreamReader StandardOutput => _process.StandardOutput;

    public int? ProcessId => _process.HasExited ? null : _process.Id;

    public static Task<HermesProcessRunner> StartAsync(HermesLaunchProfile profile, RawTranscriptCapture transcript, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = profile.ExecutablePath,
            WorkingDirectory = profile.WorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in profile.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var pair in profile.EnvironmentVariables)
        {
            startInfo.Environment[pair.Key] = pair.Value ?? string.Empty;
        }

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start Hermes executable '{profile.ExecutablePath}'.");
        }

        transcript.Record(TranscriptChannel.Info, $"launch: {profile.ExecutablePath} {string.Join(' ', profile.Arguments)}");
        return Task.FromResult(new HermesProcessRunner(process, transcript));
    }

    public string GetStandardError()
    {
        return _standardError.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();

        try
        {
            StandardInput.Close();
        }
        catch
        {
        }

        if (!_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
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
            await _stderrPump;
        }
        catch
        {
        }

        _process.Dispose();
        _shutdown.Dispose();
    }

    private async Task PumpStandardErrorAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                var line = await _process.StandardError.ReadLineAsync();
                if (line is null)
                {
                    break;
                }

                if (_standardError.Length > 0)
                {
                    _standardError.AppendLine();
                }

                _standardError.Append(line);
                _transcript.Record(TranscriptChannel.Stderr, line);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
