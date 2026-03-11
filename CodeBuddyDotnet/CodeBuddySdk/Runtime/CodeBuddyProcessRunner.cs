using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using CodeBuddySdk.Configuration;

namespace CodeBuddySdk.Runtime;

public sealed class CodeBuddyProcessRunner : IProcessRunner
{
    public async Task<RawProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var events = new List<RawProcessEvent>();
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        using var process = new Process
        {
            StartInfo = BuildStartInfo(request),
            EnableRaisingEvents = true,
        };

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                return;
            }

            lock (events)
            {
                events.Add(new RawProcessEvent(DateTimeOffset.UtcNow, "stdout", eventArgs.Data));
                stdOut.AppendLine(eventArgs.Data);
            }
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                return;
            }

            lock (events)
            {
                events.Add(new RawProcessEvent(DateTimeOffset.UtcNow, "stderr", eventArgs.Data));
                stdErr.AppendLine(eventArgs.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            stopwatch.Stop();
            return new RawProcessResult
            {
                Duration = stopwatch.Elapsed,
                CommandDescription = BuildCommandDescription(request),
                StartFailureCategory = ProcessFailureCategory.MissingExecutable,
                StartFailureMessage = ex.Message,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new RawProcessResult
            {
                Duration = stopwatch.Elapsed,
                CommandDescription = BuildCommandDescription(request),
                StartFailureCategory = ProcessFailureCategory.Unknown,
                StartFailureMessage = ex.Message,
            };
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (request.PromptTransport == PromptTransport.Stdin && request.InputText is not null)
        {
            await process.StandardInput.WriteAsync(request.InputText.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
            process.StandardInput.Close();
        }

        var waitForExitTask = process.WaitForExitAsync(cancellationToken);
        var delayTask = Task.Delay(request.Timeout, cancellationToken);
        var completedTask = await Task.WhenAny(waitForExitTask, delayTask);

        if (completedTask == delayTask)
        {
            TryKill(process);
            stopwatch.Stop();
            await waitForExitTask.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

            return new RawProcessResult
            {
                TimedOut = true,
                Duration = stopwatch.Elapsed,
                StdOut = stdOut.ToString(),
                StdErr = stdErr.ToString(),
                Events = events.ToArray(),
                CommandDescription = BuildCommandDescription(request),
            };
        }

        await waitForExitTask;
        stopwatch.Stop();

        return new RawProcessResult
        {
            ExitCode = process.ExitCode,
            Duration = stopwatch.Elapsed,
            StdOut = stdOut.ToString(),
            StdErr = stdErr.ToString(),
            Events = events.ToArray(),
            CommandDescription = BuildCommandDescription(request),
        };
    }

    private static ProcessStartInfo BuildStartInfo(ProcessRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in request.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (var pair in request.EnvironmentVariables)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        return startInfo;
    }

    private static string BuildCommandDescription(ProcessRequest request)
    {
        var parts = new List<string> { request.ExecutablePath };
        parts.AddRange(request.Arguments.Select(QuoteIfNeeded));
        return string.Join(' ', parts);
    }

    private static string QuoteIfNeeded(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
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
}
