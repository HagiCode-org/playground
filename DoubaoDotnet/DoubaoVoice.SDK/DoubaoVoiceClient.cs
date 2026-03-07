using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DoubaoVoice.SDK.Audio;
using DoubaoVoice.SDK.Protocol;

namespace DoubaoVoice.SDK;

/// <summary>
/// Main client class for DoubaoVoice SDK
/// </summary>
public class DoubaoVoiceClient : IDisposable
{
    private readonly DoubaoVoiceConfig _config;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private int _sequence = 2;
    private readonly object _sequenceLock = new();
    private bool _disposed;

    // Events
    public event EventHandler<ConnectedEventArgs>? OnConnected;
    public event EventHandler<DisconnectedEventArgs>? OnDisconnected;
    public event EventHandler<ErrorEventArgs>? OnError;
    public event EventHandler<ResultReceivedEventArgs>? OnResultReceived;
    public event EventHandler<RecognitionCompletedEventArgs>? OnRecognitionCompleted;

    /// <summary>
    /// Creates a new DoubaoVoice client
    /// </summary>
    public DoubaoVoiceClient(DoubaoVoiceConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _config.Validate();
    }

    /// <summary>
    /// Gets the current connection state
    /// </summary>
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    /// <summary>
    /// Establishes a WebSocket connection to the DoubaoVoice service
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            return;

        try
        {
            _webSocket = new ClientWebSocket();

            // Set required HTTP headers
            _webSocket.Options.SetRequestHeader("X-Api-App-Key", _config.AppId);
            _webSocket.Options.SetRequestHeader("X-Api-Access-Key", _config.AccessToken);
            _webSocket.Options.SetRequestHeader("X-Api-Resource-Id", _config.ResourceId);
            _webSocket.Options.SetRequestHeader("X-Api-Connect-Id", GenerateConnectId());

            await _webSocket.ConnectAsync(new Uri(_config.ServiceUrl), cancellationToken);

            NotifyConnected();
        }
        catch (WebSocketException ex)
        {
            NotifyError(new ErrorEventArgs
            {
                ErrorMessage = "WebSocket connection failed",
                Exception = ex,
                IsAuthenticationError = IsAuthError(ex)
            });

            throw new AuthenticationException("Failed to connect to DoubaoVoice service", ex);
        }
        catch (Exception ex)
        {
            NotifyError(new ErrorEventArgs
            {
                ErrorMessage = "Connection failed",
                Exception = ex
            });

            throw new ConnectionException("Failed to connect to DoubaoVoice service", ex);
        }
    }

    /// <summary>
    /// Sends the full client request (metadata)
    /// </summary>
    public async Task SendFullClientRequest(CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        try
        {
            var payload = BuildFullClientRequestPayload();
            // Full client request always uses sequence 1
            var encoded = PayloadEncoder.EncodeFullClientRequest(payload, 1);

            Console.WriteLine($"[DEBUG] Sending full client request ({encoded.Length} bytes)");
            Console.WriteLine($"[DEBUG] Payload JSON: {payload}");
            Console.WriteLine($"[DEBUG] First 32 bytes: {BitConverter.ToString(encoded.Take(Math.Min(32, encoded.Length)).ToArray())}");

            await _webSocket!.SendAsync(
                new ArraySegment<byte>(encoded),
                WebSocketMessageType.Binary,
                true,
                cancellationToken);

            Console.WriteLine($"[DEBUG] Send successful");

            // Note: Server might not send an immediate response
            // The actual response will come after audio data is sent
        }
        catch (WebSocketException ex)
        {
            var innerMsg = ex.InnerException?.Message ?? "no inner exception";
            var errorMsg = $"Failed to send full client request: {ex.Message} (Inner: {innerMsg})";
            NotifyError(new ErrorEventArgs { ErrorMessage = errorMsg, Exception = ex });
            throw new ConnectionException(errorMsg, ex);
        }
    }

    /// <summary>
    /// Sends an audio segment
    /// </summary>
    public async Task SendAudioSegment(byte[] audioData, bool isLastSegment, CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        try
        {
            var encoded = PayloadEncoder.EncodeAudioSegment(audioData, GetNextSequence(), isLastSegment);

            await _webSocket!.SendAsync(
                new ArraySegment<byte>(encoded),
                WebSocketMessageType.Binary,
                true,
                cancellationToken);
        }
        catch (WebSocketException ex)
        {
            NotifyError(new ErrorEventArgs { ErrorMessage = "Failed to send audio segment", Exception = ex });
            throw;
        }
    }

    /// <summary>
    /// Starts receiving messages from the server
    /// </summary>
    public async Task ReceiveMessagesAsync(CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var buffer = new byte[8192];
        var completeResult = new RecognitionResult();

        try
        {
            Console.WriteLine($"[DEBUG] ReceiveMessagesAsync started, WebSocket state: {_webSocket?.State}");

            while (!_receiveCts.Token.IsCancellationRequested && _webSocket!.State == WebSocketState.Open)
            {
                Console.WriteLine($"[DEBUG] Waiting for data...");

                WebSocketReceiveResult result;
                try
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _receiveCts.Token);

                    Console.WriteLine($"[DEBUG] Received {result.Count} bytes, type: {result.MessageType}, end: {result.EndOfMessage}");

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        NotifyDisconnected(new DisconnectedEventArgs { Reason = $"Server closed connection. Status: {result.CloseStatus}, Reason: {result.CloseStatusDescription}" });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] ReceiveAsync exception: {ex.Message}");
                    throw;
                }

                var receivedData = buffer.Take(result.Count).ToArray();

                // Debug: Print received raw data
                Console.WriteLine($"[DEBUG] Received raw data ({receivedData.Length} bytes): {BitConverter.ToString(receivedData.Take(Math.Min(64, receivedData.Length)).ToArray())}");

                try
                {
                    var decoded = PayloadDecoder.DecodeResponse(receivedData);

                    Console.WriteLine($"[DEBUG] Decoded response:");
                    Console.WriteLine($"[DEBUG]   Header: {decoded.Header.MessageTypeSpecificFlags:X2}");
                    Console.WriteLine($"[DEBUG]   PayloadSequence: {decoded.PayloadSequence}");
                    Console.WriteLine($"[DEBUG]   IsLastPackage: {decoded.IsLastPackage}");
                    Console.WriteLine($"[DEBUG]   Event: {decoded.Event}");
                    Console.WriteLine($"[DEBUG]   IsError: {decoded.IsError}");

                    if (decoded.IsError)
                    {
                        Console.WriteLine($"[DEBUG]   ErrorCode: {decoded.ErrorCode}");
                        Console.WriteLine($"[DEBUG]   ErrorMessage: {decoded.ErrorMessage}");
                        NotifyError(new ErrorEventArgs
                        {
                            ErrorMessage = decoded.ErrorMessage ?? "Unknown error from server",
                            ErrorCode = decoded.ErrorCode
                        });
                        continue;
                    }

                    if (decoded.Result != null)
                    {
                        // Update complete result
                        if (!string.IsNullOrEmpty(decoded.Result.Text))
                        {
                            completeResult.Text = decoded.Result.Text;
                        }
                        if (decoded.AudioInfo != null)
                        {
                            completeResult.AudioDuration = decoded.AudioInfo.Duration;
                        }
                        if (decoded.Result.Utterances != null)
                        {
                            completeResult.Utterances.Clear();
                            foreach (var utt in decoded.Result.Utterances)
                            {
                                completeResult.Utterances.Add(new RecognitionUtterance
                                {
                                    Text = utt.Text,
                                    Definite = utt.Definite,
                                    EndTime = utt.EndTime,
                                    StartTime = utt.StartTime,
                                    Words = utt.Words?.Select(w => new RecognitionWord
                                    {
                                        Text = w.Text,
                                        EndTime = w.EndTime,
                                        StartTime = w.StartTime
                                    }).ToList() ?? new List<RecognitionWord>()
                                });
                            }
                        }

                        NotifyResultReceived(new ResultReceivedEventArgs
                        {
                            Result = new RecognitionResult
                            {
                                Text = decoded.Result.Text,
                                AudioDuration = decoded.AudioInfo?.Duration ?? completeResult.AudioDuration,
                                Utterances = completeResult.Utterances.ToList()
                            },
                            IsFinal = decoded.IsLastPackage
                        });
                    }

                    if (decoded.IsLastPackage)
                    {
                        NotifyRecognitionCompleted(new RecognitionCompletedEventArgs
                        {
                            Result = completeResult,
                            TotalSegments = Math.Abs(decoded.PayloadSequence)
                        });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    NotifyError(new ErrorEventArgs
                    {
                        ErrorMessage = "Failed to decode server response",
                        Exception = ex
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"[DEBUG] WebSocketException in ReceiveMessagesAsync:");
            Console.WriteLine($"[DEBUG]   Message: {ex.Message}");
            Console.WriteLine($"[DEBUG]   Inner: {ex.InnerException?.Message ?? "none"}");
            Console.WriteLine($"[DEBUG]   State: {_webSocket?.State}");
            Console.WriteLine($"[DEBUG]   ErrorCode: {ex.WebSocketErrorCode}");
            NotifyError(new ErrorEventArgs { ErrorMessage = "WebSocket error while receiving messages", Exception = ex });
        }
        finally
        {
            _receiveCts?.Dispose();
            _receiveCts = null;
        }
    }

    /// <summary>
    /// Disconnects from the server immediately without waiting for close handshake
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_webSocket == null)
            return;

        try
        {
            _receiveCts?.Cancel();
            // Use AbortAsync for immediate disconnect without waiting for server response
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None).ConfigureAwait(false);
            NotifyDisconnected(new DisconnectedEventArgs { Reason = "Client disconnected" });
        }
        catch (Exception ex)
        {
            // Ignore disconnect errors
        }
        finally
        {
            _webSocket?.Dispose();
            _webSocket = null;
            _receiveCts?.Dispose();
            _receiveCts = null;
        }
    }

    /// <summary>
    /// Aborts the connection immediately without any handshake
    /// </summary>
    public void Abort()
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _receiveCts = null;

        try
        {
            _webSocket?.Dispose();
        }
        catch { }
        finally
        {
            _webSocket = null;
        }
    }

    /// <summary>
    /// Starts speech recognition with audio data
    /// </summary>
    public async Task RecognizeAudioAsync(byte[] audioData, int segmentDurationMs = 200, CancellationToken cancellationToken = default)
    {
        await ConnectAsync(cancellationToken);
        await SendFullClientRequest(cancellationToken);

        var segments = AudioSegmenter.SegmentAudio(
            audioData,
            _config.SampleRate,
            _config.Channels,
            _config.BitsPerSample,
            segmentDurationMs);

        // Start receiving messages in background
        var receiveTask = ReceiveMessagesAsync(cancellationToken);

        // Send audio segments
        for (int i = 0; i < segments.Count; i++)
        {
            var isLast = i == segments.Count - 1;
            await SendAudioSegment(segments[i], isLast, cancellationToken);

            // Wait to simulate real-time streaming
            if (!isLast)
                await Task.Delay(segmentDurationMs, cancellationToken);
        }

        await receiveTask;
        await DisconnectAsync();
    }

    /// <summary>
    /// Starts real-time speech recognition with a stream of audio data
    /// </summary>
    public async Task RecognizeStreamAsync(Func<Task<byte[]?>> audioStream, int segmentDurationMs = 200, CancellationToken cancellationToken = default)
    {
        await ConnectAsync(cancellationToken);
        await SendFullClientRequest(cancellationToken);

        // Start receiving messages in background
        var receiveTask = ReceiveMessagesAsync(cancellationToken);

        try
        {
            int segmentIndex = 0;
            while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                var audioData = await audioStream();
                if (audioData == null || audioData.Length == 0)
                    break;

                segmentIndex++;
                await SendAudioSegment(audioData, false, cancellationToken);
                await Task.Delay(segmentDurationMs, cancellationToken);
            }

            // Send final segment marker
            await SendAudioSegment(Array.Empty<byte>(), true, cancellationToken);
        }
        finally
        {
            await receiveTask;
            await DisconnectAsync();
        }
    }

    private void EnsureConnected()
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            throw new InvalidOperationException("Not connected to the server");
    }

    private int GetNextSequence()
    {
        lock (_sequenceLock)
        {
            return _sequence++;
        }
    }

    private void ResetSequence()
    {
        lock (_sequenceLock)
        {
            _sequence = 1;
        }
    }

    private static string GenerateConnectId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static bool IsAuthError(WebSocketException ex)
    {
        // Simple check - in real implementation might check HTTP status code
        return ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely;
    }

    private string BuildFullClientRequestPayload()
    {
        // Build base request object
        var requestObj = new
        {
            user = new
            {
                uid = _config.Uid,
                did = _config.Did,
                platform = _config.Platform,
                sdk_version = _config.SdkVersion,
                app_version = _config.AppVersion
            },
            audio = new
            {
                format = _config.AudioFormat,
                codec = _config.AudioCodec,
                rate = _config.SampleRate,
                bits = _config.BitsPerSample,
                channel = _config.Channels
            },
            request = new
            {
                model_name = _config.ModelName,
                enable_itn = _config.EnableITN,
                enable_punc = _config.EnablePunctuation,
                enable_ddc = _config.EnableDDC,
                show_utterances = _config.ShowUtterances,
                enable_nonstream = _config.EnableNonstream,
                end_window_size = _config.EndWindowSize
            }
        };

        // Use LINQ to build the final object dynamically based on whether HotwordContexts is configured
        var requestJson = System.Text.Json.JsonSerializer.Serialize(requestObj, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        // Parse the JSON and add corpus.context if hotword contexts are configured
        using var document = System.Text.Json.JsonDocument.Parse(requestJson);
        var root = document.RootElement;

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();

        // Write user
        writer.WritePropertyName("user");
        writer.WriteStartObject();
        if (root.TryGetProperty("user", out var user))
        {
            foreach (var prop in user.EnumerateObject())
            {
                writer.WritePropertyName(prop.Name);
                prop.Value.WriteTo(writer);
            }
        }
        writer.WriteEndObject();

        // Write audio
        writer.WritePropertyName("audio");
        writer.WriteStartObject();
        if (root.TryGetProperty("audio", out var audio))
        {
            foreach (var prop in audio.EnumerateObject())
            {
                writer.WritePropertyName(prop.Name);
                prop.Value.WriteTo(writer);
            }
        }
        writer.WriteEndObject();

        // Write request
        writer.WritePropertyName("request");
        writer.WriteStartObject();
        if (root.TryGetProperty("request", out var req))
        {
            foreach (var prop in req.EnumerateObject())
            {
                writer.WritePropertyName(prop.Name);
                prop.Value.WriteTo(writer);
            }
        }

        // Add corpus if any corpus-related properties are configured
        bool hasCorpus = (_config.HotwordContexts != null && _config.HotwordContexts.Count > 0)
            || !string.IsNullOrEmpty(_config.BoostingTableId);

        if (hasCorpus)
        {
            writer.WritePropertyName("corpus");
            writer.WriteStartObject();

            // Add context if HotwordContexts is configured
            if (_config.HotwordContexts != null && _config.HotwordContexts.Count > 0)
            {
                var contextData = _config.HotwordContexts.Select(h => new { text = h }).ToList();
                var contextObj = new
                {
                    context_type = "dialog_ctx",
                    context_data = contextData
                };
                var contextJson = System.Text.Json.JsonSerializer.Serialize(contextObj);
                writer.WritePropertyName("context");
                writer.WriteStringValue(contextJson);
            }

            // Add boosting table ID
            if (!string.IsNullOrEmpty(_config.BoostingTableId))
            {
                writer.WritePropertyName("boosting_table_id");
                writer.WriteStringValue(_config.BoostingTableId);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject(); // end request
        writer.WriteEndObject();

        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    // Event notification methods
    private void NotifyConnected()
    {
        OnConnected?.Invoke(this, new ConnectedEventArgs());
    }

    private void NotifyDisconnected(DisconnectedEventArgs args)
    {
        OnDisconnected?.Invoke(this, args);
    }

    private void NotifyError(ErrorEventArgs args)
    {
        OnError?.Invoke(this, args);
    }

    private void NotifyResultReceived(ResultReceivedEventArgs args)
    {
        OnResultReceived?.Invoke(this, args);
    }

    private void NotifyRecognitionCompleted(RecognitionCompletedEventArgs args)
    {
        OnRecognitionCompleted?.Invoke(this, args);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _webSocket?.Dispose();
    }
}
