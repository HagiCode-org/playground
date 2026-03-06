using System.Text.Json.Nodes;

namespace CodexSdk;

public sealed class CodexOptions
{
    public string? CodexPathOverride { get; init; }

    public string? BaseUrl { get; init; }

    public string? ApiKey { get; init; }

    public JsonObject? Config { get; init; }

    public IReadOnlyDictionary<string, string>? Env { get; init; }
}
