using System.Text;
using System.Text.Json;

namespace CodeBuddySdk.Runtime;

public sealed class EventNormalizer
{
    public NormalizedProcessOutput Normalize(RawProcessResult rawResult)
    {
        var events = new List<NormalizedEvent>();
        var finalContent = new StringBuilder();
        var transcript = new StringBuilder();

        foreach (var rawEvent in rawResult.Events.OrderBy(static x => x.Timestamp))
        {
            var normalized = NormalizeEvent(rawEvent);
            events.Add(normalized);
            transcript.AppendLine($"[{normalized.Timestamp:O}] {normalized.Stream}:{normalized.Kind} {normalized.Content}");

            if (normalized.Stream == "stdout"
                && (normalized.Kind is "text" or "final" or "message")
                && !string.IsNullOrWhiteSpace(normalized.Content))
            {
                if (finalContent.Length > 0)
                {
                    finalContent.AppendLine();
                }

                finalContent.Append(normalized.Content.Trim());
            }
        }

        if (finalContent.Length == 0 && !string.IsNullOrWhiteSpace(rawResult.StdOut))
        {
            finalContent.Append(rawResult.StdOut.Trim());
        }

        return new NormalizedProcessOutput
        {
            Events = events,
            FinalContent = finalContent.ToString(),
            Transcript = transcript.ToString(),
        };
    }

    private static NormalizedEvent NormalizeEvent(RawProcessEvent rawEvent)
    {
        var text = rawEvent.Text.Trim();
        if (TryParseJsonEvent(text, out var kind, out var content))
        {
            return new NormalizedEvent(rawEvent.Timestamp, rawEvent.Stream, kind, content);
        }

        return new NormalizedEvent(rawEvent.Timestamp, rawEvent.Stream, rawEvent.Stream == "stderr" ? "stderr" : "text", text);
    }

    private static bool TryParseJsonEvent(string text, out string kind, out string content)
    {
        kind = "text";
        content = text;

        if (!text.StartsWith("{", StringComparison.Ordinal) || !text.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            kind = root.TryGetProperty("type", out var typeProperty)
                ? typeProperty.GetString() ?? "json"
                : "json";

            if (root.TryGetProperty("content", out var contentProperty))
            {
                content = contentProperty.ValueKind == JsonValueKind.String
                    ? contentProperty.GetString() ?? string.Empty
                    : contentProperty.GetRawText();
                return true;
            }

            if (root.TryGetProperty("message", out var messageProperty))
            {
                content = messageProperty.GetString() ?? string.Empty;
                return true;
            }

            if (root.TryGetProperty("name", out var nameProperty))
            {
                content = nameProperty.GetString() ?? string.Empty;
                return true;
            }

            content = root.GetRawText();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
