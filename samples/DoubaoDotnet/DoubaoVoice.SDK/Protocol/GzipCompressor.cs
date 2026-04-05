using System.IO.Compression;

namespace DoubaoVoice.SDK.Protocol;

/// <summary>
/// GZIP compression utility
/// </summary>
public static class GzipCompressor
{
    /// <summary>
    /// Compresses a byte array using GZIP
    /// </summary>
    public static byte[] Compress(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionMode.Compress, true))
        {
            gzip.Write(data, 0, data.Length);
        } // gzip is disposed here, which flushes all data
        return ms.ToArray();
    }

    /// <summary>
    /// Compresses a string using GZIP (UTF-8 encoding)
    /// </summary>
    public static byte[] Compress(string text)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(text);
        return Compress(data);
    }

    /// <summary>
    /// Decompresses a GZIP byte array
    /// </summary>
    public static byte[] Decompress(byte[] compressedData)
    {
        using var ms = new MemoryStream(compressedData);
        using var gzip = new GZipStream(ms, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        gzip.CopyTo(decompressed);
        return decompressed.ToArray();
    }
}