using CopilotSdk.Configuration;

namespace CopilotSdk.Auth;

public sealed class EnvironmentCredentialProvider : ICopilotCredentialProvider
{
    private readonly CopilotPlaygroundSettings _settings;
    private readonly Func<DateTimeOffset> _clock;

    public EnvironmentCredentialProvider(
        CopilotPlaygroundSettings settings,
        Func<DateTimeOffset>? clock = null)
    {
        _settings = settings;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<CopilotCredential> AcquireAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(_settings.GitHubToken))
        {
            return Task.FromResult(
                new CopilotCredential(_settings.GitHubToken, UseLoggedInUser: false, _clock().AddHours(8)));
        }

        if (_settings.UseLoggedInUser)
        {
            return Task.FromResult(
                new CopilotCredential(null, UseLoggedInUser: true, _clock().AddMinutes(5)));
        }

        throw new InvalidOperationException("No credentials available for Copilot authentication.");
    }

    public Task<CopilotCredential> RefreshAsync(CopilotCredential current, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(_settings.GitHubToken))
        {
            return Task.FromResult(
                current with
                {
                    AccessToken = _settings.GitHubToken,
                    UseLoggedInUser = false,
                    ExpiresAtUtc = _clock().AddHours(8),
                });
        }

        if (_settings.UseLoggedInUser)
        {
            return Task.FromResult(current with { ExpiresAtUtc = _clock().AddMinutes(5) });
        }

        throw new InvalidOperationException("Token refresh failed because no credential source is configured.");
    }
}
