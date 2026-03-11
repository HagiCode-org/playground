using CodeBuddySdk.Configuration;

namespace CodeBuddySdk.Runtime;

public sealed class CodeBuddyProcessClient
{
    private readonly IProcessRunner _processRunner;
    private readonly EventNormalizer _eventNormalizer;
    private readonly ResponseClassifier _responseClassifier;

    public CodeBuddyProcessClient(
        IProcessRunner? processRunner = null,
        EventNormalizer? eventNormalizer = null,
        ResponseClassifier? responseClassifier = null)
    {
        _processRunner = processRunner ?? new CodeBuddyProcessRunner();
        _eventNormalizer = eventNormalizer ?? new EventNormalizer();
        _responseClassifier = responseClassifier ?? new ResponseClassifier();
    }

    public async Task<CodeBuddyExecutionResult> ExecuteLiveAsync(
        CodeBuddyRunOptions options,
        CodeBuddyExecutionRequest request,
        CancellationToken cancellationToken)
    {
        using var promptFileScope = CreatePromptFileIfNeeded(options, request);
        var processRequest = BuildProcessRequest(options, request, promptFileScope?.Path);
        var rawResult = await _processRunner.RunAsync(processRequest, cancellationToken);
        return BuildExecutionResult(request, rawResult);
    }

    public CodeBuddyExecutionResult NormalizeFixture(CodeBuddyExecutionRequest request, RawProcessResult rawResult)
    {
        return BuildExecutionResult(request, rawResult);
    }

    private CodeBuddyExecutionResult BuildExecutionResult(CodeBuddyExecutionRequest request, RawProcessResult rawResult)
    {
        var normalizedOutput = _eventNormalizer.Normalize(rawResult);
        var classification = _responseClassifier.Classify(rawResult, normalizedOutput);

        return new CodeBuddyExecutionResult
        {
            ScenarioName = request.ScenarioName,
            Mode = request.Mode,
            Prompt = request.Prompt,
            Success = classification.Success,
            FailureCategory = classification.FailureCategory,
            FailureMessage = classification.FailureMessage,
            FinalContent = normalizedOutput.FinalContent,
            Duration = rawResult.Duration,
            ExitCode = rawResult.ExitCode,
            Events = normalizedOutput.Events,
            Transcript = normalizedOutput.Transcript,
            StdErr = rawResult.StdErr,
            CommandDescription = rawResult.CommandDescription,
        };
    }

    private ProcessRequest BuildProcessRequest(CodeBuddyRunOptions options, CodeBuddyExecutionRequest request, string? promptFilePath)
    {
        var arguments = options.Arguments
            .Select(arg => arg
                .Replace("{workingDirectory}", options.WorkingDirectory, StringComparison.Ordinal)
                .Replace("{promptFile}", promptFilePath ?? string.Empty, StringComparison.Ordinal)
                .Replace("{prompt}", request.Prompt, StringComparison.Ordinal))
            .Where(static arg => !string.IsNullOrWhiteSpace(arg))
            .ToArray();

        return new ProcessRequest
        {
            ExecutablePath = options.CliPath,
            WorkingDirectory = options.WorkingDirectory,
            Arguments = arguments,
            PromptTransport = options.PromptTransport,
            InputText = options.PromptTransport == PromptTransport.Stdin ? request.Prompt : null,
            PromptFilePath = promptFilePath,
            EnvironmentVariables = options.EnvironmentVariables,
            Timeout = request.Timeout,
        };
    }

    private static TemporaryPromptFile? CreatePromptFileIfNeeded(CodeBuddyRunOptions options, CodeBuddyExecutionRequest request)
    {
        if (options.PromptTransport != PromptTransport.Arguments
            || !options.Arguments.Any(static arg => arg.Contains("{promptFile}", StringComparison.Ordinal)))
        {
            return null;
        }

        var directory = Path.Combine(Path.GetTempPath(), "codebuddy-dotnet-prompts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "prompt.txt");
        File.WriteAllText(path, request.Prompt);
        return new TemporaryPromptFile(path, directory);
    }

    private sealed class TemporaryPromptFile : IDisposable
    {
        public TemporaryPromptFile(string path, string directory)
        {
            Path = path;
            DirectoryPath = directory;
        }

        public string Path { get; }

        private string DirectoryPath { get; }

        public void Dispose()
        {
            try
            {
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }

                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
