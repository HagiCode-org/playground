using CodeBuddySdk.Configuration;

namespace CodeBuddySdk.Runtime;

public sealed class ProcessRequest
{
    public required string ExecutablePath { get; init; }

    public required string WorkingDirectory { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    public required PromptTransport PromptTransport { get; init; }

    public string? InputText { get; init; }

    public string? PromptFilePath { get; init; }

    public required IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; }

    public required TimeSpan Timeout { get; init; }
}
