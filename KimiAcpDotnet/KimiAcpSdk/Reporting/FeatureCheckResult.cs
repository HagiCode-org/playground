namespace KimiAcpSdk.Reporting;

public sealed record FeatureCheckResult(
    string FeatureId,
    FeatureStatus Status,
    TimeSpan Duration,
    string? Details,
    DateTimeOffset Timestamp)
{
    public static FeatureCheckResult Passed(string featureId, TimeSpan duration, string? details = null)
        => new(featureId, FeatureStatus.Passed, duration, details, DateTimeOffset.UtcNow);

    public static FeatureCheckResult Failed(string featureId, TimeSpan duration, string details)
        => new(featureId, FeatureStatus.Failed, duration, details, DateTimeOffset.UtcNow);

    public static FeatureCheckResult Skipped(string featureId, string details)
        => new(featureId, FeatureStatus.Skipped, TimeSpan.Zero, details, DateTimeOffset.UtcNow);
}
