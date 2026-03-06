using System.Runtime.CompilerServices;

namespace CodexSdk;

public sealed class CodexThread
{
    private readonly CodexExec _exec;
    private readonly CodexOptions _options;
    private readonly ThreadOptions _threadOptions;
    private string? _id;

    internal CodexThread(
        CodexExec exec,
        CodexOptions options,
        ThreadOptions threadOptions,
        string? id = null)
    {
        _exec = exec;
        _options = options;
        _threadOptions = threadOptions;
        _id = id;
    }

    public string? Id => _id;

    public Task<RunResult> RunAsync(
        string input,
        TurnOptions? turnOptions = null,
        CancellationToken cancellationToken = default)
    {
        return RunInternalAsync(new NormalizedInput(input, []), turnOptions, cancellationToken);
    }

    public Task<RunResult> RunAsync(
        IReadOnlyList<UserInputItem> input,
        TurnOptions? turnOptions = null,
        CancellationToken cancellationToken = default)
    {
        return RunInternalAsync(NormalizeInput(input), turnOptions, cancellationToken);
    }

    public IAsyncEnumerable<ThreadEvent> RunStreamedAsync(
        string input,
        TurnOptions? turnOptions = null,
        CancellationToken cancellationToken = default)
    {
        return RunStreamedInternalAsync(new NormalizedInput(input, []), turnOptions, cancellationToken);
    }

    public IAsyncEnumerable<ThreadEvent> RunStreamedAsync(
        IReadOnlyList<UserInputItem> input,
        TurnOptions? turnOptions = null,
        CancellationToken cancellationToken = default)
    {
        return RunStreamedInternalAsync(NormalizeInput(input), turnOptions, cancellationToken);
    }

    private async Task<RunResult> RunInternalAsync(
        NormalizedInput input,
        TurnOptions? turnOptions,
        CancellationToken cancellationToken)
    {
        var items = new List<ThreadItem>();
        var finalResponse = string.Empty;
        Usage? usage = null;
        ThreadError? turnFailure = null;

        await foreach (var @event in RunStreamedInternalAsync(input, turnOptions, cancellationToken))
        {
            switch (@event)
            {
                case ItemCompletedEvent itemCompleted:
                    if (itemCompleted.Item is AgentMessageItem messageItem)
                    {
                        finalResponse = messageItem.Text;
                    }

                    items.Add(itemCompleted.Item);
                    break;
                case TurnCompletedEvent turnCompleted:
                    usage = turnCompleted.Usage;
                    break;
                case TurnFailedEvent turnFailed:
                    turnFailure = turnFailed.Error;
                    break;
            }

            if (turnFailure is not null)
            {
                break;
            }
        }

        if (turnFailure is not null)
        {
            throw new InvalidOperationException(turnFailure.Message);
        }

        return new RunResult(items, finalResponse, usage);
    }

    private async IAsyncEnumerable<ThreadEvent> RunStreamedInternalAsync(
        NormalizedInput input,
        TurnOptions? turnOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var outputSchemaFile = await OutputSchemaTempFile.CreateAsync(
            turnOptions?.OutputSchema,
            cancellationToken);

        await foreach (var line in _exec.RunAsync(
                           CreateExecArgs(input, outputSchemaFile.SchemaPath),
                           cancellationToken))
        {
            ThreadEvent parsedEvent;
            try
            {
                parsedEvent = EventParser.Parse(line);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse event line: {line}", ex);
            }

            if (parsedEvent is ThreadStartedEvent threadStarted)
            {
                _id = threadStarted.ThreadId;
            }

            yield return parsedEvent;
        }
    }

    private CodexExecArgs CreateExecArgs(NormalizedInput input, string? outputSchemaPath)
    {
        return new CodexExecArgs
        {
            Input = input.Prompt,
            BaseUrl = _options.BaseUrl,
            ApiKey = _options.ApiKey,
            ThreadId = _id,
            Images = input.Images,
            Model = _threadOptions.Model,
            SandboxMode = _threadOptions.SandboxMode,
            WorkingDirectory = _threadOptions.WorkingDirectory,
            AdditionalDirectories = _threadOptions.AdditionalDirectories,
            SkipGitRepoCheck = _threadOptions.SkipGitRepoCheck ?? false,
            OutputSchemaFile = outputSchemaPath,
            ModelReasoningEffort = _threadOptions.ModelReasoningEffort,
            NetworkAccessEnabled = _threadOptions.NetworkAccessEnabled,
            WebSearchMode = _threadOptions.WebSearchMode,
            WebSearchEnabled = _threadOptions.WebSearchEnabled,
            ApprovalPolicy = _threadOptions.ApprovalPolicy,
        };
    }

    private static NormalizedInput NormalizeInput(IReadOnlyList<UserInputItem> input)
    {
        var promptParts = new List<string>();
        var images = new List<string>();

        foreach (var item in input)
        {
            switch (item)
            {
                case TextInput text:
                    promptParts.Add(text.Text);
                    break;
                case LocalImageInput image:
                    images.Add(image.Path);
                    break;
            }
        }

        return new NormalizedInput(string.Join("\n\n", promptParts), images);
    }
}
