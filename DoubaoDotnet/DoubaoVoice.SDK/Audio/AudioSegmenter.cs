namespace DoubaoVoice.SDK.Audio;

/// <summary>
/// Audio segmenter for splitting audio data into chunks
/// </summary>
public static class AudioSegmenter
{
    /// <summary>
    /// Segments audio data into chunks of a specified duration
    /// </summary>
    /// <param name="audioData">Raw audio data</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <param name="channels">Number of channels</param>
    /// <param name="bitsPerSample">Bits per sample</param>
    /// <param name="segmentDurationMs">Duration of each segment in milliseconds</param>
    /// <returns>List of audio segments</returns>
    public static List<byte[]> SegmentAudio(
        byte[] audioData,
        int sampleRate,
        int channels,
        int bitsPerSample,
        int segmentDurationMs = 200)
    {
        if (audioData == null || audioData.Length == 0)
            return new List<byte[]>();

        var segmentSize = CalculateSegmentSize(sampleRate, channels, bitsPerSample, segmentDurationMs);
        var segments = new List<byte[]>();

        for (int i = 0; i < audioData.Length; i += segmentSize)
        {
            var remaining = audioData.Length - i;
            var size = Math.Min(segmentSize, remaining);

            var segment = new byte[size];
            Array.Copy(audioData, i, segment, 0, size);
            segments.Add(segment);
        }

        return segments;
    }

    /// <summary>
    /// Calculates the segment size in bytes based on audio parameters
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <param name="channels">Number of channels</param>
    /// <param name="bitsPerSample">Bits per sample</param>
    /// <param name="segmentDurationMs">Duration of each segment in milliseconds</param>
    /// <returns>Segment size in bytes</returns>
    public static int CalculateSegmentSize(int sampleRate, int channels, int bitsPerSample, int segmentDurationMs)
    {
        var bytesPerSample = bitsPerSample / 8;
        var bytesPerSecond = sampleRate * channels * bytesPerSample;
        var segmentDurationSeconds = segmentDurationMs / 1000.0;
        return (int)(bytesPerSecond * segmentDurationSeconds);
    }

    /// <summary>
    /// Gets the total number of segments for the given audio data
    /// </summary>
    /// <param name="audioData">Raw audio data</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <param name="channels">Number of channels</param>
    /// <param name="bitsPerSample">Bits per sample</param>
    /// <param name="segmentDurationMs">Duration of each segment in milliseconds</param>
    /// <returns>Total number of segments</returns>
    public static int GetSegmentCount(
        byte[] audioData,
        int sampleRate,
        int channels,
        int bitsPerSample,
        int segmentDurationMs = 200)
    {
        if (audioData == null || audioData.Length == 0)
            return 0;

        var segmentSize = CalculateSegmentSize(sampleRate, channels, bitsPerSample, segmentDurationMs);
        return (int)Math.Ceiling((double)audioData.Length / segmentSize);
    }

    /// <summary>
    /// Segments audio data and yields segments as they are created
    /// </summary>
    public static IEnumerable<byte[]> SegmentAudioLazy(
        byte[] audioData,
        int sampleRate,
        int channels,
        int bitsPerSample,
        int segmentDurationMs = 200)
    {
        if (audioData == null || audioData.Length == 0)
            yield break;

        var segmentSize = CalculateSegmentSize(sampleRate, channels, bitsPerSample, segmentDurationMs);

        for (int i = 0; i < audioData.Length; i += segmentSize)
        {
            var remaining = audioData.Length - i;
            var size = Math.Min(segmentSize, remaining);

            var segment = new byte[size];
            Array.Copy(audioData, i, segment, 0, size);
            yield return segment;
        }
    }
}