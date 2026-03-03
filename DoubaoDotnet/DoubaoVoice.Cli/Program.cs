using DoubaoVoice.Cli.Audio;
using DoubaoVoice.SDK;

namespace DoubaoVoice.Cli;

class Program
{
    private static readonly CancellationTokenSource _cts = new();

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Handle Ctrl+C
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
            Console.WriteLine("\n\nShutting down...");
        };

        // Parse command line arguments
        var config = ParseArguments(args);
        if (config == null)
        {
            PrintUsage();
            Environment.Exit(1);
        }

        try
        {
            // Check audio devices
            if (!AudioCapture.HasAvailableDevices())
            {
                PrintError("No audio recording devices available. Please connect a microphone and try again.");
                Environment.Exit(1);
            }

            PrintInfo($"Using device: {AudioCapture.GetAvailableDevices()[0].Name}");

            // Create client and start recognition
            using var client = new DoubaoVoiceClient(config);
            var audioCapture = new AudioCapture();

            // Subscribe to events
            SubscribeToEvents(client);

            Console.WriteLine("\nListening... Press Ctrl+C to stop.\n");

            // Start capturing and recognizing
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            await StartRecognition(client, audioCapture, cts.Token);
        }
        catch (AuthenticationException ex)
        {
            PrintError($"Authentication failed: {ex.Message}");
            Environment.Exit(1);
        }
        catch (ConnectionException ex)
        {
            PrintError($"Connection failed: {ex.Message}");
            Environment.Exit(1);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nRecognition cancelled by user.");
        }
        catch (Exception ex)
        {
            PrintError($"An error occurred: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static DoubaoVoiceConfig? ParseArguments(string[] args)
    {
        if (args.Length < 2)
            return null;

        var config = new DoubaoVoiceConfig
        {
            AppId = args[0],
            AccessToken = args[1],
            Did = Guid.NewGuid().ToString("N") // Generate a unique device ID
        };

        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--url":
                    if (i + 1 < args.Length)
                        config.ServiceUrl = args[++i];
                    break;
                case "--sample-rate":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var sampleRate))
                        config.SampleRate = sampleRate;
                    break;
                case "--model":
                    if (i + 1 < args.Length)
                        config.ModelName = args[++i];
                    break;
            }
        }

        return config;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("DoubaoVoice CLI - Real-time Speech Recognition");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  DoubaoVoice.Cli <AppId> <AccessToken> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --url <url>              Service URL (default: wss://openspeech.bytedance.com/api/v2/asr)");
        Console.WriteLine("  --sample-rate <rate>     Sample rate in Hz (default: 16000)");
        Console.WriteLine("  --model <name>           Model name (default: bigmodel)");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  DoubaoVoice.Cli your_app_id your_access_token");
    }

    private static void SubscribeToEvents(DoubaoVoiceClient client)
    {
        client.OnConnected += (_, _) =>
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[Connected] Connected to DoubaoVoice service");
            Console.ResetColor();
        };

        client.OnDisconnected += (_, e) =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Disconnected] {e.Reason}");
            Console.ResetColor();
        };

        client.OnError += (_, e) =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] {e.ErrorMessage}");
            if (e.Exception != null)
                Console.WriteLine($"  Exception: {e.Exception.Message}");
            Console.ResetColor();
        };

        client.OnResultReceived += (_, e) =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.Write($"[{timestamp}] ");

            if (!string.IsNullOrEmpty(e.Result.Text))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(e.Result.Text);
                Console.ResetColor();
            }
        };

        client.OnRecognitionCompleted += (_, e) =>
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[Completed] Recognition finished");
            Console.WriteLine($"  Final text: {e.Result.Text}");
            Console.WriteLine($"  Total segments: {e.TotalSegments}");
            Console.ResetColor();
        };
    }

    private static async Task StartRecognition(
        DoubaoVoiceClient client,
        AudioCapture audioCapture,
        CancellationToken cancellationToken)
    {
        // Buffer for accumulating audio data
        var audioBuffer = new List<byte>();
        var lastSendTime = DateTime.MinValue;
        const int segmentDurationMs = 200;
        var segmentSize = (16000 * 2 * 1 * segmentDurationMs) / 1000; // 16kHz, 16-bit, mono

        // Subscribe to audio capture
        audioCapture.OnAudioCaptured += (_, e) =>
        {
            lock (audioBuffer)
            {
                audioBuffer.AddRange(e.AudioData);

                // Check if we have enough data for a segment
                if (audioBuffer.Count >= segmentSize &&
                    (DateTime.UtcNow - lastSendTime).TotalMilliseconds >= segmentDurationMs)
                {
                    var segment = audioBuffer.Take(segmentSize).ToArray();
                    audioBuffer.RemoveRange(0, Math.Min(segmentSize, audioBuffer.Count));
                    lastSendTime = DateTime.UtcNow;

                    // Send to client
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await client.SendAudioSegment(segment, false, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Failed to send audio segment: {ex.Message}");
                        }
                    }, cancellationToken);
                }
            }
        };

        // Connect and start recognition
        await client.ConnectAsync(cancellationToken);
        await client.SendFullClientRequest(cancellationToken);

        // Give the server a moment to process the initial request
        await Task.Delay(500);

        // Start receiving messages
        var receiveTask = Task.Run(() => client.ReceiveMessagesAsync(cancellationToken), cancellationToken);

        try
        {
            await audioCapture.StartCaptureAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            // Send final segment marker
            try
            {
                await client.SendAudioSegment(Array.Empty<byte>(), true, cancellationToken);
            }
            catch { }

            await receiveTask;
            await client.DisconnectAsync();
        }
    }

    private static void PrintInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }
}