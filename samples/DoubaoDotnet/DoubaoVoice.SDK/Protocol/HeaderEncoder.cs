namespace DoubaoVoice.SDK.Protocol;

/// <summary>
/// Header encoder for DoubaoVoice protocol
/// </summary>
public static class HeaderEncoder
{
    /// <summary>
    /// Encodes a protocol header to a byte array
    /// </summary>
    public static byte[] EncodeHeader(ProtocolHeader header)
    {
        var result = new byte[4];

        // Byte 0: protocol version (high 4 bits) + header size in 4-byte units (low 4 bits)
        // Header is always 4 bytes, so size in units is 1
        result[0] = (byte)((header.ProtocolVersion << 4) | 1);
        result[1] = header.MessageTypeAndFlags;
        result[2] = header.SerializationAndCompression;

        // Reserved byte (always 0x00)
        result[3] = 0x00;

        return result;
    }

    /// <summary>
    /// Creates and encodes a protocol header in one step
    /// </summary>
    public static byte[] EncodeFullClientRequest()
    {
        var header = ProtocolHeader.CreateFullClientRequest();
        return EncodeHeader(header);
    }

    /// <summary>
    /// Creates and encodes an audio-only request header
    /// </summary>
    public static byte[] EncodeAudioOnlyRequest(bool isLastSegment)
    {
        var header = ProtocolHeader.CreateAudioOnlyRequest(isLastSegment);
        return EncodeHeader(header);
    }
}