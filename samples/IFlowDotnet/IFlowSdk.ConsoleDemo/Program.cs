using IFlowSdk.Client;
using IFlowSdk.Models;

var settings = DemoSettings.Load();
await using var client = settings.EnableRawCapture ? new RawDataClient(settings.ToOptions()) : new IFlowClient(settings.ToOptions());

Console.WriteLine("iFlow C# SDK Demo");
Console.WriteLine($"Mode: {(string.IsNullOrWhiteSpace(settings.Url) ? "auto-start CLI" : $"attach {settings.Url}")}");
Console.WriteLine("Commands: /prompt <text>, /interrupt, /approve <requestId> <optionId>, /reject <requestId>, /raw, /exit");
Console.WriteLine();

await client.ConnectAsync();
Console.WriteLine($"Connected: session={client.SessionId} url={client.ConnectionUrl} process={(client.ProcessId?.ToString() ?? "external")}");

var readTask = Task.Run(async () =>
{
    await foreach (var message in client.ReceiveMessagesAsync())
    {
        switch (message)
        {
            case AssistantMessage assistant when !string.IsNullOrWhiteSpace(assistant.Chunk.Text):
                Console.WriteLine($"assistant: {assistant.Chunk.Text}");
                break;
            case AssistantMessage assistant when !string.IsNullOrWhiteSpace(assistant.Chunk.Thought):
                Console.WriteLine($"thought: {assistant.Chunk.Thought}");
                break;
            case ToolCallMessage toolCall:
                Console.WriteLine($"tool-call: id={toolCall.Id} label={toolCall.Label} status={toolCall.Status} tool={toolCall.ToolName}");
                break;
            case ToolResultMessage toolResult:
                Console.WriteLine($"tool-update: id={toolResult.Id} status={toolResult.Status} tool={toolResult.ToolName}");
                if (!string.IsNullOrWhiteSpace(toolResult.Content?.Markdown))
                {
                    Console.WriteLine(toolResult.Content.Markdown);
                }
                break;
            case ToolConfirmationRequestMessage confirmation:
                Console.WriteLine($"tool-call pending: requestId={confirmation.RequestId} title={confirmation.ToolCall.Title}");
                Console.WriteLine("options: " + string.Join(", ", confirmation.Options.Select(static option => $"{option.Id}={option.Name}")));
                break;
            case PlanMessage plan:
                Console.WriteLine("plan:");
                foreach (var entry in plan.Entries)
                {
                    Console.WriteLine($"- [{entry.Status}] {entry.Content} ({entry.Priority})");
                }
                break;
            case TaskFinishMessage finish:
                Console.WriteLine($"task-finish: {finish.StopReason?.ToString() ?? "completed"}");
                break;
            case ErrorMessage error:
                Console.WriteLine($"error: code={error.Code} message={error.MessageText}");
                if (!string.IsNullOrWhiteSpace(error.Details))
                {
                    Console.WriteLine($"details: {error.Details}");
                }
                break;
        }
    }
});

Task? rawTask = null;
if (client is RawDataClient rawClient)
{
    rawTask = Task.Run(async () =>
    {
        await foreach (var raw in rawClient.ReceiveRawMessagesAsync())
        {
            if (!settings.PrintRawMessages)
            {
                continue;
            }

            Console.WriteLine($"raw[{raw.MessageType}]: {raw.RawData}");
        }
    });
}

while (true)
{
    Console.Write("iflow> ");
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
        if (line.StartsWith("/prompt ", StringComparison.OrdinalIgnoreCase))
        {
            await client.SendMessageAsync(line[8..].Trim());
            continue;
        }

        if (string.Equals(line.Trim(), "/interrupt", StringComparison.OrdinalIgnoreCase))
        {
            await client.InterruptAsync();
            Console.WriteLine("interrupt sent");
            continue;
        }

        if (line.StartsWith("/approve ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !int.TryParse(parts[1], out var requestId))
            {
                Console.WriteLine("Usage: /approve <requestId> <optionId>");
                continue;
            }

            await client.RespondToToolConfirmationAsync(requestId, parts[2]);
            Console.WriteLine("approval sent");
            continue;
        }

        if (line.StartsWith("/reject ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var requestId))
            {
                Console.WriteLine("Usage: /reject <requestId>");
                continue;
            }

            await client.CancelToolConfirmationAsync(requestId);
            Console.WriteLine("rejection sent");
            continue;
        }

        if (string.Equals(line.Trim(), "/raw", StringComparison.OrdinalIgnoreCase))
        {
            if (!settings.EnableRawCapture)
            {
                Console.WriteLine("Restart with IFLOW_DEMO_CAPTURE_RAW=true to enable raw message streaming.");
            }
            else
            {
                settings.PrintRawMessages = !settings.PrintRawMessages;
                Console.WriteLine($"Raw message printing {(settings.PrintRawMessages ? "enabled" : "disabled")}");
            }

            continue;
        }

        await client.SendMessageAsync(line.Trim());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"diagnostic: {ex.Message}");
        if (ex.InnerException is not null)
        {
            Console.WriteLine($"inner: {ex.InnerException.Message}");
        }
    }
}

await client.DisposeAsync();
if (rawTask is not null)
{
    await rawTask;
}

await readTask;

internal sealed class DemoSettings
{
    public string? Url { get; init; }

    public string Cwd { get; init; } = Directory.GetCurrentDirectory();

    public string? ExecutablePath { get; init; }

    public int ProcessStartPort { get; init; } = 8090;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public string? AuthMethodId { get; init; }

    public bool EnableRawCapture { get; init; }

    public bool PrintRawMessages { get; set; }

    public static DemoSettings Load()
    {
        var timeoutSeconds = Environment.GetEnvironmentVariable("IFLOW_TIMEOUT_SECONDS");
        return new DemoSettings
        {
            Url = Environment.GetEnvironmentVariable("IFLOW_URL"),
            Cwd = Environment.GetEnvironmentVariable("IFLOW_CWD") ?? Directory.GetCurrentDirectory(),
            ExecutablePath = Environment.GetEnvironmentVariable("IFLOW_EXECUTABLE"),
            AuthMethodId = Environment.GetEnvironmentVariable("IFLOW_AUTH_METHOD_ID"),
            EnableRawCapture = string.Equals(Environment.GetEnvironmentVariable("IFLOW_DEMO_CAPTURE_RAW"), "true", StringComparison.OrdinalIgnoreCase),
            PrintRawMessages = string.Equals(Environment.GetEnvironmentVariable("IFLOW_DEMO_PRINT_RAW"), "true", StringComparison.OrdinalIgnoreCase),
            ProcessStartPort = int.TryParse(Environment.GetEnvironmentVariable("IFLOW_START_PORT"), out var port) ? port : 8090,
            Timeout = int.TryParse(timeoutSeconds, out var seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.FromSeconds(30),
        };
    }

    public IFlowOptions ToOptions()
    {
        return new IFlowOptions
        {
            Url = Url,
            Cwd = Cwd,
            ExecutablePath = ExecutablePath,
            ProcessStartPort = ProcessStartPort,
            Timeout = Timeout,
            AuthMethodId = AuthMethodId,
            AutoStartProcess = string.IsNullOrWhiteSpace(Url),
        };
    }
}
