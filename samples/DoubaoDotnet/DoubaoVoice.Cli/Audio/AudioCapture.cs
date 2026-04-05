namespace DoubaoVoice.Cli.Audio;

/// <summary>
/// Audio capture service for capturing real-time microphone input
/// </summary>
public class AudioCapture : IDisposable
{
    private readonly int _sampleRate;
    private readonly int _bitsPerSample;
    private readonly int _channels;
    private readonly int _segmentDurationMs;

    private object? _nativeCapture;
    private CancellationTokenSource? _captureCts;
    private bool _disposed;
    private Task? _captureTask;

    /// <summary>
    /// Event raised when audio data is captured
    /// </summary>
    public event EventHandler<AudioCapturedEventArgs>? OnAudioCaptured;

    /// <summary>
    /// Creates a new AudioCapture instance
    /// </summary>
    public AudioCapture(int sampleRate = 16000, int bitsPerSample = 16, int channels = 1, int segmentDurationMs = 200)
    {
        _sampleRate = sampleRate;
        _bitsPerSample = bitsPerSample;
        _channels = channels;
        _segmentDurationMs = segmentDurationMs;
    }

    /// <summary>
    /// Gets a list of available recording devices
    /// </summary>
    public static List<AudioDevice> GetAvailableDevices()
    {
        if (OperatingSystem.IsWindows())
        {
            return GetWindowsDevices();
        }
        else if (OperatingSystem.IsLinux())
        {
            return GetLinuxDevices();
        }
        else
        {
            // macOS - not yet implemented
            return new List<AudioDevice>();
        }
    }

    /// <summary>
    /// Checks if any recording device is available
    /// </summary>
    public static bool HasAvailableDevices()
    {
        return GetAvailableDevices().Count > 0;
    }

    /// <summary>
    /// Starts capturing audio from the default device
    /// </summary>
    public async Task StartCaptureAsync(int deviceIndex = 0, CancellationToken cancellationToken = default)
    {
        if (!HasAvailableDevices())
            throw new InvalidOperationException("No audio recording devices available");

        _captureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                await StartWindowsCaptureAsync(deviceIndex, _captureCts.Token);
            }
            else if (OperatingSystem.IsLinux())
            {
                await StartLinuxCaptureAsync(deviceIndex, _captureCts.Token);
            }
            else
            {
                throw new PlatformNotSupportedException("Audio capture is only supported on Windows and Linux");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("Failed to start audio capture", ex);
        }
    }

    /// <summary>
    /// Stops capturing audio
    /// </summary>
    public void StopCapture()
    {
        _captureCts?.Cancel();

        if (_nativeCapture is IDisposable disposable)
        {
            disposable.Dispose();
            _nativeCapture = null;
        }
    }

    private static List<AudioDevice> GetWindowsDevices()
    {
#if WINDOWS
        try
        {
            // NAudio Windows implementation
            var devices = new List<AudioDevice>();

            for (int i = 0; i < NAudio.Wave.WaveInEvent.DeviceCount; i++)
            {
                var caps = NAudio.Wave.WaveInEvent.GetCapabilities(i);
                devices.Add(new AudioDevice
                {
                    Index = i,
                    Name = caps.ProductName,
                    Channels = caps.Channels
                });
            }

            return devices;
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException)
        {
            // NAudio not available on this platform
            return new List<AudioDevice>();
        }
#else
        return new List<AudioDevice>();
#endif
    }

    private static List<AudioDevice> GetLinuxDevices()
    {
        try
        {
            // Use arecord command to list devices
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = "-c \"arecord -l 2>/dev/null || echo 'No devices found'\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
                return new List<AudioDevice>();

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var devices = new List<AudioDevice>();
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int index = 0;

            foreach (var line in lines)
            {
                if (line.Contains("card"))
                {
                    // Parse line like "card 0: PCH [HDA Intel PCH], device 0: ALC289 Analog [ALC289 Analog]"
                    var parts = line.Split(':');
                    if (parts.Length >= 2)
                    {
                        var name = parts[1].Split('[')[0].Trim();
                        devices.Add(new AudioDevice
                        {
                            Index = index++,
                            Name = name,
                            Channels = 1
                        });
                    }
                }
            }

            return devices;
        }
        catch
        {
            // arecord not available
            return new List<AudioDevice>();
        }
    }

    private async Task StartWindowsCaptureAsync(int deviceIndex, CancellationToken cancellationToken)
    {
#if WINDOWS
        var waveIn = new NAudio.Wave.WaveInEvent
        {
            DeviceNumber = deviceIndex,
            WaveFormat = new NAudio.Wave.WaveFormat(_sampleRate, _bitsPerSample, _channels)
        };

        waveIn.DataAvailable += (_, e) =>
        {
            if (OnAudioCaptured != null)
            {
                var audioData = new byte[e.BytesRecorded];
                Array.Copy(e.Buffer, audioData, e.BytesRecorded);
                OnAudioCaptured(this, new AudioCapturedEventArgs(audioData));
            }
        };

        _nativeCapture = waveIn;
        waveIn.StartRecording();
        _captureTask = Task.Delay(Timeout.Infinite, cancellationToken);
        await _captureTask;
#else
        throw new PlatformNotSupportedException("Windows audio capture is only available on Windows");
#endif
    }

    private async Task StartLinuxCaptureAsync(int deviceIndex, CancellationToken cancellationToken)
    {
        var segmentSize = (_sampleRate * (_bitsPerSample / 8) * _channels * _segmentDurationMs) / 1000;

        // Start arecord process
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "arecord",
            Arguments = $"-f S16_LE -r {_sampleRate} -c {_channels} -t raw --buffer-size={segmentSize * 2}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start arecord");

        _nativeCapture = process;

        var buffer = new byte[segmentSize];
        var readTask = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await process.StandardOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead <= 0)
                    break;

                var audioData = new byte[bytesRead];
                Array.Copy(buffer, audioData, bytesRead);

                OnAudioCaptured?.Invoke(this, new AudioCapturedEventArgs(audioData));
            }
        }, cancellationToken);

        _captureTask = readTask;
        await readTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _captureCts?.Cancel();
        _captureCts?.Dispose();

        if (_nativeCapture is IDisposable disposable)
        {
            disposable.Dispose();
            _nativeCapture = null;
        }
    }
}

/// <summary>
/// Represents an audio recording device
/// </summary>
public class AudioDevice
{
    /// <summary>
    /// Device index
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Device name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Number of channels supported
    /// </summary>
    public int Channels { get; set; }

    public override string ToString() => $"{Index}: {Name}";
}

/// <summary>
/// Event arguments for audio captured event
/// </summary>
public class AudioCapturedEventArgs : EventArgs
{
    /// <summary>
    /// Captured audio data
    /// </summary>
    public byte[] AudioData { get; set; }

    public AudioCapturedEventArgs(byte[] audioData)
    {
        AudioData = audioData;
    }
}