using CopilotSdk.Auth;
using FluentAssertions;

namespace CopilotSdk.Tests;

public sealed class AuthenticationManagerTests
{
    [Fact]
    public async Task EnsureValidCredentialAsync_ShouldAcquireCredential_WhenCacheIsEmpty()
    {
        var provider = new FakeCredentialProvider(
            acquire: () => Task.FromResult(new CopilotCredential("token-1", false, DateTimeOffset.UtcNow.AddMinutes(10))),
            refresh: _ => Task.FromResult(new CopilotCredential("token-2", false, DateTimeOffset.UtcNow.AddMinutes(10))));

        var manager = new CopilotAuthManager(provider);
        var result = await manager.EnsureValidCredentialAsync("req-1", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Credential!.AccessToken.Should().Be("token-1");
    }

    [Fact]
    public async Task EnsureValidCredentialAsync_ShouldRefreshExpiredCredential()
    {
        var now = DateTimeOffset.UtcNow;
        var provider = new FakeCredentialProvider(
            acquire: () => Task.FromResult(new CopilotCredential("expired", false, now.AddMinutes(-1))),
            refresh: _ => Task.FromResult(new CopilotCredential("fresh", false, now.AddMinutes(15))));

        var manager = new CopilotAuthManager(provider, () => now);
        var result = await manager.EnsureValidCredentialAsync("req-2", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Credential!.AccessToken.Should().Be("fresh");
    }

    [Fact]
    public async Task RefreshCredentialAsync_ShouldReturnDiagnostic_WhenRefreshFails()
    {
        var provider = new FakeCredentialProvider(
            acquire: () => Task.FromResult(new CopilotCredential("token", false, DateTimeOffset.UtcNow.AddMinutes(10))),
            refresh: _ => throw new InvalidOperationException("refresh endpoint unavailable"));

        var manager = new CopilotAuthManager(provider);
        await manager.EnsureValidCredentialAsync("req-3-warmup", CancellationToken.None);
        var result = await manager.RefreshCredentialAsync("req-3", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.Category.Should().Be(AuthFailureCategory.RefreshFailed);
    }

    private sealed class FakeCredentialProvider : ICopilotCredentialProvider
    {
        private readonly Func<Task<CopilotCredential>> _acquire;
        private readonly Func<CopilotCredential, Task<CopilotCredential>> _refresh;

        public FakeCredentialProvider(
            Func<Task<CopilotCredential>> acquire,
            Func<CopilotCredential, Task<CopilotCredential>> refresh)
        {
            _acquire = acquire;
            _refresh = refresh;
        }

        public Task<CopilotCredential> AcquireAsync(CancellationToken cancellationToken) => _acquire();

        public Task<CopilotCredential> RefreshAsync(CopilotCredential current, CancellationToken cancellationToken) => _refresh(current);
    }
}
