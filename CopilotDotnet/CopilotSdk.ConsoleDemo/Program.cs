using CopilotSdk.Auth;
using CopilotSdk.Client;
using CopilotSdk.Configuration;
using CopilotSdk.Processing;
using CopilotSdk.Runner;
using CopilotSdk.Storage;

var configPath = GetArgValue(args, "--config");
var settings = ConfigurationLoader.Load(configPath);
var validationErrors = settings.Validate();
if (validationErrors.Count > 0)
{
    Console.Error.WriteLine("Copilot playground startup failed due to invalid configuration:");
    foreach (var error in validationErrors)
    {
        Console.Error.WriteLine($"- {error}");
    }

    Environment.ExitCode = 1;
    return;
}

Directory.CreateDirectory(settings.RunStorePath);

var provider = new EnvironmentCredentialProvider(settings);
var authManager = new CopilotAuthManager(provider);
var processor = new CopilotResponseProcessor();
var gateway = new GitHubCopilotSessionGateway();
var adapter = new CopilotClientAdapter(settings, authManager, gateway, processor);
var runStore = new CopilotRunStore(settings.RunStorePath, settings.UseSqlite);
var runner = new CopilotPlaygroundRunner(adapter, runStore);

var promptArgs = args.Where(x => !x.StartsWith("--", StringComparison.OrdinalIgnoreCase)).ToArray();
if (promptArgs.Length > 0)
{
    await ExecutePromptAsync(string.Join(' ', promptArgs));
    return;
}

Console.WriteLine("Copilot SDK Playground (.NET)");
Console.WriteLine("Type a prompt and press Enter. Use /exit to quit.");
Console.WriteLine($"Model: {settings.Model}");
Console.WriteLine($"Run Store: {settings.RunStorePath}");
Console.WriteLine();

while (true)
{
    Console.Write("> ");
    var prompt = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(prompt))
    {
        continue;
    }

    if (string.Equals(prompt.Trim(), "/exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    await ExecutePromptAsync(prompt);
}

return;

async Task ExecutePromptAsync(string prompt)
{
    var record = await runner.RunAsync(settings.Model, prompt, CancellationToken.None);

    Console.WriteLine($"[{record.CorrelationId}] success={record.Success}, duration={record.DurationMs}ms");

    if (record.Success)
    {
        Console.WriteLine(record.Content);
    }
    else
    {
        Console.WriteLine($"ErrorCategory: {record.ErrorCategory}");
        Console.WriteLine($"Error: {record.ErrorMessage}");
        if (record.AuthFailureCategory is not null)
        {
            Console.WriteLine($"AuthFailure: {record.AuthFailureCategory} - {record.AuthFailureMessage}");
        }
    }

    Console.WriteLine();
}

static string? GetArgValue(string[] args, string key)
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
