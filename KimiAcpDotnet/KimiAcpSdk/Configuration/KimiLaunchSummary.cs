namespace KimiAcpSdk.Configuration;

public sealed record KimiLaunchSummary(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    string ArtifactOutputPath,
    int TimeoutSeconds);
