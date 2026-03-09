namespace CopilotSdk.Auth;

public sealed class CopilotAuthManager
{
    private readonly ICopilotCredentialProvider _provider;
    private readonly Func<DateTimeOffset> _clock;
    private CopilotCredential? _cached;

    public CopilotAuthManager(ICopilotCredentialProvider provider, Func<DateTimeOffset>? clock = null)
    {
        _provider = provider;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<AuthResult> EnsureValidCredentialAsync(string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            if (_cached is null)
            {
                _cached = await _provider.AcquireAsync(cancellationToken);
            }

            if (_cached.IsExpired(_clock()))
            {
                _cached = await _provider.RefreshAsync(_cached, cancellationToken);
            }

            return AuthResult.FromCredential(_cached);
        }
        catch (Exception ex)
        {
            return AuthResult.FromDiagnostic(CreateDiagnostic(ex, correlationId));
        }
    }

    public async Task<AuthResult> RefreshCredentialAsync(string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            if (_cached is null)
            {
                _cached = await _provider.AcquireAsync(cancellationToken);
            }
            else
            {
                _cached = await _provider.RefreshAsync(_cached, cancellationToken);
            }

            return AuthResult.FromCredential(_cached);
        }
        catch (Exception ex)
        {
            return AuthResult.FromDiagnostic(CreateDiagnostic(ex, correlationId));
        }
    }

    private AuthDiagnostic CreateDiagnostic(Exception ex, string correlationId)
    {
        var category = Classify(ex);
        return new AuthDiagnostic(category, ex.Message, correlationId, _clock());
    }

    private static AuthFailureCategory Classify(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();

        if (message.Contains("no credentials") || message.Contains("missing") || message.Contains("token is required"))
        {
            return AuthFailureCategory.MissingCredentials;
        }

        if (message.Contains("expired"))
        {
            return AuthFailureCategory.ExpiredCredentials;
        }

        if (message.Contains("refresh"))
        {
            return AuthFailureCategory.RefreshFailed;
        }

        if (message.Contains("unauthorized") || message.Contains("401"))
        {
            return AuthFailureCategory.Unauthorized;
        }

        return AuthFailureCategory.Unknown;
    }
}
