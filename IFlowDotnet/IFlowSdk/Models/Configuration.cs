namespace IFlowSdk.Models;

public sealed record EnvVariable(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value);

public sealed record McpServer
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("timeout")]
    public int? Timeout { get; init; }

    [JsonPropertyName("trust")]
    public bool? Trust { get; init; }

    [JsonPropertyName("command")]
    public string? Command { get; init; }

    [JsonPropertyName("args")]
    public IReadOnlyList<string>? Args { get; init; }

    [JsonPropertyName("env")]
    public IReadOnlyList<EnvVariable>? Env { get; init; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("headers")]
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record HookCommand(
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("timeout") ] int? Timeout = null);

public sealed record HookEventConfig(
    [property: JsonPropertyName("matcher")] string Matcher,
    [property: JsonPropertyName("hooks")] IReadOnlyList<HookCommand> Hooks);

public sealed record CommandConfig(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("description")] string? Description = null);

public sealed record CreateAgentConfig(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("tools")] IReadOnlyList<string>? Tools = null,
    [property: JsonPropertyName("model")] string? Model = null);

public sealed record AuthMethodInfo(
    [property: JsonPropertyName("apiKey")] string? ApiKey = null,
    [property: JsonPropertyName("baseUrl")] string? BaseUrl = null,
    [property: JsonPropertyName("modelName")] string? ModelName = null);

public sealed record SessionSettings
{
    [JsonPropertyName("allowed_tools")]
    public IReadOnlyList<string>? AllowedTools { get; init; }

    [JsonPropertyName("system_prompt")]
    public string? SystemPrompt { get; init; }

    [JsonPropertyName("append_system_prompt")]
    public string? AppendSystemPrompt { get; init; }

    [JsonPropertyName("permission_mode")]
    public string? PermissionMode => ApprovalMode switch
    {
        null => null,
        Models.ApprovalMode.Default => "default",
        Models.ApprovalMode.Smart => "smart",
        Models.ApprovalMode.Yolo => "yolo",
        Models.ApprovalMode.Plan => "plan",
        _ => null,
    };

    [JsonIgnore]
    public ApprovalMode? ApprovalMode { get; init; }

    [JsonPropertyName("max_turns")]
    public int? MaxTurns { get; init; }

    [JsonPropertyName("disallowed_tools")]
    public IReadOnlyList<string>? DisallowedTools { get; init; }

    [JsonPropertyName("add_dirs")]
    public IReadOnlyList<string>? AddDirs { get; init; }
}

public sealed record IFlowOptions
{
    public const string DefaultUrl = "ws://localhost:8090/acp";

    [JsonIgnore]
    public string? Url { get; init; }

    [JsonIgnore]
    public string Cwd { get; init; } = Directory.GetCurrentDirectory();

    [JsonIgnore]
    public IReadOnlyList<McpServer> McpServers { get; init; } = Array.Empty<McpServer>();

    [JsonIgnore]
    public IReadOnlyDictionary<HookEventType, IReadOnlyList<HookEventConfig>>? Hooks { get; init; }

    [JsonIgnore]
    public IReadOnlyList<CommandConfig>? Commands { get; init; }

    [JsonIgnore]
    public IReadOnlyList<CreateAgentConfig>? Agents { get; init; }

    [JsonIgnore]
    public SessionSettings? SessionSettings { get; init; }

    [JsonIgnore]
    public ApprovalMode ApprovalMode { get; init; } = ApprovalMode.Yolo;

    [JsonIgnore]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    [JsonIgnore]
    public bool AutoStartProcess { get; init; } = true;

    [JsonIgnore]
    public int ProcessStartPort { get; init; } = 8090;

    [JsonIgnore]
    public string? AuthMethodId { get; init; }

    [JsonIgnore]
    public AuthMethodInfo? AuthMethodInfo { get; init; }

    [JsonIgnore]
    public string? SessionId { get; init; }

    [JsonIgnore]
    public int MaxMessageQueueSize { get; init; } = 10_000;

    [JsonIgnore]
    public QueueOverflowStrategy QueueOverflowStrategy { get; init; } = QueueOverflowStrategy.DropOldest;

    [JsonIgnore]
    public int MaxPendingRequests { get; init; } = 1_000;

    [JsonIgnore]
    public TimeSpan RequestTtl { get; init; } = TimeSpan.FromMinutes(5);

    [JsonIgnore]
    public string? ProcessLogFile { get; init; }

    [JsonIgnore]
    public string? ExecutablePath { get; init; }

    [JsonIgnore]
    public string ResolvedUrl => string.IsNullOrWhiteSpace(Url) ? DefaultUrl : Url;

    public void Validate()
    {
        if (!Uri.TryCreate(ResolvedUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "ws" && uri.Scheme != "wss"))
        {
            throw new ArgumentException($"Invalid iFlow ACP URL: {ResolvedUrl}", nameof(Url));
        }

        if (string.IsNullOrWhiteSpace(Cwd))
        {
            throw new ArgumentException("Working directory is required.", nameof(Cwd));
        }

        if (MaxMessageQueueSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxMessageQueueSize));
        }

        if (MaxPendingRequests <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPendingRequests));
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        }
    }
}
