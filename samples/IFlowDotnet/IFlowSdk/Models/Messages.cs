namespace IFlowSdk.Models;

public abstract record Message(string Type);

public sealed record UserMessageChunk(string? Text = null, string? Path = null);

public sealed record AssistantMessageChunk(string? Text = null, string? Thought = null)
{
    public string? DisplayText => Text ?? Thought;
}

public sealed record AgentInfo(string Id, string? Name = null);

public sealed record Icon(string Kind, string Value);

public sealed record ToolCallContent(
    string Type,
    string? Markdown = null,
    string? Path = null,
    string? OldText = null,
    string? NewText = null,
    string? FileDiff = null);

public sealed record ToolCallLocation(
    string? Path = null,
    int? Line = null,
    int? Column = null);

public sealed record ToolCallConfirmation(
    string Type,
    string? Description = null,
    string? Command = null,
    string? RootCommand = null,
    string? ServerName = null,
    string? ToolName = null,
    string? ToolDisplayName = null,
    IReadOnlyList<string>? Urls = null);

public sealed record PermissionOption(string Id, string Name, string? Kind = null, string? Description = null)
{
    public static PermissionOption FromJson(JsonElement element)
    {
        return new PermissionOption(
            element.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            element.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
            element.TryGetProperty("kind", out var kind) ? kind.GetString() : null,
            element.TryGetProperty("description", out var description) ? description.GetString() : null);
    }
}

public sealed record ToolCall(
    string Id,
    string Title,
    string? Kind = null,
    string? ToolName = null,
    ToolCallStatus Status = ToolCallStatus.Pending,
    ToolCallConfirmation? Confirmation = null,
    ToolCallContent? Content = null,
    IReadOnlyList<ToolCallLocation>? Locations = null,
    IReadOnlyDictionary<string, JsonElement>? Args = null)
{
    public static ToolCall FromJson(JsonElement element)
    {
        var confirmation = element.TryGetProperty("confirmation", out var confirmationElement)
            ? ToolCallJson.ParseConfirmation(confirmationElement)
            : null;
        var content = element.TryGetProperty("content", out var contentElement)
            ? ToolCallJson.ParseContent(contentElement)
            : null;
        var locations = element.TryGetProperty("locations", out var locationsElement)
            ? ToolCallJson.ParseLocations(locationsElement)
            : null;

        return new ToolCall(
            element.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            element.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
            element.TryGetProperty("kind", out var kind) ? kind.GetString() : null,
            element.TryGetProperty("toolName", out var toolName) ? toolName.GetString() : null,
            element.TryGetProperty("status", out var status) ? ToolCallJson.ParseStatus(status.GetString()) : ToolCallStatus.Pending,
            confirmation,
            content,
            locations,
            element.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Object
                ? args.EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value.Clone())
                : null);
    }
}

public sealed record UserMessage(IReadOnlyList<UserMessageChunk> Chunks, string? AgentId = null, AgentInfo? AgentInfo = null)
    : Message("user");

public sealed record AssistantMessage(AssistantMessageChunk Chunk, string? AgentId = null, AgentInfo? AgentInfo = null)
    : Message("assistant");

public sealed record ToolCallMessage(
    string Id,
    string Label,
    Icon Icon,
    ToolCallStatus Status,
    string? ToolName = null,
    ToolCallContent? Content = null,
    IReadOnlyList<ToolCallLocation>? Locations = null,
    ToolCallConfirmation? Confirmation = null,
    string? AgentId = null,
    AgentInfo? AgentInfo = null,
    IReadOnlyDictionary<string, JsonElement>? Args = null)
    : Message("tool_call");

public sealed record ToolResultMessage(
    string Id,
    ToolCallStatus Status,
    string? ToolName = null,
    ToolCallContent? Content = null,
    IReadOnlyList<ToolCallLocation>? Locations = null,
    ToolCallConfirmation? Confirmation = null,
    string? AgentId = null,
    AgentInfo? AgentInfo = null,
    IReadOnlyDictionary<string, JsonElement>? Args = null)
    : Message("tool_result");

public sealed record ToolConfirmationRequestMessage(
    string SessionId,
    ToolCall ToolCall,
    IReadOnlyList<PermissionOption> Options,
    int RequestId)
    : Message("tool_confirmation_request");

public sealed record PlanEntry(string Content, string Priority, string Status);

public sealed record PlanMessage(IReadOnlyList<PlanEntry> Entries) : Message("plan");

public sealed record TaskFinishMessage(StopReason? StopReason = null) : Message("task_finish");

public sealed record ErrorMessage(int Code, string MessageText, string? Details = null) : Message("error");

public sealed record RawMessage(string RawData, JsonElement? JsonData = null, string? MessageType = null, bool IsControl = false, Message? ParsedMessage = null);

internal static class ToolCallJson
{
    public static ToolCallStatus ParseStatus(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "pending" => ToolCallStatus.Pending,
            "in_progress" => ToolCallStatus.InProgress,
            "running" => ToolCallStatus.InProgress,
            "completed" => ToolCallStatus.Completed,
            "finished" => ToolCallStatus.Completed,
            "failed" => ToolCallStatus.Failed,
            "error" => ToolCallStatus.Failed,
            _ => ToolCallStatus.Pending,
        };
    }

    public static StopReason? ParseStopReason(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "end_turn" => Models.StopReason.EndTurn,
            "max_tokens" => Models.StopReason.MaxTokens,
            "refusal" => Models.StopReason.Refusal,
            "cancelled" => Models.StopReason.Cancelled,
            _ => null,
        };
    }

    public static ToolCallConfirmation? ParseConfirmation(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        IReadOnlyList<string>? urls = null;
        if (element.TryGetProperty("urls", out var urlsElement) && urlsElement.ValueKind == JsonValueKind.Array)
        {
            urls = urlsElement.EnumerateArray().Select(static x => x.GetString()).OfType<string>().ToArray();
        }

        return new ToolCallConfirmation(
            element.TryGetProperty("type", out var type) ? type.GetString() ?? "other" : "other",
            element.TryGetProperty("description", out var description) ? description.GetString() : null,
            element.TryGetProperty("command", out var command) ? command.GetString() : null,
            element.TryGetProperty("rootCommand", out var rootCommand) ? rootCommand.GetString() : null,
            element.TryGetProperty("serverName", out var serverName) ? serverName.GetString() : null,
            element.TryGetProperty("toolName", out var toolName) ? toolName.GetString() : null,
            element.TryGetProperty("toolDisplayName", out var displayName) ? displayName.GetString() : null,
            urls);
    }

    public static ToolCallContent? ParseContent(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new ToolCallContent(
            element.TryGetProperty("type", out var type) ? type.GetString() ?? "markdown" : "markdown",
            element.TryGetProperty("markdown", out var markdown) ? markdown.GetString() : null,
            element.TryGetProperty("path", out var path) ? path.GetString() : null,
            element.TryGetProperty("oldText", out var oldText) ? oldText.GetString() : null,
            element.TryGetProperty("newText", out var newText) ? newText.GetString() : null,
            element.TryGetProperty("fileDiff", out var fileDiff) ? fileDiff.GetString() : null);
    }

    public static IReadOnlyList<ToolCallLocation>? ParseLocations(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return element.EnumerateArray().Select(static location => new ToolCallLocation(
            location.TryGetProperty("path", out var path) ? path.GetString() : null,
            location.TryGetProperty("line", out var line) ? line.GetInt32() : null,
            location.TryGetProperty("column", out var column) ? column.GetInt32() : null)).ToArray();
    }
}
