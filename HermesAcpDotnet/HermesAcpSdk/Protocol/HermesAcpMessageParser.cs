namespace HermesAcpSdk.Protocol;

public static class HermesAcpMessageParser
{
    public static bool TryParseFrame(string rawText, out HermesInboundFrame frame)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            frame = new HermesInboundFrame(HermesFrameKind.Unknown, rawText, default);
            return false;
        }

        if (rawText.StartsWith("//", StringComparison.Ordinal))
        {
            frame = new HermesInboundFrame(HermesFrameKind.Control, rawText, default, ControlMessage: rawText.Trim());
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(rawText);
            var root = document.RootElement.Clone();
            var requestId = TryGetRequestId(root);

            if (root.TryGetProperty("error", out _))
            {
                frame = new HermesInboundFrame(HermesFrameKind.Error, rawText, root, requestId);
                return true;
            }

            if (root.TryGetProperty("result", out _) && requestId.HasValue)
            {
                frame = new HermesInboundFrame(HermesFrameKind.Response, rawText, root, requestId);
                return true;
            }

            if (root.TryGetProperty("method", out var methodElement))
            {
                frame = new HermesInboundFrame(HermesFrameKind.Notification, rawText, root, requestId, methodElement.GetString());
                return true;
            }
        }
        catch (JsonException)
        {
        }

        frame = new HermesInboundFrame(HermesFrameKind.Unknown, rawText, default);
        return false;
    }

    public static HermesInitializeResult ParseInitializeResult(JsonElement responsePayload)
    {
        var result = responsePayload.GetProperty("result").Clone();
        var authMethods = result.TryGetProperty("authMethods", out var authMethodsElement)
            ? authMethodsElement.EnumerateArray().Select(static method => new HermesAuthMethod(
                method.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                method.TryGetProperty("name", out var name) ? name.GetString() : null,
                method.TryGetProperty("description", out var description) ? description.GetString() : null)).Where(static method => !string.IsNullOrWhiteSpace(method.Id)).ToArray()
            : Array.Empty<HermesAuthMethod>();
        var isAuthenticated = result.TryGetProperty("isAuthenticated", out var authElement) && authElement.ValueKind is JsonValueKind.True or JsonValueKind.False && authElement.GetBoolean();
        return new HermesInitializeResult(isAuthenticated, authMethods, result);
    }

    public static HermesAuthenticationResult ParseAuthenticateResult(string methodId, JsonElement responsePayload)
    {
        var result = responsePayload.GetProperty("result").Clone();
        var succeeded = true;
        if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("accepted", out var acceptedElement) && acceptedElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            succeeded = acceptedElement.GetBoolean();
        }

        return new HermesAuthenticationResult(methodId, succeeded, result);
    }

    public static HermesSessionStartResult ParseSessionStart(JsonElement responsePayload)
    {
        var result = responsePayload.GetProperty("result").Clone();
        var sessionId = result.TryGetProperty("sessionId", out var sessionIdElement)
            ? sessionIdElement.GetString()
            : result.TryGetProperty("id", out var legacyIdElement)
                ? legacyIdElement.GetString()
                : null;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new HermesProtocolException("session/new response did not contain a sessionId.");
        }

        return new HermesSessionStartResult(sessionId, result);
    }

    public static HermesPromptUpdate? ParsePromptUpdate(JsonElement notificationPayload)
    {
        if (!notificationPayload.TryGetProperty("params", out var paramsElement))
        {
            return null;
        }

        var updateElement = paramsElement.TryGetProperty("update", out var update)
            ? update
            : paramsElement.TryGetProperty("delta", out var delta)
                ? delta
                : paramsElement;

        var kind = updateElement.TryGetProperty("kind", out var kindElement)
            ? kindElement.GetString() ?? "unknown"
            : updateElement.TryGetProperty("sessionUpdate", out var sessionUpdateElement)
                ? sessionUpdateElement.GetString() ?? "unknown"
            : updateElement.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString() ?? "unknown"
                : "unknown";
        var text = TryExtractText(updateElement);
        return new HermesPromptUpdate(kind, text, updateElement.Clone());
    }

    public static string? ExtractPromptText(JsonElement responsePayload)
    {
        var result = responsePayload.TryGetProperty("result", out var resultElement)
            ? resultElement
            : responsePayload;

        if (result.ValueKind == JsonValueKind.Object)
        {
            if (result.TryGetProperty("outputText", out var outputText) && outputText.ValueKind == JsonValueKind.String)
            {
                return outputText.GetString();
            }

            if (result.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
            {
                return textElement.GetString();
            }

            if (result.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.Array)
            {
                var textChunks = contentElement.EnumerateArray()
                    .Select(TryExtractText)
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();
                if (textChunks.Length > 0)
                {
                    return string.Join(Environment.NewLine, textChunks);
                }
            }
        }

        return null;
    }

    public static string? ExtractStopReason(JsonElement responsePayload)
    {
        var result = responsePayload.TryGetProperty("result", out var resultElement)
            ? resultElement
            : responsePayload;
        if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("stopReason", out var stopReasonElement) && stopReasonElement.ValueKind == JsonValueKind.String)
        {
            return stopReasonElement.GetString();
        }

        return null;
    }

    private static int? TryGetRequestId(JsonElement root)
    {
        if (!root.TryGetProperty("id", out var idElement))
        {
            return null;
        }

        return idElement.ValueKind switch
        {
            JsonValueKind.Number when idElement.TryGetInt32(out var numericId) => numericId,
            JsonValueKind.String when int.TryParse(idElement.GetString(), out var stringId) => stringId,
            _ => null,
        };
    }

    private static string? TryExtractText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
            {
                return textElement.GetString();
            }

            if (element.TryGetProperty("content", out var contentElement))
            {
                if (contentElement.ValueKind == JsonValueKind.String)
                {
                    return contentElement.GetString();
                }

                if (contentElement.ValueKind == JsonValueKind.Object)
                {
                    return TryExtractText(contentElement);
                }

                if (contentElement.ValueKind == JsonValueKind.Array)
                {
                    var parts = contentElement.EnumerateArray()
                        .Select(TryExtractText)
                        .Where(static value => !string.IsNullOrWhiteSpace(value))
                        .ToArray();
                    if (parts.Length > 0)
                    {
                        return string.Join(Environment.NewLine, parts);
                    }
                }
            }
        }

        return null;
    }
}
