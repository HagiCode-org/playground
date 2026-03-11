using CodeBuddySdk.Artifacts;
using CodeBuddySdk.Configuration;
using CodeBuddySdk.Runtime;
using CodeBuddySdk.Scenarios;

var configPath = GetSingleValue(args, "--config");
var listOnly = HasFlag(args, "--list");
var modeValue = GetSingleValue(args, "--mode");
var scenarioNames = GetMultiValue(args, "--scenario");

var options = ConfigurationLoader.Load(configPath);
if (!string.IsNullOrWhiteSpace(modeValue) && Enum.TryParse<ExecutionMode>(modeValue, ignoreCase: true, out var requestedMode))
{
    options.Selection.Mode = requestedMode;
}

if (scenarioNames.Count > 0)
{
    options.Selection.ScenarioNames = scenarioNames;
}

var validationErrors = options.Validate();
if (validationErrors.Count > 0)
{
    Console.Error.WriteLine("CodeBuddy playground startup failed due to invalid configuration:");
    foreach (var error in validationErrors)
    {
        Console.Error.WriteLine($"- {error}");
    }

    Environment.ExitCode = 1;
    return;
}

var scenarios = ScenarioCatalog.CreateDefault();
if (listOnly)
{
    Console.WriteLine("Available scenarios:");
    foreach (var scenario in scenarios)
    {
        Console.WriteLine($"- {scenario.Name} [{scenario.Mode}] {scenario.Description}");
    }

    return;
}

Directory.CreateDirectory(options.ResolveRunStorePath());
var runner = new ScenarioRunner(
    scenarios,
    new ScenarioContext
    {
        Options = options,
        Client = new CodeBuddyProcessClient(),
        ArtifactWriter = new RunArtifactWriter(),
    });

Console.WriteLine("CodeBuddy standalone runner (.NET)");
Console.WriteLine($"Mode: {options.Selection.Mode}");
Console.WriteLine($"Working Directory: {options.WorkingDirectory}");
Console.WriteLine($"Run Store: {options.ResolveRunStorePath()}");
Console.WriteLine();

var outcomes = await runner.RunAsync(options.Selection, CancellationToken.None);
foreach (var outcome in outcomes)
{
    var artifactPath = outcome.ArtifactRecord?.RunDirectory ?? "n/a";
    Console.WriteLine($"[{outcome.Status}] {outcome.Name} ({outcome.Mode})");
    Console.WriteLine($"  {outcome.Summary}");
    Console.WriteLine($"  artifacts: {artifactPath}");
    if (outcome.ExecutionResult is not null)
    {
        Console.WriteLine($"  duration: {outcome.ExecutionResult.Duration.TotalMilliseconds:F0}ms");
        if (!string.IsNullOrWhiteSpace(outcome.ExecutionResult.FinalContent))
        {
            Console.WriteLine($"  output: {TrimForConsole(outcome.ExecutionResult.FinalContent)}");
        }
        else if (!string.IsNullOrWhiteSpace(outcome.ExecutionResult.FailureMessage))
        {
            Console.WriteLine($"  error: {outcome.ExecutionResult.FailureMessage}");
        }
    }

    Console.WriteLine();
}

if (outcomes.Any(static outcome => outcome.Status == ScenarioStatus.Failed))
{
    Environment.ExitCode = 1;
}

static string? GetSingleValue(string[] args, string key)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static List<string> GetMultiValue(string[] args, string key)
{
    var values = new List<string>();
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
        {
            values.Add(args[i + 1]);
        }
    }

    return values;
}

static bool HasFlag(string[] args, string key)
{
    return args.Any(arg => string.Equals(arg, key, StringComparison.OrdinalIgnoreCase));
}

static string TrimForConsole(string value)
{
    const int maxLength = 120;
    var flattened = value.ReplaceLineEndings(" ").Trim();
    return flattened.Length <= maxLength ? flattened : flattened[..maxLength] + "...";
}
