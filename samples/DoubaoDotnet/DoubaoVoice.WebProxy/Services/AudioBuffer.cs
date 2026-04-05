using System.Collections.Concurrent;

namespace DoubaoVoice.WebProxy.Services;

/// <summary>
/// Represents a buffered audio segment
/// </summary>
public class BufferedSegment
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int Sequence { get; set; }
}

/// <summary>
/// Audio buffer for managing audio segments
/// </summary>
public class AudioBuffer
{
    private readonly ConcurrentQueue<BufferedSegment> _buffer = new();
    private int _sequenceCounter = 0;
    private readonly object _lock = new();
    private DateTime _lastAddTime = DateTime.UtcNow;

    /// <summary>
    /// Maximum number of segments in the buffer
    /// </summary>
    public int MaxSize { get; set; } = 10;

    /// <summary>
    /// Timeout for buffer inactivity (ms)
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets the current buffer count
    /// </summary>
    public int Count => _buffer.Count;

    /// <summary>
    /// Adds audio data to the buffer
    /// </summary>
    public bool Add(byte[] data)
    {
        if (data == null || data.Length == 0)
            return false;

        lock (_lock)
        {
            // Check buffer overflow
            if (_buffer.Count >= MaxSize)
            {
                // Remove oldest segment
                _buffer.TryDequeue(out _);
            }

            var segment = new BufferedSegment
            {
                Data = data,
                Timestamp = DateTime.UtcNow,
                Sequence = ++_sequenceCounter
            };

            _buffer.Enqueue(segment);
            _lastAddTime = DateTime.UtcNow;
            return true;
        }
    }

    /// <summary>
    /// Gets the next segment from the buffer
    /// </summary>
    public BufferedSegment? Get()
    {
        if (_buffer.TryDequeue(out var segment))
        {
            _lastAddTime = DateTime.UtcNow;
            return segment;
        }
        return null;
    }

    /// <summary>
    /// Gets all segments from the buffer
    /// </summary>
    public List<BufferedSegment> GetAll()
    {
        var segments = new List<BufferedSegment>();
        while (_buffer.TryDequeue(out var segment))
        {
            segments.Add(segment);
        }
        return segments;
    }

    /// <summary>
    /// Gets the next segment as a byte array
    /// </summary>
    public byte[]? GetBytes()
    {
        return Get()?.Data;
    }

    /// <summary>
    /// Peeks at the next segment without removing it
    /// </summary>
    public BufferedSegment? Peek()
    {
        return _buffer.TryPeek(out var segment) ? segment : null;
    }

    /// <summary>
    /// Clears the buffer
    /// </summary>
    public void Clear()
    {
        while (_buffer.TryDequeue(out _)) { }
        _sequenceCounter = 0;
    }

    /// <summary>
    /// Checks if the buffer has timed out
    /// </summary>
    public bool HasTimedOut()
    {
        return (DateTime.UtcNow - _lastAddTime).TotalMilliseconds > TimeoutMs;
    }

    /// <summary>
    /// Gets the total size of all buffered data
    /// </summary>
    public int GetTotalSize()
    {
        return _buffer.Sum(s => s.Data.Length);
    }

    /// <summary>
    /// Gets the time since last activity
    /// </summary>
    public TimeSpan GetTimeSinceLastActivity()
    {
        return DateTime.UtcNow - _lastAddTime;
    }
}
