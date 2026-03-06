using System.Text.Json;

namespace CodexSdk;

internal static class EventParser
{
    public static ThreadEvent Parse(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        var type = GetRequiredString(root, "type", "event.type");

        return type switch
        {
            "thread.started" => new ThreadStartedEvent(
                GetRequiredString(root, "thread_id", "thread.started.thread_id")
            ),
            "turn.started" => new TurnStartedEvent(),
            "turn.completed" => new TurnCompletedEvent(ParseUsage(GetRequiredProperty(
                root,
                "usage",
                "turn.completed.usage"))
            ),
            "turn.failed" => ParseTurnFailedEvent(root),
            "item.started" => new ItemStartedEvent(ParseItem(GetRequiredProperty(
                root,
                "item",
                "item.started.item"))
            ),
            "item.updated" => new ItemUpdatedEvent(ParseItem(GetRequiredProperty(
                root,
                "item",
                "item.updated.item"))
            ),
            "item.completed" => new ItemCompletedEvent(ParseItem(GetRequiredProperty(
                root,
                "item",
                "item.completed.item"))
            ),
            "error" => new ThreadErrorEvent(GetRequiredString(root, "message", "error.message")),
            _ => new UnknownThreadEvent(type, root.Clone()),
        };
    }

    private static TurnFailedEvent ParseTurnFailedEvent(JsonElement root)
    {
        var errorNode = GetRequiredProperty(root, "error", "turn.failed.error");
        return new TurnFailedEvent(new ThreadError(GetRequiredString(
            errorNode,
            "message",
            "turn.failed.error.message")));
    }

    private static Usage ParseUsage(JsonElement usage)
    {
        return new Usage(
            GetRequiredInt64(usage, "input_tokens", "usage.input_tokens"),
            GetRequiredInt64(usage, "cached_input_tokens", "usage.cached_input_tokens"),
            GetRequiredInt64(usage, "output_tokens", "usage.output_tokens")
        );
    }

    private static ThreadItem ParseItem(JsonElement item)
    {
        var id = GetRequiredString(item, "id", "item.id");
        var itemType = GetRequiredString(item, "type", "item.type");

        return itemType switch
        {
            "agent_message" => new AgentMessageItem(id, GetRequiredString(
                item,
                "text",
                "item.agent_message.text")),
            "reasoning" => new ReasoningItem(id, GetRequiredString(
                item,
                "text",
                "item.reasoning.text")),
            "command_execution" => new CommandExecutionItem(
                id,
                GetRequiredString(item, "command", "item.command_execution.command"),
                GetOptionalString(item, "aggregated_output") ?? string.Empty,
                GetOptionalInt32(item, "exit_code"),
                GetRequiredString(item, "status", "item.command_execution.status")
            ),
            "file_change" => new FileChangeItem(
                id,
                ParseFileChanges(item),
                GetRequiredString(item, "status", "item.file_change.status")
            ),
            "mcp_tool_call" => new McpToolCallItem(
                id,
                GetRequiredString(item, "server", "item.mcp_tool_call.server"),
                GetRequiredString(item, "tool", "item.mcp_tool_call.tool"),
                GetOptionalClone(item, "arguments"),
                GetOptionalClone(item, "result"),
                GetOptionalClone(item, "error"),
                GetRequiredString(item, "status", "item.mcp_tool_call.status")
            ),
            "web_search" => new WebSearchItem(id, GetOptionalString(item, "query")),
            "todo_list" => new TodoListItem(id, ParseTodoItems(item)),
            "error" => new ErrorItem(id, GetRequiredString(item, "message", "item.error.message")),
            _ => new UnknownThreadItem(id, itemType, item.Clone()),
        };
    }

    private static IReadOnlyList<FileUpdateChange> ParseFileChanges(JsonElement item)
    {
        if (!item.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var parsed = new List<FileUpdateChange>();
        foreach (var change in changes.EnumerateArray())
        {
            parsed.Add(new FileUpdateChange(
                GetRequiredString(change, "path", "item.file_change.changes.path"),
                GetRequiredString(change, "kind", "item.file_change.changes.kind")));
        }

        return parsed;
    }

    private static IReadOnlyList<TodoEntry> ParseTodoItems(JsonElement item)
    {
        if (!item.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var parsed = new List<TodoEntry>();
        foreach (var todo in items.EnumerateArray())
        {
            parsed.Add(new TodoEntry(
                GetRequiredString(todo, "text", "item.todo_list.items.text"),
                GetOptionalBoolean(todo, "completed") ?? false));
        }

        return parsed;
    }

    private static JsonElement GetRequiredProperty(JsonElement element, string name, string path)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            throw new InvalidOperationException($"Missing required property '{path}'.");
        }

        return property;
    }

    private static string GetRequiredString(JsonElement element, string name, string path)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Missing or invalid string property '{path}'.");
        }

        return property.GetString()!;
    }

    private static string? GetOptionalString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static int? GetOptionalInt32(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        if (!property.TryGetInt32(out var value))
        {
            return null;
        }

        return value;
    }

    private static long GetRequiredInt64(JsonElement element, string name, string path)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"Missing or invalid number property '{path}'.");
        }

        if (!property.TryGetInt64(out var value))
        {
            throw new InvalidOperationException($"Property '{path}' must be an integer.");
        }

        return value;
    }

    private static bool? GetOptionalBoolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static JsonElement? GetOptionalClone(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.Clone();
    }
}
