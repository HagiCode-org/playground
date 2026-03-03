namespace DoubaoVoice.WebProxy.Handlers;

/// <summary>
/// Interface for message protocol serialization
/// </summary>
public interface IMessageProtocol
{
    /// <summary>
    /// Serializes a message to bytes
    /// </summary>
    byte[] Serialize<T>(T message) where T : class;

    /// <summary>
    /// Deserializes bytes to a message
    /// </summary>
    T? Deserialize<T>(byte[] data) where T : class;

    /// <summary>
    /// Parses a message and determines its type
    /// </summary>
    Models.MessageType? ParseMessageType(byte[] data);

    /// <summary>
    /// Serializes audio data directly (binary format)
    /// </summary>
    byte[] SerializeAudio(byte[] audioData, bool isLastSegment);

    /// <summary>
    /// Deserializes audio data from binary format
    /// </summary>
    (byte[] data, bool isLastSegment) DeserializeAudio(byte[] data);
}
