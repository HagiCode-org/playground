using KimiAcpSdk.Runtime;

namespace KimiAcpSdk.Reporting;

public static class MarkdownReportWriter
{
    public static string Build(KimiRunResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Kimi ACP Run Report ({result.ProfileName})");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Started | {result.StartedAt:yyyy-MM-dd HH:mm:ss zzz} |");
        builder.AppendLine($"| Completed | {result.CompletedAt:yyyy-MM-dd HH:mm:ss zzz} |");
        builder.AppendLine($"| Duration | {result.Duration.TotalSeconds:F2}s |");
        builder.AppendLine($"| Profile | {result.ProfileName} |");
        builder.AppendLine($"| Prompt | {Escape(result.PromptText ?? "(none)")} |");
        builder.AppendLine($"| Failure Stage | {Escape(result.FailureStage ?? "(none)")} |");
        builder.AppendLine($"| Failure Message | {Escape(result.FailureMessage ?? "(none)")} |");
        builder.AppendLine();
        builder.AppendLine("## Effective Launch Contract");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Executable | `{result.EffectiveLaunch.ExecutablePath}` |");
        builder.AppendLine($"| Arguments | `{string.Join(' ', result.EffectiveLaunch.Arguments)}` |");
        builder.AppendLine($"| Working Directory | `{result.EffectiveLaunch.WorkingDirectory}` |");
        builder.AppendLine($"| Artifact Output | `{result.EffectiveLaunch.ArtifactOutputPath}` |");
        builder.AppendLine($"| Timeout Seconds | {result.EffectiveLaunch.TimeoutSeconds} |");
        builder.AppendLine();
        builder.AppendLine("### Environment");
        builder.AppendLine();
        foreach (var pair in result.EffectiveLaunch.EnvironmentVariables.OrderBy(static pair => pair.Key))
        {
            builder.AppendLine($"- `{pair.Key}` = `{pair.Value}`");
        }

        if (result.EffectiveLaunch.EnvironmentVariables.Count == 0)
        {
            builder.AppendLine("- (none)");
        }

        builder.AppendLine();
        builder.AppendLine("## Feature Matrix");
        builder.AppendLine();
        builder.AppendLine("| Feature | Status | Duration | Details |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var feature in result.Features)
        {
            builder.AppendLine($"| {feature.FeatureId} | {feature.Status} | {feature.Duration.TotalSeconds:F2}s | {Escape(feature.Details ?? string.Empty)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Parsed Outcomes");
        builder.AppendLine();
        builder.AppendLine($"- Initialize auth methods: {string.Join(", ", result.Initialize?.AuthMethods.Select(static method => method.Id) ?? [])}");
        builder.AppendLine($"- Session Id: {Escape(result.Session?.SessionId ?? "(none)")}");
        builder.AppendLine($"- Prompt stop reason: {Escape(result.Prompt?.StopReason ?? "(none)")}");
        builder.AppendLine($"- Prompt final text: {Escape(result.Prompt?.FinalText ?? "(none)")}");
        builder.AppendLine($"- Transcript entries: {result.Transcript.Count}");
        builder.AppendLine();
        builder.AppendLine("## Transcript Preview");
        builder.AppendLine();
        foreach (var entry in result.Transcript.TakeLast(10))
        {
            builder.AppendLine($"- [{entry.Timestamp:HH:mm:ss}] {entry.Channel}: {Escape(entry.Content)}");
        }

        if (result.Transcript.Count == 0)
        {
            builder.AppendLine("- (no transcript captured)");
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|").Replace(Environment.NewLine, "<br />").Replace("\n", "<br />");
    }
}
