using HermesAcpSdk.Configuration;

namespace HermesAcpSdk.Protocol;

public static class HermesAcpMessageFactory
{
    public static string CreateInitializeRequest(int requestId, HermesLaunchProfile profile)
    {
        var payload = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "initialize",
            ["params"] = new Dictionary<string, object?>
            {
                ["protocolVersion"] = profile.Client.ProtocolVersion,
                ["client"] = new Dictionary<string, object?>
                {
                    ["name"] = profile.Client.Name,
                    ["version"] = profile.Client.Version,
                },
                ["clientCapabilities"] = new Dictionary<string, object?>
                {
                    ["fs"] = new Dictionary<string, object?>
                    {
                        ["readTextFile"] = true,
                    },
                },
            },
        };

        return JsonSerializer.Serialize(payload, HermesJson.Default);
    }

    public static string CreateAuthenticateRequest(int requestId, HermesAuthenticationOptions authentication, string methodId)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["methodId"] = methodId,
        };

        var methodInfo = authentication.MethodInfo
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        if (methodInfo.Count > 0)
        {
            parameters["methodInfo"] = methodInfo;
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "authenticate",
            ["params"] = parameters,
        }, HermesJson.Default);
    }

    public static string CreateSessionNewRequest(int requestId, HermesLaunchProfile profile)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "session/new",
            ["params"] = new Dictionary<string, object?>
            {
                ["cwd"] = profile.WorkingDirectory,
                ["mcpServers"] = Array.Empty<object>(),
                ["settings"] = profile.SessionDefaults.ToDictionary(),
            },
        }, HermesJson.Default);
    }

    public static string CreatePromptRequest(int requestId, string sessionId, string promptText)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "session/prompt",
            ["params"] = new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId,
                ["prompt"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["type"] = "text",
                        ["text"] = promptText,
                    },
                },
            },
        }, HermesJson.Default);
    }
}
