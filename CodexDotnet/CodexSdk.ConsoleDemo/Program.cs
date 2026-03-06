using CodexSdk;

var codex = new Codex(new CodexOptions
{
    CodexPathOverride = Environment.GetEnvironmentVariable("CODEX_EXECUTABLE"),
    BaseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL"),
    ApiKey = Environment.GetEnvironmentVariable("CODEX_API_KEY"),
});

var threadOptions = new ThreadOptions
{
    WorkingDirectory = Environment.GetEnvironmentVariable("CODEX_WORKING_DIR"),
    SandboxMode = Environment.GetEnvironmentVariable("CODEX_SANDBOX_MODE"),
    Model = Environment.GetEnvironmentVariable("CODEX_MODEL"),
    ApprovalPolicy = Environment.GetEnvironmentVariable("CODEX_APPROVAL_POLICY"),
};

var thread = codex.StartThread(threadOptions);

Console.WriteLine("Codex C# SDK demo");
Console.WriteLine("Type your prompt and press Enter. Use /exit to quit.");
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

    try
    {
        await foreach (var @event in thread.RunStreamedAsync(prompt))
        {
            PrintEvent(@event);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }

    Console.WriteLine();
}

return;

static void PrintEvent(ThreadEvent @event)
{
    switch (@event)
    {
        case ThreadStartedEvent started:
            Console.WriteLine($"[thread] started: {started.ThreadId}");
            break;
        case TurnStartedEvent:
            Console.WriteLine("[turn] started");
            break;
        case TurnCompletedEvent completed:
            Console.WriteLine(
                $"[turn] completed (input={completed.Usage.InputTokens}, cached={completed.Usage.CachedInputTokens}, output={completed.Usage.OutputTokens})");
            break;
        case TurnFailedEvent failed:
            Console.WriteLine($"[turn] failed: {failed.Error.Message}");
            break;
        case ItemCompletedEvent completedItem:
            PrintItem("completed", completedItem.Item);
            break;
        case ItemUpdatedEvent updatedItem:
            PrintItem("updated", updatedItem.Item);
            break;
        case ItemStartedEvent startedItem:
            PrintItem("started", startedItem.Item);
            break;
        case ThreadErrorEvent streamError:
            Console.WriteLine($"[stream] error: {streamError.Message}");
            break;
        case UnknownThreadEvent unknown:
            Console.WriteLine($"[event] unknown type: {unknown.EventType}");
            break;
    }
}

static void PrintItem(string lifecycle, ThreadItem item)
{
    switch (item)
    {
        case AgentMessageItem agentMessage:
            Console.WriteLine($"[item/{lifecycle}] assistant: {agentMessage.Text}");
            break;
        case ReasoningItem reasoning:
            Console.WriteLine($"[item/{lifecycle}] reasoning: {reasoning.Text}");
            break;
        case CommandExecutionItem command:
            Console.WriteLine(
                $"[item/{lifecycle}] command: {command.Command} (status={command.Status}, exit={command.ExitCode?.ToString() ?? "n/a"})");
            if (!string.IsNullOrWhiteSpace(command.AggregatedOutput))
            {
                Console.WriteLine(command.AggregatedOutput.TrimEnd());
            }
            break;
        case FileChangeItem fileChange:
            Console.WriteLine($"[item/{lifecycle}] file change (status={fileChange.Status})");
            foreach (var change in fileChange.Changes)
            {
                Console.WriteLine($"  - {change.Kind}: {change.Path}");
            }
            break;
        case McpToolCallItem mcp:
            Console.WriteLine($"[item/{lifecycle}] mcp: {mcp.Server}/{mcp.Tool} (status={mcp.Status})");
            break;
        case TodoListItem todos:
            Console.WriteLine($"[item/{lifecycle}] todo list:");
            foreach (var todo in todos.Items)
            {
                Console.WriteLine($"  - [{(todo.Completed ? "x" : " ")}] {todo.Text}");
            }
            break;
        case WebSearchItem webSearch:
            Console.WriteLine($"[item/{lifecycle}] web search: {webSearch.Query ?? "(empty query)"}");
            break;
        case ErrorItem error:
            Console.WriteLine($"[item/{lifecycle}] error: {error.Message}");
            break;
        case UnknownThreadItem unknown:
            Console.WriteLine($"[item/{lifecycle}] unknown type: {unknown.ItemType}");
            break;
    }
}
