namespace DoubaoVoice.SDK.Protocol;

/// <summary>
/// Header decoder for DoubaoVoice protocol
/// </summary>
public static class HeaderDecoder
{
    /// <summary>
    /// Decodes a protocol header from a byte array
    /// </summary>
    public static ProtocolHeader DecodeHeader(byte[] data)
    {
        if (data == null || data.Length < 4)
            throw new ArgumentException("Header data must be at least 4 bytes", nameof(data));

        var result = new ProtocolHeader
        {
            // Byte 0: protocol version (high 4 bits)
            ProtocolVersion = (byte)((data[0] >> 4) & 0x0F),

            // Byte 1: message type (high 4 bits) + message type specific flags (low 4 bits)
            MessageTypeAndFlags = data[1],

            // Byte 2: serialization type (high 4 bits) + compression type (low 4 bits)
            SerializationAndCompression = data[2]
        };

        return result;
    }

    /// <summary>
    /// Decodes a protocol header from a byte array at a specific offset
    /// </summary>
    public static ProtocolHeader DecodeHeader(byte[] data, int offset)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (offset < 0 || offset + 4 > data.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset is out of range");

        var headerData = new byte[4];
        Array.Copy(data, offset, headerData, 0, 4);
        return DecodeHeader(headerData);
    }

    /// <summary>
    /// Gets the payload start position in the data (always 4 bytes for header)
    /// </summary>
    public static int GetPayloadStart(ProtocolHeader header) => 4;
}