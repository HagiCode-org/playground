using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace GitCompatibilityTest;

internal enum ValidationMode
{
    Compatibility,
    Benchmark,
    Summarize
}

internal enum ValidationExitCode
{
    Success = 0,
    CompatibilityFailed = 1,
    BenchmarkFailed = 2,
    InvalidArguments = 64
}

internal sealed class PlatformMetadata
{
    public string OsDescription { get; init; } = string.Empty;
    public string OsArchitecture { get; init; } = string.Empty;
    public string ProcessArchitecture { get; init; } = string.Empty;
    public string RuntimeIdentifier { get; init; } = string.Empty;
    public string FrameworkDescription { get; init; } = string.Empty;
    public string DotnetVersion { get; init; } = string.Empty;
    public string? GithubRunnerOs { get; init; }
    public string? GithubRunnerArch { get; init; }
    public HardwareMetadata Hardware { get; init; } = new();

    public static PlatformMetadata Capture()
    {
        return new PlatformMetadata
        {
            OsDescription = RuntimeInformation.OSDescription,
            OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            DotnetVersion = Environment.Version.ToString(),
            GithubRunnerOs = Environment.GetEnvironmentVariable("RUNNER_OS"),
            GithubRunnerArch = Environment.GetEnvironmentVariable("RUNNER_ARCH"),
            Hardware = HardwareInfoProvider.Capture()
        };
    }
}

internal sealed class HardwareMetadata
{
    public string MachineName { get; init; } = string.Empty;
    public int LogicalCoreCount { get; init; }
    public string? CpuModel { get; init; }
    public long? TotalMemoryBytes { get; init; }
}

internal sealed class FailureDetails
{
    public string Type { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? StackTrace { get; init; }

    public static FailureDetails FromException(Exception exception)
    {
        return new FailureDetails
        {
            Type = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.StackTrace
        };
    }
}

internal sealed class ValidationCheckResult
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public Dictionary<string, string> Details { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public FailureDetails? Failure { get; init; }
}

internal sealed class CompatibilityRunResult
{
    public string SampleName { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset CompletedAtUtc { get; init; }
    public string RepositoryPath { get; init; } = string.Empty;
    public PlatformMetadata Platform { get; init; } = new();
    public List<ValidationCheckResult> Checks { get; init; } = [];

    [JsonIgnore]
    public bool AllPassed => Checks.All(check => check.Passed);

    [JsonIgnore]
    public int PassedCount => Checks.Count(check => check.Passed);
}

internal sealed class FixtureRepositoryInfo
{
    public string RepositoryPath { get; init; } = string.Empty;
    public string InitialBranch { get; init; } = string.Empty;
    public int CommitCount { get; init; }
    public int TrackedFileCount { get; init; }
    public int DirtyEntryCount { get; init; }
}

internal sealed class SqliteFixtureInfo
{
    public string DatabasePath { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;
    public int RowCount { get; init; }
}

internal sealed class BenchmarkScenarioResult
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int WarmupIterations { get; init; }
    public int MeasuredIterations { get; init; }
    public bool Passed { get; init; }
    public string Stage { get; init; } = "measured";
    public List<double> SamplesMilliseconds { get; init; } = [];
    public FailureDetails? Failure { get; init; }

    [JsonIgnore]
    public double? MeanMilliseconds => SamplesMilliseconds.Count == 0 ? null : SamplesMilliseconds.Average();

    [JsonIgnore]
    public double? MedianMilliseconds
    {
        get
        {
            if (SamplesMilliseconds.Count == 0)
            {
                return null;
            }

            var ordered = SamplesMilliseconds.OrderBy(value => value).ToArray();
            var midpoint = ordered.Length / 2;
            return ordered.Length % 2 == 0
                ? (ordered[midpoint - 1] + ordered[midpoint]) / 2.0
                : ordered[midpoint];
        }
    }

    [JsonIgnore]
    public double? MinMilliseconds => SamplesMilliseconds.Count == 0 ? null : SamplesMilliseconds.Min();

    [JsonIgnore]
    public double? MaxMilliseconds => SamplesMilliseconds.Count == 0 ? null : SamplesMilliseconds.Max();
}

internal sealed class ReadmeRefreshInfo
{
    public string Status { get; init; } = string.Empty;
    public string? ReadmePath { get; init; }
    public string? SummaryArtifactPath { get; init; }
}

internal sealed class BenchmarkRunResult
{
    public string SampleName { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset CompletedAtUtc { get; init; }
    public PlatformMetadata Platform { get; init; } = new();
    public FixtureRepositoryInfo Fixture { get; init; } = new();
    public SqliteFixtureInfo SqliteFixture { get; init; } = new();
    public int WarmupIterations { get; init; }
    public int MeasuredIterations { get; init; }
    public Dictionary<string, string> StatusOptions { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<BenchmarkScenarioResult> Scenarios { get; init; } = [];
    public ReadmeRefreshInfo? ReadmeRefresh { get; set; }

    [JsonIgnore]
    public bool AllPassed => Scenarios.All(scenario => scenario.Passed);
}
