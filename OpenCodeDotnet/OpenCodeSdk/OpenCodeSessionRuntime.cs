using OpenCodeSdk.Generated;

namespace OpenCodeSdk;

public static class OpenCodeSessionRuntime
{
    public static OpenCodeClient Connect(OpenCodeClientOptions? options = null)
    {
        return OpenCodeClientFactory.CreateClient(options);
    }

    public static async Task<OpenCodeSessionHandle> StartAsync(
        OpenCodeSessionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new OpenCodeSessionOptions();

        OpenCodeProcessHandle? processHandle = null;
        Uri? baseUri = options.BaseUri;

        if (baseUri is null)
        {
            processHandle = await OpenCodeProcessManager.StartAsync(options.Process, cancellationToken);
            baseUri = processHandle.BaseUri;
        }

        var client = OpenCodeClientFactory.CreateClient(new OpenCodeClientOptions
        {
            BaseUri = baseUri,
            Directory = options.Directory,
            Workspace = options.Workspace,
        });

        var session = await client.Session.CreateAsync(new OpenCodeSessionCreateRequest
        {
            Title = options.SessionTitle,
        }, cancellationToken);

        return new OpenCodeSessionHandle(client, session, processHandle);
    }
}
