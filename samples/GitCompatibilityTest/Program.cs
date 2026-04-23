using LibGit2Sharp;

namespace GitCompatibilityTest;

internal static class Program
{
    private static readonly StatusOptions SharedStatusOptions = new()
    {
        IncludeUntracked = true,
        RecurseUntrackedDirs = true,
        DetectRenamesInIndex = true,
        DetectRenamesInWorkDir = true
    };

    public static async Task<int> Main(string[] args)
    {
        var options = CliOptions.Parse(args);
        if (options.ShowHelp)
        {
            PrintHelp();
            return (int)ValidationExitCode.Success;
        }

        if (!options.IsValid(out var validationError))
        {
            Console.Error.WriteLine(validationError);
            Console.Error.WriteLine();
            PrintHelp();
            return (int)ValidationExitCode.InvalidArguments;
        }

        var outputDirectory = ResolveOutputDirectory(options.OutputDirectory, options.Mode);
        Directory.CreateDirectory(outputDirectory);

        var startedAtUtc = DateTimeOffset.UtcNow;
        var platform = PlatformMetadata.Capture();

        PrintHeader(options, outputDirectory, platform);

        return options.Mode switch
        {
            ValidationMode.Compatibility => await RunCompatibilityAsync(options, outputDirectory, startedAtUtc, platform),
            ValidationMode.Benchmark => await RunBenchmarkAsync(options, outputDirectory, startedAtUtc, platform),
            ValidationMode.Summarize => await RunSummarizeAsync(options, outputDirectory),
            _ => (int)ValidationExitCode.InvalidArguments
        };
    }

    private static async Task<int> RunCompatibilityAsync(
        CliOptions options,
        string outputDirectory,
        DateTimeOffset startedAtUtc,
        PlatformMetadata platform)
    {
        var repositoryPath = ResolveRepositoryPath(options.RepositoryPath);
        var sqliteFixture = SqliteFixtureFactory.Create(Path.Combine(outputDirectory, "sqlite-fixture", "compatibility.sqlite"));

        Console.WriteLine($"Repository path: {repositoryPath}");
        Console.WriteLine($"SQLite fixture: {sqliteFixture.DatabasePath}");
        Console.WriteLine();

        var checks = new List<ValidationCheckResult>
        {
            RunCheck("library-load", "Library load", () =>
            {
                var version = GlobalSettings.Version;
                return new Dictionary<string, string>
                {
                    ["libgit2sharpVersion"] = version.ToString()
                };
            }),
            RunCheck("repository-open", "Repository initialization", () =>
            {
                EnsureValidRepository(repositoryPath);
                using var repository = new Repository(repositoryPath);
                return new Dictionary<string, string>
                {
                    ["isBare"] = repository.Info.IsBare.ToString(),
                    ["workingDirectory"] = repository.Info.WorkingDirectory ?? repositoryPath
                };
            }),
            RunCheck("status-scan", "Repository status retrieval", () =>
            {
                using var repository = OpenRepository(repositoryPath);
                var status = repository.RetrieveStatus(SharedStatusOptions);
                return new Dictionary<string, string>
                {
                    ["statusEntryCount"] = status.Count().ToString(),
                    ["modifiedCount"] = status.Modified.Count().ToString(),
                    ["untrackedCount"] = status.Untracked.Count().ToString()
                };
            }),
            RunCheck("branch-lookup", "Branch lookup", () =>
            {
                using var repository = OpenRepository(repositoryPath);
                return new Dictionary<string, string>
                {
                    ["headDetached"] = repository.Info.IsHeadDetached.ToString(),
                    ["branchName"] = repository.Head.FriendlyName ?? "HEAD"
                };
            }),
            RunCheck("head-commit-lookup", "HEAD commit lookup", () =>
            {
                using var repository = OpenRepository(repositoryPath);
                var tip = repository.Head.Tip ?? throw new InvalidOperationException("HEAD does not have a tip commit.");
                return new Dictionary<string, string>
                {
                    ["commitSha"] = tip.Id.Sha,
                    ["messageShort"] = tip.MessageShort
                };
            }),
            RunCheck("sqlite-ef-query", "SQLite EF Core smoke query", () =>
            {
                return SqliteValidationService.RunEfSmokeCheck(sqliteFixture.DatabasePath);
            }),
            RunCheck("sqlite-linq2db-query", "SQLite linq2db smoke query", () =>
            {
                return SqliteValidationService.RunLinqToDbSmokeCheck(sqliteFixture.DatabasePath);
            })
        };

        var result = new CompatibilityRunResult
        {
            SampleName = "GitCompatibilityTest",
            Mode = ValidationMode.Compatibility.ToString().ToLowerInvariant(),
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            RepositoryPath = repositoryPath,
            Platform = platform,
            Checks = checks
        };

        ResultWriter.WriteCompatibilityArtifacts(outputDirectory, result);
        Console.WriteLine(ResultWriter.BuildCompatibilityConsoleReport(result));

        return result.AllPassed
            ? (int)ValidationExitCode.Success
            : (int)ValidationExitCode.CompatibilityFailed;
    }

    private static async Task<int> RunBenchmarkAsync(
        CliOptions options,
        string outputDirectory,
        DateTimeOffset startedAtUtc,
        PlatformMetadata platform)
    {
        var fixture = FixtureRepositoryFactory.Create(Path.Combine(outputDirectory, "fixture-repo"));
        var sqliteFixture = SqliteFixtureFactory.Create(Path.Combine(outputDirectory, "sqlite-fixture", "benchmark.sqlite"));

        Console.WriteLine($"Fixture repository: {fixture.RepositoryPath}");
        Console.WriteLine($"SQLite fixture: {sqliteFixture.DatabasePath}");
        Console.WriteLine($"Warmup iterations: {options.WarmupIterations}");
        Console.WriteLine($"Measured iterations: {options.MeasuredIterations}");
        Console.WriteLine();

        using var statusRepository = new Repository(fixture.RepositoryPath);
        using var branchRepository = new Repository(fixture.RepositoryPath);
        using var headRepository = new Repository(fixture.RepositoryPath);

        var scenarios = new[]
        {
            new BenchmarkScenarioDefinition(
                "repository-open",
                "Open a Repository instance using the compatibility-check path.",
                () =>
                {
                    using var repository = new Repository(fixture.RepositoryPath);
                    _ = repository.Info.WorkingDirectory;
                    return ValueTask.CompletedTask;
                }),
            new BenchmarkScenarioDefinition(
                "status-scan",
                "Run Repository.RetrieveStatus with the same StatusOptions shape used by hagicode-core.",
                () =>
                {
                    var status = statusRepository.RetrieveStatus(SharedStatusOptions);
                    _ = status.Count();
                    return ValueTask.CompletedTask;
                }),
            new BenchmarkScenarioDefinition(
                "branch-lookup",
                "Read Repository.Head.FriendlyName on the fixture repository.",
                () =>
                {
                    _ = branchRepository.Head.FriendlyName;
                    return ValueTask.CompletedTask;
                }),
            new BenchmarkScenarioDefinition(
                "head-commit-lookup",
                "Read Repository.Head.Tip metadata on the fixture repository.",
                () =>
                {
                    _ = headRepository.Head.Tip?.Id.Sha;
                    return ValueTask.CompletedTask;
                }),
            new BenchmarkScenarioDefinition(
                "sqlite-ef-query",
                "Open a SQLite EF Core DbContext and read the latest qualifying row.",
                () =>
                {
                    SqliteValidationService.RunEfBenchmarkQuery(sqliteFixture.DatabasePath);
                    return ValueTask.CompletedTask;
                }),
            new BenchmarkScenarioDefinition(
                "sqlite-linq2db-query",
                "Open a SQLite linq2db DataConnection and read the latest qualifying row.",
                () =>
                {
                    SqliteValidationService.RunLinqToDbBenchmarkQuery(sqliteFixture.DatabasePath);
                    return ValueTask.CompletedTask;
                })
        };

        var runner = new BenchmarkScenarioRunner(options.WarmupIterations, options.MeasuredIterations);
        var scenarioResults = await runner.RunAsync(scenarios);

        var benchmarkResult = new BenchmarkRunResult
        {
            SampleName = "GitCompatibilityTest",
            Mode = ValidationMode.Benchmark.ToString().ToLowerInvariant(),
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Platform = platform,
            Fixture = fixture,
            SqliteFixture = sqliteFixture,
            WarmupIterations = options.WarmupIterations,
            MeasuredIterations = options.MeasuredIterations,
            StatusOptions = ResultWriter.DescribeStatusOptions(SharedStatusOptions),
            Scenarios = scenarioResults
        };

        benchmarkResult.ReadmeRefresh = ResultWriter.WriteBenchmarkArtifacts(
            outputDirectory,
            benchmarkResult,
            options.RefreshReadme,
            options.ReadmePath);

        Console.WriteLine(ResultWriter.BuildBenchmarkConsoleReport(benchmarkResult, outputDirectory));

        return benchmarkResult.AllPassed
            ? (int)ValidationExitCode.Success
            : (int)ValidationExitCode.BenchmarkFailed;
    }

    private static async Task<int> RunSummarizeAsync(CliOptions options, string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(options.ResultsRoot))
        {
            Console.Error.WriteLine("`--results-root` is required when `--mode summarize` is used.");
            return (int)ValidationExitCode.InvalidArguments;
        }

        var resultsRoot = Path.GetFullPath(options.ResultsRoot);
        var benchmarkResults = ResultWriter.LoadBenchmarkResults(resultsRoot);
        if (benchmarkResults.Count == 0)
        {
            Console.Error.WriteLine($"No benchmark result files were found under `{resultsRoot}`.");
            return (int)ValidationExitCode.InvalidArguments;
        }

        var summaryMarkdown = ResultWriter.BuildAggregateMarkdownSummary(benchmarkResults, resultsRoot);
        var summaryPath = Path.Combine(outputDirectory, "latest-manual-run-summary.md");
        await File.WriteAllTextAsync(summaryPath, summaryMarkdown);

        ReadmeRefreshInfo? refreshInfo = null;
        if (!string.IsNullOrWhiteSpace(options.ReadmePath))
        {
            refreshInfo = ResultWriter.UpdateReadmeSummary(options.ReadmePath, summaryMarkdown);
            var refreshedReadmePath = Path.Combine(outputDirectory, "README.md");
            File.Copy(Path.GetFullPath(options.ReadmePath), refreshedReadmePath, overwrite: true);
        }

        Console.WriteLine(ResultWriter.BuildAggregateConsoleReport(benchmarkResults, summaryPath, refreshInfo));
        return (int)ValidationExitCode.Success;
    }

    private static ValidationCheckResult RunCheck(
        string id,
        string displayName,
        Func<Dictionary<string, string>> action)
    {
        try
        {
            return new ValidationCheckResult
            {
                Id = id,
                DisplayName = displayName,
                Passed = true,
                Details = action()
            };
        }
        catch (Exception ex)
        {
            return new ValidationCheckResult
            {
                Id = id,
                DisplayName = displayName,
                Passed = false,
                Failure = FailureDetails.FromException(ex)
            };
        }
    }

    private static Repository OpenRepository(string repositoryPath)
    {
        EnsureValidRepository(repositoryPath);
        return new Repository(repositoryPath);
    }

    private static void EnsureValidRepository(string repositoryPath)
    {
        if (!Repository.IsValid(repositoryPath))
        {
            throw new DirectoryNotFoundException($"`{repositoryPath}` is not a valid Git repository.");
        }
    }

    private static string ResolveRepositoryPath(string? repositoryPath)
    {
        if (!string.IsNullOrWhiteSpace(repositoryPath))
        {
            return Path.GetFullPath(repositoryPath);
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var gitMetadata = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitMetadata) || File.Exists(gitMetadata))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string ResolveOutputDirectory(string? requestedPath, ValidationMode mode)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            return Path.GetFullPath(requestedPath);
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "artifacts", $"{mode.ToString().ToLowerInvariant()}-{timestamp}"));
    }

    private static void PrintHeader(CliOptions options, string outputDirectory, PlatformMetadata platform)
    {
        Console.WriteLine("================================================");
        Console.WriteLine("GitCompatibilityTest");
        Console.WriteLine("================================================");
        Console.WriteLine($"Mode: {options.Mode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Output directory: {outputDirectory}");
        Console.WriteLine($"OS: {platform.OsDescription}");
        Console.WriteLine($"OS architecture: {platform.OsArchitecture}");
        Console.WriteLine($"Process architecture: {platform.ProcessArchitecture}");
        Console.WriteLine($".NET runtime: {platform.FrameworkDescription} / {platform.DotnetVersion}");
        Console.WriteLine($"Hardware: {ResultWriter.BuildHardwareSummary(platform.Hardware)}");
        if (!string.IsNullOrWhiteSpace(platform.GithubRunnerOs))
        {
            Console.WriteLine($"GitHub runner: {platform.GithubRunnerOs} / {platform.GithubRunnerArch ?? "unknown"}");
        }

        Console.WriteLine();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("GitCompatibilityTest CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -- --mode compatibility [--repository <path>] [--output <path>]");
        Console.WriteLine("  dotnet run -- --mode benchmark [--output <path>] [--warmup <count>] [--iterations <count>] [--refresh-readme --readme <path>]");
        Console.WriteLine("  dotnet run -- --mode summarize --results-root <path> [--output <path>] [--readme <path>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --mode <compatibility|benchmark|summarize>   Execution mode.");
        Console.WriteLine("  --repository <path>                          Repository path for compatibility checks.");
        Console.WriteLine("  --output <path>                              Directory where artifacts are written.");
        Console.WriteLine("  --warmup <count>                             Warmup iteration count for benchmark mode.");
        Console.WriteLine("  --iterations <count>                         Measured iteration count for benchmark mode.");
        Console.WriteLine("  --results-root <path>                        Root directory containing benchmark-results.json files.");
        Console.WriteLine("  --refresh-readme                             Update the root README summary section after benchmark mode.");
        Console.WriteLine("  --readme <path>                              README file to update when refresh is enabled.");
        Console.WriteLine("  --help                                       Print this message.");
    }

    private sealed record CliOptions(
        ValidationMode Mode,
        string? RepositoryPath,
        string? OutputDirectory,
        int WarmupIterations,
        int MeasuredIterations,
        bool RefreshReadme,
        string? ReadmePath,
        string? ResultsRoot,
        bool ShowHelp)
    {
        public static CliOptions Parse(IReadOnlyList<string> args)
        {
            var mode = ValidationMode.Compatibility;
            string? repositoryPath = null;
            string? outputDirectory = null;
            var warmupIterations = 2;
            var measuredIterations = 8;
            var refreshReadme = false;
            string? readmePath = null;
            string? resultsRoot = null;
            var showHelp = false;

            for (var i = 0; i < args.Count; i++)
            {
                var current = args[i];
                switch (current)
                {
                    case "--mode":
                        mode = ParseMode(ReadValue(args, ref i, current));
                        break;
                    case "--repository":
                        repositoryPath = ReadValue(args, ref i, current);
                        break;
                    case "--output":
                        outputDirectory = ReadValue(args, ref i, current);
                        break;
                    case "--warmup":
                        warmupIterations = ParsePositiveInt(ReadValue(args, ref i, current), current);
                        break;
                    case "--iterations":
                        measuredIterations = ParsePositiveInt(ReadValue(args, ref i, current), current);
                        break;
                    case "--refresh-readme":
                        refreshReadme = true;
                        break;
                    case "--readme":
                        readmePath = ReadValue(args, ref i, current);
                        break;
                    case "--results-root":
                        resultsRoot = ReadValue(args, ref i, current);
                        break;
                    case "--help":
                    case "-h":
                        showHelp = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {current}");
                }
            }

            return new CliOptions(
                mode,
                repositoryPath,
                outputDirectory,
                warmupIterations,
                measuredIterations,
                refreshReadme,
                readmePath,
                resultsRoot,
                showHelp);
        }

        public bool IsValid(out string validationError)
        {
            if (WarmupIterations < 0)
            {
                validationError = "`--warmup` must be zero or greater.";
                return false;
            }

            if (MeasuredIterations <= 0)
            {
                validationError = "`--iterations` must be greater than zero.";
                return false;
            }

            if (Mode == ValidationMode.Benchmark && RefreshReadme && string.IsNullOrWhiteSpace(ReadmePath))
            {
                validationError = "`--readme` is required when `--refresh-readme` is set.";
                return false;
            }

            if (Mode == ValidationMode.Summarize && string.IsNullOrWhiteSpace(ResultsRoot))
            {
                validationError = "`--results-root` is required when `--mode summarize` is used.";
                return false;
            }

            validationError = string.Empty;
            return true;
        }

        private static string ReadValue(IReadOnlyList<string> args, ref int index, string option)
        {
            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"Missing value for `{option}`.");
            }

            index++;
            return args[index];
        }

        private static ValidationMode ParseMode(string value)
        {
            return value.ToLowerInvariant() switch
            {
                "compatibility" => ValidationMode.Compatibility,
                "benchmark" => ValidationMode.Benchmark,
                "summarize" => ValidationMode.Summarize,
                _ => throw new ArgumentException($"Unsupported mode `{value}`.")
            };
        }

        private static int ParsePositiveInt(string value, string option)
        {
            if (!int.TryParse(value, out var parsed) || parsed < 0)
            {
                throw new ArgumentException($"`{option}` expects a non-negative integer.");
            }

            return parsed;
        }
    }
}
