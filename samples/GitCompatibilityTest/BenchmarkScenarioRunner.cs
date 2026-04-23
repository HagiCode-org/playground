using System.Diagnostics;

namespace GitCompatibilityTest;

internal sealed record BenchmarkScenarioDefinition(
    string Name,
    string Description,
    Func<ValueTask> Action);

internal sealed class BenchmarkScenarioRunner
{
    private readonly int _warmupIterations;
    private readonly int _measuredIterations;

    public BenchmarkScenarioRunner(int warmupIterations, int measuredIterations)
    {
        _warmupIterations = warmupIterations;
        _measuredIterations = measuredIterations;
    }

    public async Task<List<BenchmarkScenarioResult>> RunAsync(IEnumerable<BenchmarkScenarioDefinition> scenarios)
    {
        var results = new List<BenchmarkScenarioResult>();

        foreach (var scenario in scenarios)
        {
            Console.WriteLine($"Running benchmark scenario: {scenario.Name}");

            var samples = new List<double>();

            try
            {
                for (var iteration = 0; iteration < _warmupIterations; iteration++)
                {
                    await scenario.Action();
                }

                for (var iteration = 0; iteration < _measuredIterations; iteration++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    await scenario.Action();
                    stopwatch.Stop();
                    samples.Add(stopwatch.Elapsed.TotalMilliseconds);
                }

                results.Add(new BenchmarkScenarioResult
                {
                    Name = scenario.Name,
                    Description = scenario.Description,
                    WarmupIterations = _warmupIterations,
                    MeasuredIterations = _measuredIterations,
                    Passed = true,
                    SamplesMilliseconds = samples
                });
            }
            catch (Exception ex)
            {
                results.Add(new BenchmarkScenarioResult
                {
                    Name = scenario.Name,
                    Description = scenario.Description,
                    WarmupIterations = _warmupIterations,
                    MeasuredIterations = _measuredIterations,
                    Passed = false,
                    SamplesMilliseconds = samples,
                    Failure = FailureDetails.FromException(ex)
                });
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        return results;
    }
}
