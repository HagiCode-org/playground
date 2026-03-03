using System.Buffers.Binary;
using System.Text;

namespace DoubaoVoice.SDK.Audio;

/// <summary>
/// WAV file header information
/// </summary>
public class WavHeader
{
    public string ChunkId { get; set; } = string.Empty;
    public uint ChunkSize { get; set; }
    public string Format { get; set; } = string.Empty;
    public string Subchunk1Id { get; set; } = string.Empty;
    public uint Subchunk1Size { get; set; }
    public ushort AudioFormat { get; set; }
    public ushort NumChannels { get; set; }
    public uint SampleRate { get; set; }
    public uint ByteRate { get; set; }
    public ushort BlockAlign { get; set; }
    public ushort BitsPerSample { get; set; }
    public string Subchunk2Id { get; set; } = string.Empty;
    public uint Subchunk2Size { get; set; }
}

/// <summary>
/// Parsed WAV file information
/// </summary>
public class WavInfo
{
    /// <summary>
    /// Number of audio channels (1 for mono, 2 for stereo)
    /// </summary>
    public int Channels { get; set; }

    /// <summary>
    /// Sample width in bytes (2 for 16-bit)
    /// </summary>
    public int SampleWidth { get; set; }

    /// <summary>
    /// Sample rate in Hz
    /// </summary>
    public int SampleRate { get; set; }

    /// <summary>
    /// Number of audio frames
    /// </summary>
    public int FrameCount { get; set; }

    /// <summary>
    /// Raw audio data (without WAV header)
    /// </summary>
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// WAV file parser
/// </summary>
public static class WavParser
{
    /// <summary>
    /// Parses a WAV file header
    /// </summary>
    public static WavHeader ParseWavHeader(byte[] data)
    {
        if (data == null || data.Length < 44)
            throw new InvalidAudioFormatException("WAV file header must be at least 44 bytes");

        var header = new WavHeader();
        var reader = new BinaryReader(new MemoryStream(data));

        header.ChunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
        header.ChunkSize = reader.ReadUInt32();
        header.Format = Encoding.ASCII.GetString(reader.ReadBytes(4));
        header.Subchunk1Id = Encoding.ASCII.GetString(reader.ReadBytes(4));
        header.Subchunk1Size = reader.ReadUInt32();
        header.AudioFormat = reader.ReadUInt16();
        header.NumChannels = reader.ReadUInt16();
        header.SampleRate = reader.ReadUInt32();
        header.ByteRate = reader.ReadUInt32();
        header.BlockAlign = reader.ReadUInt16();
        header.BitsPerSample = reader.ReadUInt16();
        header.Subchunk2Id = Encoding.ASCII.GetString(reader.ReadBytes(4));
        header.Subchunk2Size = reader.ReadUInt32();

        return header;
    }

    /// <summary>
    /// Extracts pure audio data from a WAV file (removes the header)
    /// </summary>
    public static byte[] ExtractAudioData(byte[] wavData)
    {
        if (wavData == null || wavData.Length < 44)
            throw new InvalidAudioFormatException("Invalid WAV file data");

        var header = ParseWavHeader(wavData);
        var dataStart = 44; // Standard WAV header size
        var audioDataSize = (int)header.Subchunk2Size;

        if (wavData.Length < dataStart + audioDataSize)
            throw new InvalidAudioFormatException("WAV file data is truncated");

        var audioData = new byte[audioDataSize];
        Array.Copy(wavData, dataStart, audioData, 0, audioDataSize);

        return audioData;
    }

    /// <summary>
    /// Validates WAV file format
    /// </summary>
    public static void ValidateWavFormat(byte[] data)
    {
        if (data == null || data.Length < 44)
            throw new InvalidAudioFormatException("File is too short to be a valid WAV file");

        var chunkId = Encoding.ASCII.GetString(data, 0, 4);
        var format = Encoding.ASCII.GetString(data, 8, 12);

        if (chunkId != "RIFF" || format != "WAVE")
            throw new InvalidAudioFormatException("File is not a valid WAV file (missing RIFF/WAVE markers)");

        var header = ParseWavHeader(data);

        if (header.NumChannels != 1)
            throw new InvalidAudioFormatException($"Only mono audio is supported, but {header.NumChannels} channels found");

        if (header.BitsPerSample != 16)
            throw new InvalidAudioFormatException($"Only 16-bit audio is supported, but {header.BitsPerSample}-bit found");

        if (header.SampleRate != 16000 && header.SampleRate != 8000)
            throw new InvalidAudioFormatException($"Sample rate {header.SampleRate} Hz is not supported. Use 16000 Hz or 8000 Hz.");
    }

    /// <summary>
    /// Checks if a byte array represents a valid WAV file
    /// </summary>
    public static bool IsWavFile(byte[] data)
    {
        if (data == null || data.Length < 12)
            return false;

        var chunkId = Encoding.ASCII.GetString(data, 0, 4);
        var format = Encoding.ASCII.GetString(data, 8, 4);

        return chunkId == "RIFF" && format == "WAVE";
    }

    /// <summary>
    /// Reads WAV file information
    /// </summary>
    public static WavInfo ReadWavInfo(byte[] data)
    {
        ValidateWavFormat(data);

        var header = ParseWavHeader(data);
        var audioData = ExtractAudioData(data);

        return new WavInfo
        {
            Channels = header.NumChannels,
            SampleWidth = header.BitsPerSample / 8,
            SampleRate = (int)header.SampleRate,
            FrameCount = (int)header.Subchunk2Size / (header.NumChannels * (header.BitsPerSample / 8)),
            AudioData = audioData
        };
    }

    /// <summary>
    /// Creates WAV formatted data from raw PCM data
    /// </summary>
    public static byte[] CreateWavData(byte[] pcmData, int sampleRate, ushort channels, ushort bitsPerSample)
    {
        var sampleWidth = bitsPerSample / 8;
        var byteRate = sampleRate * channels * sampleWidth;
        var blockAlign = (ushort)(channels * sampleWidth);
        var dataSize = (uint)pcmData.Length;
        var fileSize = 36 + dataSize; // 44 bytes total header, minus 8 bytes for RIFF header size field

        using var ms = new MemoryStream(44 + pcmData.Length);
        using var writer = new BinaryWriter(ms, Encoding.ASCII, true);

        // RIFF chunk
        writer.Write("RIFF"u8);
        writer.Write(fileSize);
        writer.Write("WAVE"u8);

        // fmt sub-chunk
        writer.Write("fmt "u8);
        writer.Write(16u); // Sub-chunk 1 size (16 for PCM)
        writer.Write((ushort)1); // Audio format (1 for PCM)
        writer.Write(channels);
        writer.Write((uint)sampleRate);
        writer.Write((uint)byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);

        // data sub-chunk
        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(pcmData);

        return ms.ToArray();
    }
}