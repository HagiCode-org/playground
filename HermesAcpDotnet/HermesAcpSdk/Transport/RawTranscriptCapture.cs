namespace HermesAcpSdk.Transport;

public enum TranscriptChannel
{
    Outbound,
    Inbound,
    Stderr,
    Info,
}

public sealed record TranscriptEntry(DateTimeOffset Timestamp, TranscriptChannel Channel, string Content);

public sealed class RawTranscriptCapture
{
    private readonly List<TranscriptEntry> _entries = [];
    private readonly Lock _sync = new();

    public event Action<TranscriptEntry>? EntryRecorded;

    public void Record(TranscriptChannel channel, string content)
    {
        var entry = new TranscriptEntry(DateTimeOffset.UtcNow, channel, content);
        lock (_sync)
        {
            _entries.Add(entry);
        }

        EntryRecorded?.Invoke(entry);
    }

    public IReadOnlyList<TranscriptEntry> Snapshot()
    {
        lock (_sync)
        {
            return _entries.ToArray();
        }
    }

    public string ToPlainText()
    {
        return string.Join(Environment.NewLine, Snapshot().Select(static entry => $"[{entry.Timestamp:O}] {entry.Channel}: {entry.Content}"));
    }
}
