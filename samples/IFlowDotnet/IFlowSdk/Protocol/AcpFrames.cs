using IFlowSdk.Models;
using IFlowSdk.Utilities;

namespace IFlowSdk.Protocol;

public enum AcpFrameKind
{
    Control,
    SessionUpdate,
    PermissionRequest,
    Response,
    Error,
    LegacyToolCall,
    LegacyToolUpdate,
    LegacyTaskFinish,
    Unknown,
}

public sealed record AcpInboundFrame(
    AcpFrameKind Kind,
    string RawText,
    JsonElement Payload,
    int? RequestId = null,
    string? ControlMessage = null,
    string? Method = null,
    string? SessionUpdateType = null);

public static class AcpMessageSerializer
{
    public static string CreateInitializeRequest(int requestId, IEnumerable<McpServer>? mcpServers, IReadOnlyDictionary<HookEventType, IReadOnlyList<HookEventConfig>>? hooks, IEnumerable<CommandConfig>? commands, IEnumerable<CreateAgentConfig>? agents)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["protocolVersion"] = 1,
            ["clientCapabilities"] = new { fs = new { readTextFile = true, writeTextFile = true } },
        };

        if (mcpServers is not null && mcpServers.Any())
        {
            parameters["mcpServers"] = mcpServers;
        }

        if (hooks is not null && hooks.Count > 0)
        {
            var mappedHooks = hooks.ToDictionary(
                static pair => pair.Key.ToString(),
                static pair => (object)pair.Value);
            parameters["hooks"] = mappedHooks;
        }

        if (commands is not null && commands.Any())
        {
            parameters["commands"] = commands;
        }

        if (agents is not null && agents.Any())
        {
            parameters["agents"] = agents;
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "initialize",
            ["params"] = parameters,
        }, IFlowJson.Default);
    }

    public static string CreateAuthenticateRequest(int requestId, string methodId, AuthMethodInfo? methodInfo)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["methodId"] = methodId,
        };

        if (methodInfo is not null)
        {
            parameters["methodInfo"] = methodInfo;
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "authenticate",
            ["params"] = parameters,
        }, IFlowJson.Default);
    }

    public static string CreateSessionNewRequest(int requestId, string cwd, IEnumerable<McpServer>? mcpServers, IReadOnlyDictionary<HookEventType, IReadOnlyList<HookEventConfig>>? hooks, IEnumerable<CommandConfig>? commands, IEnumerable<CreateAgentConfig>? agents, SessionSettings? settings)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "session/new",
            ["params"] = BuildSessionParameters(cwd, mcpServers, hooks, commands, agents, settings, sessionId: null),
        }, IFlowJson.Default);
    }

    public static string CreateSessionLoadRequest(int requestId, string sessionId, string cwd, IEnumerable<McpServer>? mcpServers, IReadOnlyDictionary<HookEventType, IReadOnlyList<HookEventConfig>>? hooks, IEnumerable<CommandConfig>? commands, IEnumerable<CreateAgentConfig>? agents, SessionSettings? settings)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "session/load",
            ["params"] = BuildSessionParameters(cwd, mcpServers, hooks, commands, agents, settings, sessionId),
        }, IFlowJson.Default);
    }

    public static string CreatePromptRequest(int requestId, string sessionId, IReadOnlyList<object> prompt)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "session/prompt",
            ["params"] = new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId,
                ["prompt"] = prompt,
            },
        }, IFlowJson.Default);
    }

    public static string CreateCancelRequest(int requestId, string sessionId)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "session/cancel",
            ["params"] = new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId,
            },
        }, IFlowJson.Default);
    }

    public static string CreatePermissionResponse(int requestId, string? optionId, bool cancelled)
    {
        object result = cancelled
            ? new Dictionary<string, object?> { ["outcome"] = new Dictionary<string, object?> { ["outcome"] = "cancelled" } }
            : new Dictionary<string, object?> { ["outcome"] = new Dictionary<string, object?> { ["outcome"] = "selected", ["optionId"] = optionId } };

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["result"] = result,
        }, IFlowJson.Default);
    }

    public static IReadOnlyList<object> BuildPrompt(string text, IReadOnlyList<string>? files)
    {
        var blocks = new List<object>
        {
            new Dictionary<string, object?>
            {
                ["type"] = "text",
                ["text"] = text,
            },
        };

        if (files is null)
        {
            return blocks;
        }

        foreach (var file in files.Where(static x => !string.IsNullOrWhiteSpace(x)))
        {
            var fullPath = Path.GetFullPath(file);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (TryGetBinaryAttachment(fullPath, extension, out var block))
            {
                blocks.Add(block);
                continue;
            }

            blocks.Add(new Dictionary<string, object?>
            {
                ["type"] = "resource_link",
                ["uri"] = new Uri(fullPath).AbsoluteUri,
                ["name"] = Path.GetFileName(fullPath),
                ["title"] = Path.GetFileNameWithoutExtension(fullPath),
                ["size"] = new FileInfo(fullPath).Length,
            });
        }

        return blocks;
    }

    private static Dictionary<string, object?> BuildSessionParameters(string cwd, IEnumerable<McpServer>? mcpServers, IReadOnlyDictionary<HookEventType, IReadOnlyList<HookEventConfig>>? hooks, IEnumerable<CommandConfig>? commands, IEnumerable<CreateAgentConfig>? agents, SessionSettings? settings, string? sessionId)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["cwd"] = cwd,
            ["mcpServers"] = mcpServers?.ToArray() ?? Array.Empty<McpServer>(),
        };

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            parameters["sessionId"] = sessionId;
        }

        if (hooks is not null && hooks.Count > 0)
        {
            var mappedHooks = hooks.ToDictionary(
                static pair => pair.Key.ToString(),
                static pair => (object)pair.Value);
            parameters["hooks"] = mappedHooks;
        }

        if (commands is not null && commands.Any())
        {
            parameters["commands"] = commands;
        }

        if (agents is not null && agents.Any())
        {
            parameters["agents"] = agents;
        }

        if (settings is not null)
        {
            parameters["settings"] = settings;
        }

        return parameters;
    }

    private static bool TryGetBinaryAttachment(string fullPath, string extension, out Dictionary<string, object?> block)
    {
        var bytes = File.ReadAllBytes(fullPath);
        var base64 = Convert.ToBase64String(bytes);

        var imageMime = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => null,
        };

        if (imageMime is not null)
        {
            block = new Dictionary<string, object?>
            {
                ["type"] = "image",
                ["data"] = base64,
                ["mimeType"] = imageMime,
            };
            return true;
        }

        var audioMime = extension switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".m4a" => "audio/mp4",
            ".ogg" => "audio/ogg",
            ".flac" => "audio/flac",
            _ => null,
        };

        if (audioMime is not null)
        {
            block = new Dictionary<string, object?>
            {
                ["type"] = "audio",
                ["data"] = base64,
                ["mimeType"] = audioMime,
            };
            return true;
        }

        block = new Dictionary<string, object?>();
        return false;
    }
}

public static class AcpMessageParser
{
    public static bool TryParseInbound(string rawText, out AcpInboundFrame frame)
    {
        if (rawText.StartsWith("//", StringComparison.Ordinal))
        {
            frame = new AcpInboundFrame(AcpFrameKind.Control, rawText, default, ControlMessage: rawText);
            return true;
        }

        using var document = JsonDocument.Parse(rawText);
        var root = document.RootElement.Clone();

        if (root.TryGetProperty("method", out var methodElement))
        {
            var method = methodElement.GetString();
            int? requestId = root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.Number
                ? idElement.GetInt32()
                : null;
            var payload = root.TryGetProperty("params", out var paramsElement) ? paramsElement.Clone() : root;

            frame = method switch
            {
                "session/update" => new AcpInboundFrame(
                    AcpFrameKind.SessionUpdate,
                    rawText,
                    payload,
                    requestId,
                    Method: method,
                    SessionUpdateType: GetNestedString(payload, "update", "sessionUpdate")),
                "session/request_permission" => new AcpInboundFrame(AcpFrameKind.PermissionRequest, rawText, payload, requestId, Method: method),
                "notifyToolCall" => new AcpInboundFrame(AcpFrameKind.LegacyToolCall, rawText, payload, requestId, Method: method),
                "updateToolCall" => new AcpInboundFrame(AcpFrameKind.LegacyToolUpdate, rawText, payload, requestId, Method: method),
                "notifyTaskFinish" => new AcpInboundFrame(AcpFrameKind.LegacyTaskFinish, rawText, payload, requestId, Method: method),
                _ => new AcpInboundFrame(AcpFrameKind.Unknown, rawText, payload, requestId, Method: method),
            };

            return true;
        }

        if (root.TryGetProperty("error", out _))
        {
            frame = new AcpInboundFrame(
                AcpFrameKind.Error,
                rawText,
                root,
                root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.Number ? idElement.GetInt32() : null);
            return true;
        }

        frame = new AcpInboundFrame(
            AcpFrameKind.Response,
            rawText,
            root,
            root.TryGetProperty("id", out var requestIdElement) && requestIdElement.ValueKind == JsonValueKind.Number ? requestIdElement.GetInt32() : null);
        return true;
    }

    public static string? GetNestedString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }
}
