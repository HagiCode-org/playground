using System.Text.Json.Nodes;
using OpenCodeSdk;
using OpenCodeSdk.Generated;

var settings = DemoSettings.Load();
OpenCodeSessionHandle? current = null;

Console.WriteLine("OpenCode C# SDK demo");
Console.WriteLine(settings.BaseUri is null
    ? "Mode: dedicated process per session"
    : $"Mode: attach to {settings.BaseUri}");
Console.WriteLine("Commands: /start, /prompt <text>, /status, /health, /parallel <count> <text>, /dispose, /exit");
Console.WriteLine();

while (true)
{
    Console.Write(current is null ? "opencode> " : $"opencode[{current.Session.Id}]> ");
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    if (string.Equals(line.Trim(), "/exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    try
    {
        if (string.Equals(line.Trim(), "/start", StringComparison.OrdinalIgnoreCase))
        {
            current ??= await StartSessionAsync(settings);
            PrintSession(current);
            continue;
        }

        if (string.Equals(line.Trim(), "/dispose", StringComparison.OrdinalIgnoreCase))
        {
            if (current is null)
            {
                Console.WriteLine("No active session.");
            }
            else
            {
                var disposedSessionId = current.Session.Id;
                await current.DisposeAsync();
                Console.WriteLine($"Disposed session {disposedSessionId}.");
                current = null;
            }

            continue;
        }

        if (string.Equals(line.Trim(), "/status", StringComparison.OrdinalIgnoreCase))
        {
            if (current is null)
            {
                Console.WriteLine("No active session.");
            }
            else
            {
                var status = await current.GetStatusAsync();
                Console.WriteLine($"Session={status.SessionId} title={status.SessionTitle ?? "(none)"} messages={status.MessageCount}");
                Console.WriteLine($"BaseUri={status.BaseUri} process={(status.ProcessId?.ToString() ?? "attach")} ownProcess={status.OwnsProcess}");
            }

            continue;
        }

        if (string.Equals(line.Trim(), "/health", StringComparison.OrdinalIgnoreCase))
        {
            var client = current?.Client ?? OpenCodeSessionRuntime.Connect(settings.ToClientOptions());
            var health = await client.Global.HealthAsync();
            Console.WriteLine($"healthy={health.Healthy} version={health.Version}");
            continue;
        }

        if (line.StartsWith("/parallel ", StringComparison.OrdinalIgnoreCase))
        {
            var rest = line[10..].Trim();
            var firstSpace = rest.IndexOf(' ');
            if (firstSpace <= 0 || !int.TryParse(rest[..firstSpace], out var count) || count <= 0)
            {
                Console.WriteLine("Usage: /parallel <count> <prompt>");
                continue;
            }

            var prompt = rest[(firstSpace + 1)..].Trim();
            await RunParallelAsync(settings, count, prompt);
            continue;
        }

        if (line.StartsWith("/prompt ", StringComparison.OrdinalIgnoreCase))
        {
            current ??= await StartSessionAsync(settings);
            await RunPromptAsync(current, line[8..].Trim());
            continue;
        }

        current ??= await StartSessionAsync(settings);
        await RunPromptAsync(current, line.Trim());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }

    Console.WriteLine();
}

if (current is not null)
{
    await current.DisposeAsync();
}

return;

static async Task<OpenCodeSessionHandle> StartSessionAsync(DemoSettings settings)
{
    var handle = await OpenCodeSessionRuntime.StartAsync(settings.ToSessionOptions());
    Console.WriteLine("[session] started");
    PrintSession(handle);
    return handle;
}

static async Task RunPromptAsync(OpenCodeSessionHandle handle, string prompt)
{
    Console.WriteLine($"[prompt] {prompt}");
    var response = await handle.PromptAsync(prompt);
    var text = response.GetTextContent();
    Console.WriteLine(string.IsNullOrWhiteSpace(text)
        ? "[assistant] (response contained no text parts)"
        : $"[assistant] {text}");
}

static async Task RunParallelAsync(DemoSettings settings, int count, string prompt)
{
    Console.WriteLine($"Running {count} isolated sessions in parallel...");
    var tasks = Enumerable.Range(1, count).Select(index => RunParallelWorkerAsync(settings, index, prompt)).ToArray();
    var results = await Task.WhenAll(tasks);
    foreach (var result in results)
    {
        Console.WriteLine($"[parallel {result.Index}] session={result.SessionId} process={result.ProcessId?.ToString() ?? "attach"}");
        Console.WriteLine($"[parallel {result.Index}] {result.Response}");
    }
}

static async Task<(int Index, string SessionId, int? ProcessId, string Response)> RunParallelWorkerAsync(
    DemoSettings settings,
    int index,
    string prompt)
{
    await using var handle = await OpenCodeSessionRuntime.StartAsync(settings.ToSessionOptions($"parallel-{index}"));
    var response = await handle.PromptAsync(prompt);
    return (index, handle.Session.Id, handle.ProcessId, response.GetTextContent());
}

static void PrintSession(OpenCodeSessionHandle handle)
{
    Console.WriteLine($"Session={handle.Session.Id} title={handle.Session.Title ?? "(none)"}");
    Console.WriteLine($"BaseUri={handle.Client.BaseUri} process={(handle.ProcessId?.ToString() ?? "attach")} ownProcess={handle.OwnsProcess}");
}

internal sealed class DemoSettings
{
    public Uri? BaseUri { get; init; }

    public string? Directory { get; init; }

    public string? Workspace { get; init; }

    public string? ExecutablePath { get; init; }

    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public string? LogLevel { get; init; }

    public JsonNode? Config { get; init; }

    public string? SessionTitle { get; init; }

    public static DemoSettings Load()
    {
        var baseUrl = Environment.GetEnvironmentVariable("OPENCODE_BASE_URL");
        var configJson = Environment.GetEnvironmentVariable("OPENCODE_CONFIG_JSON");
        var timeoutSeconds = Environment.GetEnvironmentVariable("OPENCODE_STARTUP_TIMEOUT_SECONDS");

        return new DemoSettings
        {
            BaseUri = string.IsNullOrWhiteSpace(baseUrl) ? null : new Uri(baseUrl, UriKind.Absolute),
            Directory = Environment.GetEnvironmentVariable("OPENCODE_DIRECTORY"),
            Workspace = Environment.GetEnvironmentVariable("OPENCODE_WORKSPACE"),
            ExecutablePath = Environment.GetEnvironmentVariable("OPENCODE_EXECUTABLE"),
            LogLevel = Environment.GetEnvironmentVariable("OPENCODE_LOG_LEVEL"),
            Config = string.IsNullOrWhiteSpace(configJson) ? null : JsonNode.Parse(configJson),
            SessionTitle = Environment.GetEnvironmentVariable("OPENCODE_SESSION_TITLE"),
            StartupTimeout = int.TryParse(timeoutSeconds, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.FromSeconds(15),
        };
    }

    public OpenCodeClientOptions ToClientOptions()
    {
        return new OpenCodeClientOptions
        {
            BaseUri = BaseUri,
            Directory = Directory,
            Workspace = Workspace,
        };
    }

    public OpenCodeSessionOptions ToSessionOptions(string? titleOverride = null)
    {
        return new OpenCodeSessionOptions
        {
            BaseUri = BaseUri,
            Directory = Directory,
            Workspace = Workspace,
            SessionTitle = titleOverride ?? SessionTitle,
            Process = new OpenCodeProcessOptions
            {
                ExecutablePath = ExecutablePath,
                StartupTimeout = StartupTimeout,
                LogLevel = LogLevel,
                Config = Config,
            },
        };
    }
}
