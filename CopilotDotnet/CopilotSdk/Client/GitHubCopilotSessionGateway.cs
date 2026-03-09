using CopilotSdk.Models;
using GitHub.Copilot.SDK;

namespace CopilotSdk.Client;

public sealed class GitHubCopilotSessionGateway : ICopilotSessionGateway
{
    public async Task<CopilotGatewayResponse> SendPromptAsync(
        CopilotGatewayRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var deltaChunks = new List<string>();
        string? finalContent = null;
        Exception? sessionError = null;

        var clientOptions = new CopilotClientOptions
        {
            AutoStart = true,
            UseStdio = true,
            AutoRestart = true,
            Cwd = request.WorkingDirectory,
            CliPath = request.CliPath,
            CliUrl = request.CliUrl,
            UseLoggedInUser = request.Credential.UseLoggedInUser,
            GitHubToken = request.Credential.AccessToken,
            CliArgs = new[]
            {
                "--allow-all",
                "--no-ask-user",
            },
        };

        await using var client = new CopilotClient(clientOptions);
        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = request.Model,
            WorkingDirectory = request.WorkingDirectory,
            Streaming = request.Streaming,
            OnPermissionRequest = PermissionHandler.ApproveAll,
        }, cancellationToken);

        using var subscription = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent deltaEvent when !string.IsNullOrWhiteSpace(deltaEvent.Data.DeltaContent):
                    deltaChunks.Add(deltaEvent.Data.DeltaContent);
                    break;
                case AssistantMessageEvent messageEvent:
                    finalContent = messageEvent.Data.Content;
                    break;
                case SessionErrorEvent errorEvent:
                    sessionError = new InvalidOperationException(errorEvent.Data.Message);
                    break;
            }
        });

        AssistantMessageEvent? response = null;
        try
        {
            response = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = request.Prompt },
                request.Timeout,
                cancellationToken);
        }
        catch (Exception ex)
        {
            sessionError = ex;
        }

        stopwatch.Stop();

        if (sessionError is not null)
        {
            throw sessionError;
        }

        finalContent ??= response?.Data?.Content;
        return new CopilotGatewayResponse(deltaChunks, finalContent, stopwatch.Elapsed, request.Streaming);
    }
}
