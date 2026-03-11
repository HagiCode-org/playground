namespace CodeBuddySdk.Runtime;

public sealed record RawProcessEvent(DateTimeOffset Timestamp, string Stream, string Text);
