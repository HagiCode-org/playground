using KimiAcpSdk.Configuration;
using KimiAcpSdk.Protocol;
using KimiAcpSdk.Reporting;
using KimiAcpSdk.Transport;

namespace KimiAcpSdk.Runtime;

public sealed class KimiSessionRunner : IAsyncDisposable
{
    private readonly string _profileName;
    private readonly KimiLaunchProfile _profile;
    private readonly KimiRunRequest _request;
    private readonly RawTranscriptCapture _transcript = new();
    private readonly List<FeatureCheckResult> _features = [];
    private readonly RunArtifactWriter _artifactWriter = new();
    private KimiProcessRunner? _processRunner;
    private StdioAcpTransport? _transport;
    private KimiAcpClient? _client;
    private DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private KimiInitializeResult? _initialize;
    private KimiAuthenticationResult? _authentication;
    private KimiSessionStartResult? _session;
    private KimiPromptResult? _prompt;
    private string? _failureStage;
    private string? _failureMessage;
    private string? _promptText;

    public KimiSessionRunner(string profileName, KimiLaunchProfile profile, KimiRunRequest request)
    {
        _profileName = profileName;
        _profile = profile;
        _request = request;
    }

    public event Action<TranscriptEntry>? TranscriptObserved
    {
        add => _transcript.EntryRecorded += value;
        remove => _transcript.EntryRecorded -= value;
    }

    public string? SessionId => _session?.SessionId;

    public bool IsConnected => _session is not null;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        _startedAt = DateTimeOffset.UtcNow;
        _processRunner = await KimiProcessRunner.StartAsync(_profile, _transcript, cancellationToken);
        _transport = new StdioAcpTransport(_processRunner, _transcript);
        _transport.Start();
        _client = new KimiAcpClient(_transport, TimeSpan.FromSeconds(_profile.TimeoutSeconds));
        await _client.StartAsync(cancellationToken);

        _initialize = await CaptureFeatureAsync(
            "initialize",
            () => _client.InitializeAsync(_profile, cancellationToken),
            static result => result.AuthMethods.Count == 0
                ? "no auth methods advertised"
                : $"auth methods: {string.Join(", ", result.AuthMethods.Select(static method => method.Id))}");

        if (_initialize.AuthMethods.Count == 0)
        {
            ReplaceFeature(FeatureCheckResult.Skipped("authenticate", "initialize did not advertise auth methods"));
        }
        else
        {
            var methodId = _profile.Authentication.ResolveMethodId(_initialize.AuthMethods) ?? _initialize.AuthMethods.First().Id;
            _authentication = await CaptureFeatureAsync(
                "authenticate",
                () => _client.AuthenticateAsync(_profile.Authentication, methodId, cancellationToken),
                static result => result.Succeeded ? $"method={result.MethodId}" : $"method={result.MethodId} returned accepted=false");
        }

        _session = await CaptureFeatureAsync(
            "session/new",
            () => _client.CreateSessionAsync(_profile, cancellationToken),
            static result => $"sessionId={result.SessionId}");
    }

    public async Task<KimiPromptResult> PromptAsync(string promptText, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            await ConnectAsync(cancellationToken);
        }

        _promptText = promptText;
        _prompt = await CaptureFeatureAsync(
            "session/prompt",
            async () =>
            {
                var result = await _client!.PromptAsync(_session!.SessionId, promptText, cancellationToken);
                _prompt = result;
                if (string.IsNullOrWhiteSpace(result.FinalText))
                {
                    throw new KimiProtocolException("Prompt completed without usable text.");
                }

                return result;
            },
            static result => $"stopReason={result.StopReason ?? "unknown"}");

        return _prompt;
    }

    public KimiRunResult Snapshot()
    {
        if (_transcript.Snapshot().Count > 0)
        {
            ReplaceFeature(FeatureCheckResult.Passed("transcript", TimeSpan.Zero, $"entries={_transcript.Snapshot().Count}"));
        }
        else
        {
            ReplaceFeature(FeatureCheckResult.Failed("transcript", TimeSpan.Zero, "no transcript frames captured"));
        }

        return new KimiRunResult
        {
            ProfileName = _profileName,
            EffectiveLaunch = _profile.ToSummary(),
            StartedAt = _startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            Features = _features.OrderBy(static feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase).ToArray(),
            Transcript = _transcript.Snapshot(),
            PromptText = _promptText ?? _request.Prompt,
            Initialize = _initialize,
            Authentication = _authentication,
            Session = _session,
            Prompt = _prompt,
            FailureStage = _failureStage,
            FailureMessage = _failureMessage,
            StandardError = _processRunner?.GetStandardError(),
        };
    }

    public async Task<KimiRunResult> PersistSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = Snapshot();
        snapshot.Artifact = await _artifactWriter.WriteAsync(snapshot, cancellationToken);
        return snapshot;
    }

    public async ValueTask DisposeAsync()
    {
        if (_processRunner is not null)
        {
            await _processRunner.DisposeAsync();
        }

        if (_transport is not null)
        {
            await _transport.DisposeAsync();
        }

        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }

    private async Task<T> CaptureFeatureAsync<T>(string featureId, Func<Task<T>> callback, Func<T, string?> describeSuccess)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await callback();
            ReplaceFeature(FeatureCheckResult.Passed(featureId, stopwatch.Elapsed, describeSuccess(result)));
            return result;
        }
        catch (Exception exception)
        {
            _failureStage = featureId;
            _failureMessage = exception.Message;
            ReplaceFeature(FeatureCheckResult.Failed(featureId, stopwatch.Elapsed, exception.Message));
            throw;
        }
    }

    private void ReplaceFeature(FeatureCheckResult feature)
    {
        _features.RemoveAll(existing => string.Equals(existing.FeatureId, feature.FeatureId, StringComparison.OrdinalIgnoreCase));
        _features.Add(feature);
    }
}
