using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.Json;

namespace DoubaoVoice.SDK.Protocol;

/// <summary>
/// Decoded server response
/// </summary>
public class DecodedResponse
{
    /// <summary>
    /// Protocol header
    /// </summary>
    public ProtocolHeader Header { get; set; }

    /// <summary>
    /// Payload sequence number
    /// </summary>
    public int PayloadSequence { get; set; }

    /// <summary>
    /// Payload size
    /// </summary>
    public int PayloadSize { get; set; }

    /// <summary>
    /// Error code (for error responses)
    /// </summary>
    public int ErrorCode { get; set; }

    /// <summary>
    /// Event (if present)
    /// </summary>
    public int Event { get; set; }

    /// <summary>
    /// Whether this is the last package
    /// </summary>
    public bool IsLastPackage { get; set; }

    /// <summary>
    /// Audio info
    /// </summary>
    public AudioInfo? AudioInfo { get; set; }

    /// <summary>
    /// Recognition result
    /// </summary>
    public RecognitionResultInfo? Result { get; set; }

    /// <summary>
    /// Error message (if present)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether this response contains an error
    /// </summary>
    public bool IsError => Header.MessageType == Protocol.SERVER_ERROR_RESPONSE || !string.IsNullOrEmpty(ErrorMessage);
}

/// <summary>
/// Audio information from server response
/// </summary>
public class AudioInfo
{
    public int Duration { get; set; }
}

/// <summary>
/// Recognition result from server response
/// </summary>
public class RecognitionResultInfo
{
    public string Text { get; set; } = string.Empty;
    public List<UtteranceInfo>? Utterances { get; set; }
}

/// <summary>
/// Utterance information
/// </summary>
public class UtteranceInfo
{
    public string Text { get; set; } = string.Empty;
    public bool Definite { get; set; }
    public int EndTime { get; set; }
    public int StartTime { get; set; }
    public List<WordInfo>? Words { get; set; }
}

/// <summary>
/// Word information
/// </summary>
public class WordInfo
{
    public string Text { get; set; } = string.Empty;
    public int EndTime { get; set; }
    public int StartTime { get; set; }
}

/// <summary>
/// Payload decoder for DoubaoVoice protocol
/// </summary>
public static class PayloadDecoder
{
    /// <summary>
    /// Decodes a server response
    /// </summary>
    public static DecodedResponse DecodeResponse(byte[] data)
    {
        if (data == null || data.Length < 4)
            throw new ArgumentException("Response data must be at least 4 bytes", nameof(data));

        var result = new DecodedResponse
        {
            Header = HeaderDecoder.DecodeHeader(data)
        };

        var offset = result.Header.GetHeaderSizeBytes();
        var payload = data.AsSpan(offset);

        // Parse message type specific flags
        var flags = result.Header.MessageTypeSpecificFlags;

        // POS_SEQUENCE or NEG_WITH_SEQUENCE flag - payload sequence is present
        if ((flags & Protocol.POS_SEQUENCE) != 0)
        {
            result.PayloadSequence = BinaryPrimitives.ReadInt32BigEndian(payload);
            payload = payload.Slice(4);
        }

        // NEG_SEQUENCE flag - last package
        if ((flags & Protocol.NEG_SEQUENCE) != 0)
        {
            result.IsLastPackage = true;
        }

        // Event flag
        if ((flags & 0x04) != 0)
        {
            result.Event = BinaryPrimitives.ReadInt32BigEndian(payload);
            payload = payload.Slice(4);
        }

        // Parse message type
        switch (result.Header.MessageType)
        {
            case Protocol.SERVER_FULL_RESPONSE:
                result.PayloadSize = BinaryPrimitives.ReadInt32BigEndian(payload);
                payload = payload.Slice(4);
                break;

            case Protocol.SERVER_ERROR_RESPONSE:
                result.ErrorCode = BinaryPrimitives.ReadInt32BigEndian(payload);
                result.PayloadSize = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(4));
                payload = payload.Slice(8);
                break;
        }

        // Decompress if needed
        if (result.Header.CompressionType == Protocol.GZIP && payload.Length > 0)
        {
            payload = Decompress(payload);
        }

        // Parse JSON payload
        if (payload.Length > 0 && result.Header.SerializationType == Protocol.JSON)
        {
            ParseJsonPayload(result, payload.ToArray());
        }

        return result;
    }

    /// <summary>
    /// Parses JSON payload from the response
    /// </summary>
    private static void ParseJsonPayload(DecodedResponse result, byte[] payloadData)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(payloadData);
            var root = jsonDoc.RootElement;

            // Parse audio_info
            if (root.TryGetProperty("audio_info", out var audioInfo))
            {
                result.AudioInfo = new AudioInfo
                {
                    Duration = audioInfo.TryGetProperty("duration", out var duration) ? duration.GetInt32() : 0
                };
            }

            // Parse result
            if (root.TryGetProperty("result", out var resultProp))
            {
                result.Result = new RecognitionResultInfo
                {
                    Text = resultProp.TryGetProperty("text", out var text) ? text.GetString() ?? string.Empty : string.Empty
                };

                if (resultProp.TryGetProperty("utterances", out var utterances))
                {
                    result.Result.Utterances = new List<UtteranceInfo>();
                    foreach (var utt in utterances.EnumerateArray())
                    {
                        var utteranceInfo = new UtteranceInfo
                        {
                            Text = utt.TryGetProperty("text", out var uttText) ? uttText.GetString() ?? string.Empty : string.Empty,
                            Definite = utt.TryGetProperty("definite", out var definite) ? definite.GetBoolean() : false,
                            EndTime = utt.TryGetProperty("end_time", out var endTime) ? endTime.GetInt32() : 0,
                            StartTime = utt.TryGetProperty("start_time", out var startTime) ? startTime.GetInt32() : 0
                        };

                        if (utt.TryGetProperty("words", out var words))
                        {
                            utteranceInfo.Words = new List<WordInfo>();
                            foreach (var word in words.EnumerateArray())
                            {
                                utteranceInfo.Words.Add(new WordInfo
                                {
                                    Text = word.TryGetProperty("text", out var wordText) ? wordText.GetString() ?? string.Empty : string.Empty,
                                    EndTime = word.TryGetProperty("end_time", out var wordEndTime) ? wordEndTime.GetInt32() : 0,
                                    StartTime = word.TryGetProperty("start_time", out var wordStartTime) ? wordStartTime.GetInt32() : 0
                                });
                            }
                        }

                        result.Result.Utterances.Add(utteranceInfo);
                    }
                }
            }

            // Parse error
            if (root.TryGetProperty("error", out var errorProp))
            {
                result.ErrorMessage = errorProp.GetString();
            }
        }
        catch (JsonException)
        {
            // Ignore JSON parsing errors
        }
    }

    /// <summary>
    /// Decompresses GZIP data
    /// </summary>
    private static Span<byte> Decompress(Span<byte> compressed)
    {
        using var ms = new MemoryStream(compressed.ToArray());
        using var gzip = new GZipStream(ms, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        gzip.CopyTo(decompressed);
        return decompressed.ToArray();
    }
}