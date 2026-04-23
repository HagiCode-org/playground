using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace GitCompatibilityTest;

internal static class HardwareInfoProvider
{
    public static HardwareMetadata Capture()
    {
        return new HardwareMetadata
        {
            MachineName = Environment.MachineName,
            LogicalCoreCount = Environment.ProcessorCount,
            CpuModel = TryGetCpuModel(),
            TotalMemoryBytes = TryGetTotalMemoryBytes()
        };
    }

    private static string? TryGetCpuModel()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return TryReadLinuxCpuModel();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return TryRunCommand("sysctl", "-n machdep.cpu.brand_string");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return TryRunWindowsPowerShell("(Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name)");
        }

        return null;
    }

    private static long? TryGetTotalMemoryBytes()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return TryReadLinuxMemoryBytes();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var output = TryRunCommand("sysctl", "-n hw.memsize");
            if (long.TryParse(output, NumberStyles.Integer, CultureInfo.InvariantCulture, out var macBytes))
            {
                return macBytes;
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var output = TryRunWindowsPowerShell("(Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory");
            if (long.TryParse(output, NumberStyles.Integer, CultureInfo.InvariantCulture, out var windowsBytes))
            {
                return windowsBytes;
            }
        }

        var fallback = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return fallback > 0 ? fallback : null;
    }

    private static string? TryReadLinuxCpuModel()
    {
        const string cpuInfoPath = "/proc/cpuinfo";
        if (!File.Exists(cpuInfoPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(cpuInfoPath))
        {
            if (!line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0 || separatorIndex + 1 >= line.Length)
            {
                continue;
            }

            return line[(separatorIndex + 1)..].Trim();
        }

        return null;
    }

    private static long? TryReadLinuxMemoryBytes()
    {
        const string memInfoPath = "/proc/meminfo";
        if (!File.Exists(memInfoPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(memInfoPath))
        {
            if (!line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
            {
                return null;
            }

            if (long.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kibibytes))
            {
                return kibibytes * 1024;
            }

            return null;
        }

        return null;
    }

    private static string? TryRunWindowsPowerShell(string script)
    {
        return TryRunCommand("pwsh", $"-NoProfile -Command \"{script}\"")
               ?? TryRunCommand("powershell", $"-NoProfile -Command \"{script}\"");
    }

    private static string? TryRunCommand(string fileName, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? output
                : null;
        }
        catch
        {
            return null;
        }
    }
}
