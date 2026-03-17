namespace HermesAcpSdk.Tests;

internal static class IntegrationTestSupport
{
    public static bool IsOptedIn(string variableName)
    {
        return string.Equals(Environment.GetEnvironmentVariable(variableName), "1", StringComparison.OrdinalIgnoreCase);
    }

    public static string CreateTempDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static bool CanRun(string executable, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            process?.WaitForExit(3000);
            return process is not null && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static string FindMonorepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var agentsPath = Path.Combine(directory.FullName, "AGENTS.md");
            var packageJsonPath = Path.Combine(directory.FullName, "package.json");
            if (File.Exists(agentsPath) && File.Exists(packageJsonPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the monorepo root from the test base directory.");
    }

    public static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
