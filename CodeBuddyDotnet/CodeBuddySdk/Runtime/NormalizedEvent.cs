namespace CodeBuddySdk.Runtime;

public sealed record NormalizedEvent(DateTimeOffset Timestamp, string Stream, string Kind, string Content);
