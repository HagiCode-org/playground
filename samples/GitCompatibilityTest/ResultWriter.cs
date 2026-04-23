using System.Globalization;
using System.Text;
using System.Text.Json;
using LibGit2Sharp;

namespace GitCompatibilityTest;

internal static class ResultWriter
{
    private const string ReadmeSummaryStartMarker = "<!-- git-cross-os-benchmark-summary:start -->";
    private const string ReadmeSummaryEndMarker = "<!-- git-cross-os-benchmark-summary:end -->";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void WriteCompatibilityArtifacts(string outputDirectory, CompatibilityRunResult result)
    {
        var jsonPath = Path.Combine(outputDirectory, "compatibility-report.json");
        var markdownPath = Path.Combine(outputDirectory, "compatibility-report.md");

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        File.WriteAllText(markdownPath, BuildCompatibilityMarkdown(result));
    }

    public static ReadmeRefreshInfo WriteBenchmarkArtifacts(
        string outputDirectory,
        BenchmarkRunResult result,
        bool refreshReadme,
        string? readmePath)
    {
        var jsonPath = Path.Combine(outputDirectory, "benchmark-results.json");
        var csvPath = Path.Combine(outputDirectory, "benchmark-results.csv");
        var summaryPath = Path.Combine(outputDirectory, "benchmark-summary.md");

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        File.WriteAllText(csvPath, BuildBenchmarkCsv(result));

        ReadmeRefreshInfo readmeRefresh;
        if (refreshReadme && !string.IsNullOrWhiteSpace(readmePath))
        {
            var summaryMarkdown = BuildSingleRunReadmeSummary(result, outputDirectory, "local");
            readmeRefresh = UpdateReadmeSummary(readmePath, summaryMarkdown);
            File.WriteAllText(summaryPath, BuildBenchmarkMarkdown(result, outputDirectory, readmeRefresh));
        }
        else
        {
            readmeRefresh = new ReadmeRefreshInfo
            {
                Status = "skipped",
                ReadmePath = readmePath
            };
            File.WriteAllText(summaryPath, BuildBenchmarkMarkdown(result, outputDirectory, readmeRefresh));
        }

        return readmeRefresh;
    }

    public static Dictionary<string, string> DescribeStatusOptions(StatusOptions options)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["includeUntracked"] = options.IncludeUntracked.ToString(),
            ["recurseUntrackedDirs"] = options.RecurseUntrackedDirs.ToString(),
            ["detectRenamesInIndex"] = options.DetectRenamesInIndex.ToString(),
            ["detectRenamesInWorkDir"] = options.DetectRenamesInWorkDir.ToString()
        };
    }

    public static List<BenchmarkRunResult> LoadBenchmarkResults(string resultsRoot)
    {
        return Directory
            .EnumerateFiles(resultsRoot, "benchmark-results.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => JsonSerializer.Deserialize<BenchmarkRunResult>(File.ReadAllText(path), JsonOptions))
            .Where(result => result is not null)
            .Cast<BenchmarkRunResult>()
            .ToList();
    }

    public static ReadmeRefreshInfo UpdateReadmeSummary(string readmePath, string summaryMarkdown)
    {
        var absoluteReadmePath = Path.GetFullPath(readmePath);
        var readmeContent = File.Exists(absoluteReadmePath)
            ? File.ReadAllText(absoluteReadmePath)
            : string.Empty;

        var replacement = new StringBuilder()
            .AppendLine(ReadmeSummaryStartMarker)
            .AppendLine(summaryMarkdown.Trim())
            .AppendLine(ReadmeSummaryEndMarker)
            .ToString();

        string updatedContent;
        if (readmeContent.Contains(ReadmeSummaryStartMarker, StringComparison.Ordinal) &&
            readmeContent.Contains(ReadmeSummaryEndMarker, StringComparison.Ordinal))
        {
            var startIndex = readmeContent.IndexOf(ReadmeSummaryStartMarker, StringComparison.Ordinal);
            var endIndex = readmeContent.IndexOf(ReadmeSummaryEndMarker, StringComparison.Ordinal);
            updatedContent = readmeContent[..startIndex] +
                             replacement +
                             readmeContent[(endIndex + ReadmeSummaryEndMarker.Length)..];
        }
        else
        {
            updatedContent = readmeContent.TrimEnd() +
                             Environment.NewLine +
                             Environment.NewLine +
                             "## 最近一次手动执行摘要" +
                             Environment.NewLine +
                             replacement;
        }

        File.WriteAllText(absoluteReadmePath, updatedContent);

        return new ReadmeRefreshInfo
        {
            Status = "updated",
            ReadmePath = absoluteReadmePath
        };
    }

    public static string BuildHardwareSummary(HardwareMetadata hardware)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(hardware.CpuModel))
        {
            parts.Add(hardware.CpuModel);
        }

        parts.Add($"{hardware.LogicalCoreCount} logical cores");

        if (hardware.TotalMemoryBytes.HasValue)
        {
            parts.Add($"{FormatBytes(hardware.TotalMemoryBytes.Value)} RAM");
        }

        if (!string.IsNullOrWhiteSpace(hardware.MachineName))
        {
            parts.Add($"machine={hardware.MachineName}");
        }

        return string.Join("; ", parts);
    }

    public static string BuildCompatibilityConsoleReport(CompatibilityRunResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Compatibility report");
        builder.AppendLine("--------------------");
        builder.AppendLine($"Overall status: {(result.AllPassed ? "PASS" : "FAIL")}");
        builder.AppendLine($"Checks passed: {result.PassedCount}/{result.Checks.Count}");
        builder.AppendLine($"Repository path: {result.RepositoryPath}");
        builder.AppendLine($"Hardware: {BuildHardwareSummary(result.Platform.Hardware)}");
        builder.AppendLine();

        foreach (var check in result.Checks)
        {
            builder.AppendLine($"[{(check.Passed ? "PASS" : "FAIL")}] {check.DisplayName} ({check.Id})");
            foreach (var detail in check.Details)
            {
                builder.AppendLine($"  - {detail.Key}: {detail.Value}");
            }

            if (check.Failure is not null)
            {
                builder.AppendLine($"  - exceptionType: {check.Failure.Type}");
                builder.AppendLine($"  - message: {check.Failure.Message}");
            }
        }

        return builder.ToString();
    }

    public static string BuildBenchmarkConsoleReport(BenchmarkRunResult result, string outputDirectory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Benchmark report");
        builder.AppendLine("----------------");
        builder.AppendLine($"Overall status: {(result.AllPassed ? "PASS" : "FAIL")}");
        builder.AppendLine($"Fixture repository: {result.Fixture.RepositoryPath}");
        builder.AppendLine($"SQLite fixture: {result.SqliteFixture.DatabasePath}");
        builder.AppendLine($"Output directory: {outputDirectory}");
        builder.AppendLine($"Hardware: {BuildHardwareSummary(result.Platform.Hardware)}");
        builder.AppendLine();

        foreach (var scenario in result.Scenarios)
        {
            builder.AppendLine($"[{(scenario.Passed ? "PASS" : "FAIL")}] {scenario.Name}");
            builder.AppendLine($"  meanMs={FormatNullableDouble(scenario.MeanMilliseconds)} medianMs={FormatNullableDouble(scenario.MedianMilliseconds)} minMs={FormatNullableDouble(scenario.MinMilliseconds)} maxMs={FormatNullableDouble(scenario.MaxMilliseconds)}");
            if (scenario.Failure is not null)
            {
                builder.AppendLine($"  exceptionType={scenario.Failure.Type}");
                builder.AppendLine($"  message={scenario.Failure.Message}");
            }
        }

        builder.AppendLine();
        builder.AppendLine($"README refresh: {result.ReadmeRefresh?.Status ?? "not-recorded"}");
        return builder.ToString();
    }

    public static string BuildAggregateConsoleReport(
        IReadOnlyCollection<BenchmarkRunResult> results,
        string summaryPath,
        ReadmeRefreshInfo? refreshInfo)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Aggregate benchmark summary");
        builder.AppendLine("---------------------------");
        builder.AppendLine($"Runner result count: {results.Count}");
        builder.AppendLine($"Summary markdown: {summaryPath}");
        builder.AppendLine($"README refresh: {refreshInfo?.Status ?? "skipped"}");

        foreach (var result in results.OrderBy(item => item.Platform.OsDescription, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {result.Platform.OsDescription} / {result.Platform.ProcessArchitecture}: {(result.AllPassed ? "PASS" : "FAIL")} [{BuildHardwareSummary(result.Platform.Hardware)}]");
        }

        return builder.ToString();
    }

    public static string BuildAggregateMarkdownSummary(
        IReadOnlyCollection<BenchmarkRunResult> results,
        string resultsRoot)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"_Generated at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss 'UTC'} from `{resultsRoot}`._");
        builder.AppendLine();
        builder.AppendLine("| Runner | Hardware | Status | repository-open median (ms) | status-scan median (ms) | branch-lookup median (ms) | head-commit-lookup median (ms) | sqlite-ef-query median (ms) | sqlite-linq2db-query median (ms) |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |");

        foreach (var result in results.OrderBy(item => item.Platform.OsDescription, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(
                $"| {EscapePipes(BuildRunnerLabel(result.Platform))} | {EscapePipes(BuildHardwareSummary(result.Platform.Hardware))} | {(result.AllPassed ? "PASS" : "FAIL")} | {FormatScenarioMedian(result, "repository-open")} | {FormatScenarioMedian(result, "status-scan")} | {FormatScenarioMedian(result, "branch-lookup")} | {FormatScenarioMedian(result, "head-commit-lookup")} | {FormatScenarioMedian(result, "sqlite-ef-query")} | {FormatScenarioMedian(result, "sqlite-linq2db-query")} |");
        }

        builder.AppendLine();
        builder.AppendLine("Artifacts remain per runner in the workflow downloads. Compare medians alongside hardware metadata, then inspect JSON/CSV for raw samples and failure details.");

        return builder.ToString();
    }

    private static string BuildCompatibilityMarkdown(CompatibilityRunResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Compatibility Report");
        builder.AppendLine();
        builder.AppendLine($"- Sample: `{result.SampleName}`");
        builder.AppendLine($"- Mode: `{result.Mode}`");
        builder.AppendLine($"- Started: `{result.StartedAtUtc:O}`");
        builder.AppendLine($"- Completed: `{result.CompletedAtUtc:O}`");
        builder.AppendLine($"- Repository: `{result.RepositoryPath}`");
        builder.AppendLine($"- Platform: `{BuildRunnerLabel(result.Platform)}`");
        builder.AppendLine($"- Hardware: `{BuildHardwareSummary(result.Platform.Hardware)}`");
        builder.AppendLine($"- Overall status: `{(result.AllPassed ? "PASS" : "FAIL")}`");
        builder.AppendLine();
        builder.AppendLine("| Check | Status | Details |");
        builder.AppendLine("| --- | --- | --- |");

        foreach (var check in result.Checks)
        {
            var details = check.Passed
                ? string.Join("<br>", check.Details.Select(item => $"{item.Key}: {item.Value}"))
                : $"{check.Failure?.Type}: {check.Failure?.Message}";
            builder.AppendLine($"| `{check.Id}` | {(check.Passed ? "PASS" : "FAIL")} | {EscapePipes(details)} |");
        }

        return builder.ToString();
    }

    private static string BuildBenchmarkMarkdown(BenchmarkRunResult result, string outputDirectory, ReadmeRefreshInfo readmeRefresh)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Benchmark Summary");
        builder.AppendLine();
        builder.AppendLine($"- Sample: `{result.SampleName}`");
        builder.AppendLine($"- Mode: `{result.Mode}`");
        builder.AppendLine($"- Platform: `{BuildRunnerLabel(result.Platform)}`");
        builder.AppendLine($"- Hardware: `{BuildHardwareSummary(result.Platform.Hardware)}`");
        builder.AppendLine($"- Fixture repository: `{result.Fixture.RepositoryPath}`");
        builder.AppendLine($"- SQLite fixture: `{result.SqliteFixture.DatabasePath}`");
        builder.AppendLine($"- Warmup iterations: `{result.WarmupIterations}`");
        builder.AppendLine($"- Measured iterations: `{result.MeasuredIterations}`");
        builder.AppendLine($"- Output directory: `{outputDirectory}`");
        builder.AppendLine($"- README refresh: `{readmeRefresh.Status}`");
        builder.AppendLine();
        builder.AppendLine("## Scenarios");
        builder.AppendLine();
        builder.AppendLine("| Scenario | Status | Mean (ms) | Median (ms) | Min (ms) | Max (ms) | Notes |");
        builder.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | --- |");

        foreach (var scenario in result.Scenarios)
        {
            var notes = scenario.Passed
                ? $"samples={scenario.SamplesMilliseconds.Count}"
                : $"{scenario.Failure?.Type}: {scenario.Failure?.Message}";
            builder.AppendLine(
                $"| `{scenario.Name}` | {(scenario.Passed ? "PASS" : "FAIL")} | {FormatNullableDouble(scenario.MeanMilliseconds)} | {FormatNullableDouble(scenario.MedianMilliseconds)} | {FormatNullableDouble(scenario.MinMilliseconds)} | {FormatNullableDouble(scenario.MaxMilliseconds)} | {EscapePipes(notes)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Artifact files");
        builder.AppendLine();
        builder.AppendLine("- `benchmark-results.json`");
        builder.AppendLine("- `benchmark-results.csv`");
        builder.AppendLine("- `benchmark-summary.md`");
        builder.AppendLine("- `compatibility.log` and `benchmark.log` should be captured by the workflow shell.");

        return builder.ToString();
    }

    private static string BuildBenchmarkCsv(BenchmarkRunResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("sample,mode,osDescription,osArchitecture,processArchitecture,cpuModel,logicalCoreCount,totalMemoryBytes,scenario,status,sampleIndex,elapsedMilliseconds,warmupIterations,measuredIterations,errorType,errorMessage");

        foreach (var scenario in result.Scenarios)
        {
            if (scenario.SamplesMilliseconds.Count == 0)
            {
                builder.AppendLine(string.Join(",",
                    EscapeCsv(result.SampleName),
                    EscapeCsv(result.Mode),
                    EscapeCsv(result.Platform.OsDescription),
                    EscapeCsv(result.Platform.OsArchitecture),
                    EscapeCsv(result.Platform.ProcessArchitecture),
                    EscapeCsv(result.Platform.Hardware.CpuModel ?? string.Empty),
                    result.Platform.Hardware.LogicalCoreCount.ToString(CultureInfo.InvariantCulture),
                    result.Platform.Hardware.TotalMemoryBytes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    EscapeCsv(scenario.Name),
                    EscapeCsv(scenario.Passed ? "PASS" : "FAIL"),
                    string.Empty,
                    string.Empty,
                    scenario.WarmupIterations.ToString(CultureInfo.InvariantCulture),
                    scenario.MeasuredIterations.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(scenario.Failure?.Type ?? string.Empty),
                    EscapeCsv(scenario.Failure?.Message ?? string.Empty)));
                continue;
            }

            for (var index = 0; index < scenario.SamplesMilliseconds.Count; index++)
            {
                builder.AppendLine(string.Join(",",
                    EscapeCsv(result.SampleName),
                    EscapeCsv(result.Mode),
                    EscapeCsv(result.Platform.OsDescription),
                    EscapeCsv(result.Platform.OsArchitecture),
                    EscapeCsv(result.Platform.ProcessArchitecture),
                    EscapeCsv(result.Platform.Hardware.CpuModel ?? string.Empty),
                    result.Platform.Hardware.LogicalCoreCount.ToString(CultureInfo.InvariantCulture),
                    result.Platform.Hardware.TotalMemoryBytes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    EscapeCsv(scenario.Name),
                    EscapeCsv(scenario.Passed ? "PASS" : "FAIL"),
                    index.ToString(CultureInfo.InvariantCulture),
                    scenario.SamplesMilliseconds[index].ToString("F6", CultureInfo.InvariantCulture),
                    scenario.WarmupIterations.ToString(CultureInfo.InvariantCulture),
                    scenario.MeasuredIterations.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(scenario.Failure?.Type ?? string.Empty),
                    EscapeCsv(scenario.Failure?.Message ?? string.Empty)));
            }
        }

        return builder.ToString();
    }

    private static string BuildSingleRunReadmeSummary(BenchmarkRunResult result, string outputDirectory, string label)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"_Updated at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss 'UTC'} via `{label}`._");
        builder.AppendLine();
        builder.AppendLine($"- Platform: `{BuildRunnerLabel(result.Platform)}`");
        builder.AppendLine($"- Hardware: `{BuildHardwareSummary(result.Platform.Hardware)}`");
        builder.AppendLine($"- Overall status: `{(result.AllPassed ? "PASS" : "FAIL")}`");
        builder.AppendLine($"- Output directory: `{outputDirectory}`");
        builder.AppendLine();
        builder.AppendLine("| Scenario | Mean (ms) | Median (ms) | Min (ms) | Max (ms) | Status |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | --- |");

        foreach (var scenario in result.Scenarios)
        {
            builder.AppendLine(
                $"| `{scenario.Name}` | {FormatNullableDouble(scenario.MeanMilliseconds)} | {FormatNullableDouble(scenario.MedianMilliseconds)} | {FormatNullableDouble(scenario.MinMilliseconds)} | {FormatNullableDouble(scenario.MaxMilliseconds)} | {(scenario.Passed ? "PASS" : "FAIL")} |");
        }

        builder.AppendLine();
        builder.AppendLine("Artifacts: `benchmark-results.json`, `benchmark-results.csv`, `benchmark-summary.md`.");
        return builder.ToString();
    }

    private static string BuildRunnerLabel(PlatformMetadata platform)
    {
        return $"{platform.OsDescription} / {platform.ProcessArchitecture} / {platform.FrameworkDescription}";
    }

    private static string FormatScenarioMedian(BenchmarkRunResult result, string scenarioName)
    {
        var scenario = result.Scenarios.FirstOrDefault(item => string.Equals(item.Name, scenarioName, StringComparison.OrdinalIgnoreCase));
        return scenario is null ? "n/a" : FormatNullableDouble(scenario.MedianMilliseconds);
    }

    private static string FormatNullableDouble(double? value)
    {
        return value.HasValue ? value.Value.ToString("F4", CultureInfo.InvariantCulture) : "n/a";
    }

    private static string EscapeCsv(string value)
    {
        var normalized = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{normalized}\"";
    }

    private static string EscapePipes(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string FormatBytes(long value)
    {
        string[] suffixes = ["B", "KiB", "MiB", "GiB", "TiB"];
        double size = value;
        var order = 0;
        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:F1} {suffixes[order]}";
    }
}
