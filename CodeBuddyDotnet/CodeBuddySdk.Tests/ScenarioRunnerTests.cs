using CodeBuddySdk.Artifacts;
using CodeBuddySdk.Configuration;
using CodeBuddySdk.Runtime;
using CodeBuddySdk.Scenarios;
using CodeBuddySdk.Scenarios.Definitions;

namespace CodeBuddySdk.Tests;

public sealed class ScenarioRunnerTests
{
    [Fact]
    public async Task RunAsync_FixtureMode_CompletesPassingFixtureScenarios()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var runner = CreateRunner(tempDirectory, new CodeBuddyProcessClient(new FakeProcessRunner(_ => throw new InvalidOperationException("Fixture mode should not call live runner."))));
            var outcomes = await runner.RunAsync(new ScenarioSelectionOptions { Mode = ExecutionMode.Fixture }, CancellationToken.None);

            Assert.Equal(3, outcomes.Count);
            Assert.All(outcomes, outcome => Assert.Equal(ScenarioStatus.Passed, outcome.Status));
            Assert.All(outcomes, outcome => Assert.NotNull(outcome.ArtifactRecord));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_LiveMode_SkipsWhenOptInDisabled()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var runner = CreateRunner(tempDirectory, new CodeBuddyProcessClient(new FakeProcessRunner(_ => throw new InvalidOperationException("Should be skipped."))), enableLiveScenarios: false);
            var outcomes = await runner.RunAsync(new ScenarioSelectionOptions { Mode = ExecutionMode.Live }, CancellationToken.None);

            Assert.Equal(2, outcomes.Count);
            Assert.All(outcomes, outcome => Assert.Equal(ScenarioStatus.Skipped, outcome.Status));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_LiveMode_UsesRunnerAndProducesPassOutcomes()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fakeRunner = new FakeProcessRunner(request =>
            {
                if (request.Timeout <= TimeSpan.FromMilliseconds(1))
                {
                    return new RawProcessResult
                    {
                        TimedOut = true,
                        Duration = TimeSpan.FromMilliseconds(1),
                        CommandDescription = "fake timeout",
                    };
                }

                return new RawProcessResult
                {
                    ExitCode = 0,
                    Duration = TimeSpan.FromMilliseconds(25),
                    StdOut = "READY CODEBUDDY\nworkspace ok\n",
                    Events =
                    [
                        new RawProcessEvent(DateTimeOffset.UtcNow, "stdout", "READY CODEBUDDY"),
                        new RawProcessEvent(DateTimeOffset.UtcNow.AddMilliseconds(10), "stdout", "workspace ok"),
                    ],
                    CommandDescription = "fake live command",
                };
            });
            var runner = CreateRunner(tempDirectory, new CodeBuddyProcessClient(fakeRunner), enableLiveScenarios: true);
            var outcomes = await runner.RunAsync(new ScenarioSelectionOptions { Mode = ExecutionMode.Live }, CancellationToken.None);

            Assert.Equal(2, outcomes.Count);
            Assert.All(outcomes, outcome => Assert.Equal(ScenarioStatus.Passed, outcome.Status));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static ScenarioRunner CreateRunner(string tempDirectory, CodeBuddyProcessClient client, bool enableLiveScenarios = false)
    {
        var options = new CodeBuddyRunOptions
        {
            WorkingDirectory = tempDirectory,
            RunStorePath = Path.Combine(tempDirectory, "runs"),
            EnableLiveScenarios = enableLiveScenarios,
        };

        return new ScenarioRunner(
            ScenarioCatalog.CreateDefault(),
            new ScenarioContext
            {
                Options = options,
                Client = client,
                ArtifactWriter = new RunArtifactWriter(),
            });
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "codebuddy-dotnet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        private readonly Func<ProcessRequest, RawProcessResult> _handler;

        public FakeProcessRunner(Func<ProcessRequest, RawProcessResult> handler)
        {
            _handler = handler;
        }

        public Task<RawProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
