using System.Buffers.Binary;
using System.IO.Compression;
using DoubaoVoice.SDK.Audio;

namespace DoubaoVoice.SDK.Protocol;

/// <summary>
/// Payload encoder for DoubaoVoice protocol
/// </summary>
public static class PayloadEncoder
{
    /// <summary>
    /// Encodes a full client request (JSON payload)
    /// </summary>
    public static byte[] EncodeFullClientRequest(string jsonPayload, int sequence = 0)
    {
        var header = ProtocolHeader.CreateFullClientRequest();
        var compressed = GzipCompressor.Compress(jsonPayload);

        var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            // Write header
            var headerBytes = HeaderEncoder.EncodeHeader(header);
            writer.Write(headerBytes);

            // Write sequence number (big endian)
            var seqBytes = BitConverter.GetBytes(sequence);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(seqBytes);
            writer.Write(seqBytes);

            // Write payload size (big endian)
            var sizeBytes = BitConverter.GetBytes(compressed.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(sizeBytes);
            writer.Write(sizeBytes);

            // Write payload
            writer.Write(compressed);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Encodes an audio segment (binary payload)
    /// </summary>
    /// <param name="audioData">Raw audio data</param>
    /// <param name="sequence">Sequence number</param>
    /// <param name="isLastSegment">Whether this is the last segment</param>
    public static byte[] EncodeAudioSegment(byte[] audioData, int sequence, bool isLastSegment)
    {
        // Use default audio parameters (16kHz, 16-bit, mono)
        return EncodeAudioSegment(audioData, sequence, isLastSegment, 16000, 1, 16);
    }

    /// <summary>
    /// Encodes an audio segment with WAV header (binary payload)
    /// </summary>
    /// <param name="pcmData">Raw PCM audio data</param>
    /// <param name="sequence">Sequence number</param>
    /// <param name="isLastSegment">Whether this is the last segment</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    /// <param name="channels">Number of audio channels</param>
    /// <param name="bitsPerSample">Bits per sample</param>
    public static byte[] EncodeAudioSegment(byte[] pcmData, int sequence, bool isLastSegment, int sampleRate, int channels, int bitsPerSample)
    {
        var header = ProtocolHeader.CreateAudioOnlyRequest(isLastSegment);

        // Convert PCM to WAV format
        var wavData = WavParser.CreateWavData(pcmData, sampleRate, (ushort)channels, (ushort)bitsPerSample);
        var compressed = GzipCompressor.Compress(wavData);

        var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            // Write header
            var headerBytes = HeaderEncoder.EncodeHeader(header);
            writer.Write(headerBytes);

            // Write sequence number (big endian)
            // For last segment, use negative sequence number
            var seq = isLastSegment ? -sequence : sequence;
            var seqBytes = BitConverter.GetBytes(seq);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(seqBytes);
            writer.Write(seqBytes);

            // Write payload size (big endian)
            var sizeBytes = BitConverter.GetBytes(compressed.Length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(sizeBytes);
            writer.Write(sizeBytes);

            // Write payload
            writer.Write(compressed);
        }

        return ms.ToArray();
    }
}