using CopilotSdk.Auth;
using CopilotSdk.Configuration;
using CopilotSdk.Models;
using CopilotSdk.Processing;

namespace CopilotSdk.Client;

public sealed class CopilotClientAdapter
{
    private readonly CopilotPlaygroundSettings _settings;
    private readonly CopilotAuthManager _authManager;
    private readonly ICopilotSessionGateway _gateway;
    private readonly CopilotResponseProcessor _processor;
    private readonly Func<DateTimeOffset> _clock;

    public CopilotClientAdapter(
        CopilotPlaygroundSettings settings,
        CopilotAuthManager authManager,
        ICopilotSessionGateway gateway,
        CopilotResponseProcessor processor,
        Func<DateTimeOffset>? clock = null)
    {
        _settings = settings;
        _authManager = authManager;
        _gateway = gateway;
        _processor = processor;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<(CopilotNormalizedResponse Response, AuthDiagnostic? AuthDiagnostic)> ExecuteAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        var correlationId = RequestIdGenerator.NewId(_clock());
        var request = new CopilotPromptRequest(
            CorrelationId: correlationId,
            Model: _settings.Model,
            Prompt: prompt,
            Timeout: TimeSpan.FromSeconds(_settings.TimeoutSeconds),
            Streaming: true);

        var auth = await _authManager.EnsureValidCredentialAsync(correlationId, cancellationToken);
        if (!auth.Success)
        {
            var diagnostic = auth.Diagnostic ?? new AuthDiagnostic(AuthFailureCategory.Unknown, "Unknown auth failure.", correlationId, _clock());
            var ex = new UnauthorizedAccessException(diagnostic.Message);
            var failed = _processor.NormalizeFailure(request, ex, retriedAfterRefresh: false, TimeSpan.Zero);
            return (failed, diagnostic);
        }

        var retriedAfterRefresh = false;
        var startedAt = _clock();

        try
        {
            var gatewayResponse = await _gateway.SendPromptAsync(
                new CopilotGatewayRequest(
                    CorrelationId: request.CorrelationId,
                    Model: request.Model,
                    Prompt: request.Prompt,
                    Timeout: request.Timeout,
                    CliPath: _settings.CliPath,
                    CliUrl: _settings.CliUrl,
                    WorkingDirectory: _settings.WorkingDirectory,
                    Credential: auth.Credential!,
                    Streaming: request.Streaming),
                cancellationToken);

            var success = _processor.NormalizeSuccess(request, gatewayResponse, retriedAfterRefresh);
            return (success, null);
        }
        catch (Exception ex)
        {
            var category = _processor.ClassifyError(ex);
            if (category == CopilotErrorCategory.Authentication)
            {
                var refreshed = await _authManager.RefreshCredentialAsync(correlationId, cancellationToken);
                if (refreshed.Success)
                {
                    retriedAfterRefresh = true;
                    try
                    {
                        var retried = await _gateway.SendPromptAsync(
                            new CopilotGatewayRequest(
                                CorrelationId: request.CorrelationId,
                                Model: request.Model,
                                Prompt: request.Prompt,
                                Timeout: request.Timeout,
                                CliPath: _settings.CliPath,
                                CliUrl: _settings.CliUrl,
                                WorkingDirectory: _settings.WorkingDirectory,
                                Credential: refreshed.Credential!,
                                Streaming: request.Streaming),
                            cancellationToken);

                        var successAfterRetry = _processor.NormalizeSuccess(request, retried, retriedAfterRefresh);
                        return (successAfterRetry, null);
                    }
                    catch (Exception retryEx)
                    {
                        var duration = _clock() - startedAt;
                        var failedRetry = _processor.NormalizeFailure(request, retryEx, retriedAfterRefresh, duration);
                        return (failedRetry, refreshed.Diagnostic);
                    }
                }

                var refreshDiagnostic = refreshed.Diagnostic;
                var durationAfterRefreshFailure = _clock() - startedAt;
                var failedByRefresh = _processor.NormalizeFailure(
                    request,
                    new UnauthorizedAccessException(refreshDiagnostic?.Message ?? "Authentication refresh failed."),
                    retriedAfterRefresh: false,
                    duration: durationAfterRefreshFailure);
                return (failedByRefresh, refreshDiagnostic);
            }

            var durationFallback = _clock() - startedAt;
            var failed = _processor.NormalizeFailure(request, ex, retriedAfterRefresh, durationFallback);
            return (failed, null);
        }
    }
}
