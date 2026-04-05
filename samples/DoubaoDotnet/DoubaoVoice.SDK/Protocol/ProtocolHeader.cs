namespace DoubaoVoice.SDK.Protocol;

/// <summary>
/// Protocol constants and enums
/// </summary>
public static class Protocol
{
    // Protocol version
    public const byte PROTOCOL_VERSION = 0b0001;

    // Message Types
    public const byte CLIENT_FULL_REQUEST = 0b0001;
    public const byte CLIENT_AUDIO_ONLY_REQUEST = 0b0010;
    public const byte SERVER_FULL_RESPONSE = 0b1001;
    public const byte SERVER_ERROR_RESPONSE = 0b1111;

    // Message Type Specific Flags
    public const byte NO_SEQUENCE = 0b0000;
    public const byte POS_SEQUENCE = 0b0001;
    public const byte NEG_SEQUENCE = 0b0010;
    public const byte NEG_WITH_SEQUENCE = 0b0011;

    // Serialization Types
    public const byte NO_SERIALIZATION = 0b0000;
    public const byte JSON = 0b0001;

    // Compression Types
    public const byte NO_COMPRESSION = 0b0000;
    public const byte GZIP = 0b0001;

    // Default sample rate
    public const int DEFAULT_SAMPLE_RATE = 16000;
}

/// <summary>
/// Protocol header structure
/// </summary>
public struct ProtocolHeader
{
    /// <summary>
    /// Protocol version (4 bits)
    /// </summary>
    public byte ProtocolVersion;

    /// <summary>
    /// Message type (4 bits high nibble) + Message type specific flags (4 bits low nibble)
    /// </summary>
    public byte MessageTypeAndFlags;

    /// <summary>
    /// Serialization type (4 bits high nibble) + Compression type (4 bits low nibble)
    /// </summary>
    public byte SerializationAndCompression;

    /// <summary>
    /// Gets the message type from MessageTypeAndFlags
    /// </summary>
    public byte MessageType => (byte)((MessageTypeAndFlags >> 4) & 0x0F);

    /// <summary>
    /// Gets the message type specific flags from MessageTypeAndFlags
    /// </summary>
    public byte MessageTypeSpecificFlags => (byte)(MessageTypeAndFlags & 0x0F);

    /// <summary>
    /// Gets the serialization type from SerializationAndCompression
    /// </summary>
    public byte SerializationType => (byte)((SerializationAndCompression >> 4) & 0x0F);

    /// <summary>
    /// Gets the compression type from SerializationAndCompression
    /// </summary>
    public byte CompressionType => (byte)(SerializationAndCompression & 0x0F);

    /// <summary>
    /// Creates a protocol header
    /// </summary>
    public static ProtocolHeader Create(
        byte protocolVersion,
        byte messageType,
        byte messageTypeSpecificFlags,
        byte serializationType,
        byte compressionType)
    {
        return new ProtocolHeader
        {
            ProtocolVersion = protocolVersion,
            MessageTypeAndFlags = (byte)((messageType << 4) | (messageTypeSpecificFlags & 0x0F)),
            SerializationAndCompression = (byte)((serializationType << 4) | (compressionType & 0x0F))
        };
    }

    /// <summary>
    /// Creates a protocol header for a full client request
    /// </summary>
    public static ProtocolHeader CreateFullClientRequest()
    {
        return Create(
            Protocol.PROTOCOL_VERSION,
            Protocol.CLIENT_FULL_REQUEST,
            Protocol.POS_SEQUENCE,
            Protocol.JSON,
            Protocol.GZIP);
    }

    /// <summary>
    /// Creates a protocol header for an audio-only request
    /// </summary>
    public static ProtocolHeader CreateAudioOnlyRequest(bool isLastSegment)
    {
        return Create(
            Protocol.PROTOCOL_VERSION,
            Protocol.CLIENT_AUDIO_ONLY_REQUEST,
            isLastSegment ? Protocol.NEG_WITH_SEQUENCE : Protocol.POS_SEQUENCE,
            Protocol.NO_SERIALIZATION,
            Protocol.GZIP);
    }

    /// <summary>
    /// Gets the actual header size in bytes (always 4)
    /// </summary>
    public int GetHeaderSizeBytes() => 4;
}