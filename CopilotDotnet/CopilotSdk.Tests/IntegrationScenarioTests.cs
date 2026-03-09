using CopilotSdk.Auth;
using CopilotSdk.Client;
using CopilotSdk.Configuration;
using CopilotSdk.Models;
using CopilotSdk.Processing;
using CopilotSdk.Runner;
using CopilotSdk.Storage;
using FluentAssertions;

namespace CopilotSdk.Tests;

public sealed class IntegrationScenarioTests
{
    [Fact]
    public async Task EndToEnd_ShouldPersistSuccessfulRun()
    {
        var runPath = CreateTempDirectory();
        var settings = CreateSettings(runPath);

        var provider = new StubCredentialProvider(
            acquire: () => Task.FromResult(new CopilotCredential("token", false, DateTimeOffset.UtcNow.AddMinutes(10))),
            refresh: _ => Task.FromResult(new CopilotCredential("token-refreshed", false, DateTimeOffset.UtcNow.AddMinutes(10))));
        var auth = new CopilotAuthManager(provider);
        var gateway = new StubGateway(_ => Task.FromResult(new CopilotGatewayResponse(new[] { "Hi" }, "Hi", TimeSpan.FromMilliseconds(5), true)));
        var adapter = new CopilotClientAdapter(settings, auth, gateway, new CopilotResponseProcessor());
        var store = new CopilotRunStore(runPath, useSqlite: true);
        var runner = new CopilotPlaygroundRunner(adapter, store);

        var record = await runner.RunAsync(settings.Model, "say hi", CancellationToken.None);

        record.Success.Should().BeTrue();
        File.Exists(Path.Combine(runPath, "runs.jsonl")).Should().BeTrue();
        File.Exists(Path.Combine(runPath, "runs.db")).Should().BeTrue();
    }

    [Fact]
    public async Task EndToEnd_ShouldRetryOnce_OnAuthenticationError()
    {
        var runPath = CreateTempDirectory();
        var settings = CreateSettings(runPath);

        var provider = new StubCredentialProvider(
            acquire: () => Task.FromResult(new CopilotCredential("token-1", false, DateTimeOffset.UtcNow.AddMinutes(10))),
            refresh: _ => Task.FromResult(new CopilotCredential("token-2", false, DateTimeOffset.UtcNow.AddMinutes(10))));

        var attempt = 0;
        var gateway = new StubGateway(_ =>
        {
            attempt++;
            if (attempt == 1)
            {
                throw new InvalidOperationException("401 unauthorized");
            }

            return Task.FromResult(new CopilotGatewayResponse(new[] { "Recovered" }, "Recovered", TimeSpan.FromMilliseconds(8), true));
        });

        var adapter = new CopilotClientAdapter(settings, new CopilotAuthManager(provider), gateway, new CopilotResponseProcessor());

        var (response, _) = await adapter.ExecuteAsync("recover", CancellationToken.None);

        response.Success.Should().BeTrue();
        response.RetriedAfterRefresh.Should().BeTrue();
    }

    [Fact]
    public async Task EndToEnd_ShouldClassifyTimeoutFailure()
    {
        var runPath = CreateTempDirectory();
        var settings = CreateSettings(runPath);

        var provider = new StubCredentialProvider(
            acquire: () => Task.FromResult(new CopilotCredential("token", false, DateTimeOffset.UtcNow.AddMinutes(10))),
            refresh: _ => Task.FromResult(new CopilotCredential("token", false, DateTimeOffset.UtcNow.AddMinutes(10))));

        var gateway = new StubGateway(_ => throw new TimeoutException("gateway timeout"));
        var adapter = new CopilotClientAdapter(settings, new CopilotAuthManager(provider), gateway, new CopilotResponseProcessor());

        var (response, _) = await adapter.ExecuteAsync("slow request", CancellationToken.None);

        response.Success.Should().BeFalse();
        response.ErrorCategory.Should().Be(CopilotErrorCategory.Timeout);
    }

    [Fact]
    public async Task EndToEnd_ShouldReturnAuthFailure_WhenAcquireFails()
    {
        var runPath = CreateTempDirectory();
        var settings = CreateSettings(runPath);

        var provider = new StubCredentialProvider(
            acquire: () => throw new InvalidOperationException("no credentials available"),
            refresh: _ => Task.FromResult(new CopilotCredential("unused", false, DateTimeOffset.UtcNow.AddMinutes(10))));

        var gateway = new StubGateway(_ => Task.FromResult(new CopilotGatewayResponse(Array.Empty<string>(), "unused", TimeSpan.FromMilliseconds(1), true)));
        var adapter = new CopilotClientAdapter(settings, new CopilotAuthManager(provider), gateway, new CopilotResponseProcessor());

        var (response, authDiagnostic) = await adapter.ExecuteAsync("test", CancellationToken.None);

        response.Success.Should().BeFalse();
        response.ErrorCategory.Should().Be(CopilotErrorCategory.Authentication);
        authDiagnostic.Should().NotBeNull();
    }

    private static CopilotPlaygroundSettings CreateSettings(string runStorePath)
    {
        return new CopilotPlaygroundSettings
        {
            Model = "gpt-5",
            WorkingDirectory = Directory.GetCurrentDirectory(),
            RunStorePath = runStorePath,
            UseLoggedInUser = true,
            TimeoutSeconds = 30,
            UseSqlite = true,
        };
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "copilot-dotnet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubCredentialProvider : ICopilotCredentialProvider
    {
        private readonly Func<Task<CopilotCredential>> _acquire;
        private readonly Func<CopilotCredential, Task<CopilotCredential>> _refresh;

        public StubCredentialProvider(
            Func<Task<CopilotCredential>> acquire,
            Func<CopilotCredential, Task<CopilotCredential>> refresh)
        {
            _acquire = acquire;
            _refresh = refresh;
        }

        public Task<CopilotCredential> AcquireAsync(CancellationToken cancellationToken) => _acquire();

        public Task<CopilotCredential> RefreshAsync(CopilotCredential current, CancellationToken cancellationToken) => _refresh(current);
    }

    private sealed class StubGateway : ICopilotSessionGateway
    {
        private readonly Func<CopilotGatewayRequest, Task<CopilotGatewayResponse>> _handler;

        public StubGateway(Func<CopilotGatewayRequest, Task<CopilotGatewayResponse>> handler)
        {
            _handler = handler;
        }

        public Task<CopilotGatewayResponse> SendPromptAsync(CopilotGatewayRequest request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
