namespace OpenCodeSdk;

public sealed class OpenCodeClientOptions
{
    public Uri? BaseUri { get; init; }

    public string? Directory { get; init; }

    public string? Workspace { get; init; }

    public HttpMessageHandler? HttpMessageHandler { get; init; }

    public JsonSerializerOptions? SerializerOptions { get; init; }

    public TimeSpan? Timeout { get; init; }
}
