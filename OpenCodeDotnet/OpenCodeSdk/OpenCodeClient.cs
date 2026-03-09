using OpenCodeSdk.Generated;

namespace OpenCodeSdk;

public sealed class OpenCodeClient
{
    private readonly OpenCodeGeneratedClient _generated;

    internal OpenCodeClient(
        OpenCodeGeneratedClient generated,
        Uri baseUri,
        string? directory,
        string? workspace)
    {
        _generated = generated;
        BaseUri = baseUri;
        Directory = directory;
        Workspace = workspace;

        Global = new GlobalRoutes(_generated);
        Project = new ProjectRoutes(_generated, directory, workspace);
        Session = new SessionRoutes(_generated, directory, workspace);
        File = new FileRoutes(_generated, directory, workspace);
        Event = new EventRoutes(_generated, directory, workspace);
    }

    public Uri BaseUri { get; }

    public string? Directory { get; }

    public string? Workspace { get; }

    public GlobalRoutes Global { get; }

    public ProjectRoutes Project { get; }

    public SessionRoutes Session { get; }

    public FileRoutes File { get; }

    public EventRoutes Event { get; }
}

public sealed class GlobalRoutes
{
    private readonly OpenCodeGeneratedClient _generated;

    internal GlobalRoutes(OpenCodeGeneratedClient generated)
    {
        _generated = generated;
    }

    public Task<OpenCodeHealthResponse> HealthAsync(CancellationToken cancellationToken = default)
    {
        return _generated.GlobalHealthAsync(cancellationToken);
    }
}

public sealed class ProjectRoutes
{
    private readonly OpenCodeGeneratedClient _generated;
    private readonly string? _directory;
    private readonly string? _workspace;

    internal ProjectRoutes(OpenCodeGeneratedClient generated, string? directory, string? workspace)
    {
        _generated = generated;
        _directory = directory;
        _workspace = workspace;
    }

    public Task<IReadOnlyList<OpenCodeProject>> ListAsync(CancellationToken cancellationToken = default)
    {
        return _generated.ProjectListAsync(_directory, _workspace, cancellationToken);
    }
}

public sealed class SessionRoutes
{
    private readonly OpenCodeGeneratedClient _generated;
    private readonly string? _directory;
    private readonly string? _workspace;

    internal SessionRoutes(OpenCodeGeneratedClient generated, string? directory, string? workspace)
    {
        _generated = generated;
        _directory = directory;
        _workspace = workspace;
    }

    public Task<IReadOnlyList<OpenCodeSession>> ListAsync(
        bool? roots = null,
        long? start = null,
        string? search = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return _generated.SessionListAsync(_directory, _workspace, roots, start, search, limit, cancellationToken);
    }

    public Task<OpenCodeSession> CreateAsync(
        OpenCodeSessionCreateRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        return _generated.SessionCreateAsync(request, _directory, _workspace, cancellationToken);
    }

    public Task<IReadOnlyList<OpenCodeMessageEnvelope>> MessagesAsync(
        string sessionId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return _generated.SessionMessagesAsync(sessionId, _directory, _workspace, limit, cancellationToken);
    }

    public Task<OpenCodeMessageEnvelope> PromptAsync(
        string sessionId,
        OpenCodeSessionPromptRequest request,
        CancellationToken cancellationToken = default)
    {
        return _generated.SessionPromptAsync(sessionId, request, _directory, _workspace, cancellationToken);
    }

    public Task<OpenCodeMessageEnvelope> PromptTextAsync(
        string sessionId,
        string text,
        OpenCodeModelSelection? model = null,
        string? agent = null,
        CancellationToken cancellationToken = default)
    {
        return PromptAsync(sessionId, OpenCodeSessionPromptRequest.FromText(text, model, agent), cancellationToken);
    }
}

public sealed class FileRoutes
{
    private readonly OpenCodeGeneratedClient _generated;
    private readonly string? _directory;
    private readonly string? _workspace;

    internal FileRoutes(OpenCodeGeneratedClient generated, string? directory, string? workspace)
    {
        _generated = generated;
        _directory = directory;
        _workspace = workspace;
    }

    public Task<IReadOnlyList<OpenCodeFileNode>> ListAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        return _generated.FileListAsync(path, _directory, _workspace, cancellationToken);
    }
}

public sealed class EventRoutes
{
    private readonly OpenCodeGeneratedClient _generated;
    private readonly string? _directory;
    private readonly string? _workspace;

    internal EventRoutes(OpenCodeGeneratedClient generated, string? directory, string? workspace)
    {
        _generated = generated;
        _directory = directory;
        _workspace = workspace;
    }

    public IAsyncEnumerable<OpenCodeEventEnvelope> SubscribeAsync(CancellationToken cancellationToken = default)
    {
        return _generated.EventSubscribeAsync(_directory, _workspace, cancellationToken);
    }
}
