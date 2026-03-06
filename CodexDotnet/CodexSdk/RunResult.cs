namespace CodexSdk;

public sealed record Usage(long InputTokens, long CachedInputTokens, long OutputTokens);

public sealed record RunResult(IReadOnlyList<ThreadItem> Items, string FinalResponse, Usage? Usage);
