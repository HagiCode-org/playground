namespace HermesAcpSdk.Configuration;

public sealed record HermesLaunchSummary(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    string ArtifactOutputPath,
    int TimeoutSeconds);
