using System.Text.Json;

namespace CodexSdk;

public abstract record ThreadItem(string Id, string Type);

public sealed record AgentMessageItem(string Id, string Text)
    : ThreadItem(Id, "agent_message");

public sealed record ReasoningItem(string Id, string Text)
    : ThreadItem(Id, "reasoning");

public sealed record CommandExecutionItem(
    string Id,
    string Command,
    string AggregatedOutput,
    int? ExitCode,
    string Status
) : ThreadItem(Id, "command_execution");

public sealed record FileUpdateChange(string Path, string Kind);

public sealed record FileChangeItem(
    string Id,
    IReadOnlyList<FileUpdateChange> Changes,
    string Status
) : ThreadItem(Id, "file_change");

public sealed record McpToolCallItem(
    string Id,
    string Server,
    string Tool,
    JsonElement? Arguments,
    JsonElement? Result,
    JsonElement? Error,
    string Status
) : ThreadItem(Id, "mcp_tool_call");

public sealed record WebSearchItem(string Id, string? Query)
    : ThreadItem(Id, "web_search");

public sealed record TodoEntry(string Text, bool Completed);

public sealed record TodoListItem(string Id, IReadOnlyList<TodoEntry> Items)
    : ThreadItem(Id, "todo_list");

public sealed record ErrorItem(string Id, string Message)
    : ThreadItem(Id, "error");

public sealed record UnknownThreadItem(string Id, string ItemType, JsonElement Raw)
    : ThreadItem(Id, ItemType);
