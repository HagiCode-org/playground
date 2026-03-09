namespace OpenCodeSdk;

public sealed class OpenCodeProcessOptions
{
    public string? ExecutablePath { get; init; }

    public string Hostname { get; init; } = "127.0.0.1";

    public int? Port { get; init; }

    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public string? LogLevel { get; init; }

    public JsonNode? Config { get; init; }

    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }
}
