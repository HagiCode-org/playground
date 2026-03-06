using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CodexSdk;

internal sealed class CodexExec
{
    private const string InternalOriginatorEnv = "CODEX_INTERNAL_ORIGINATOR_OVERRIDE";
    private const string CSharpSdkOriginator = "codex_sdk_csharp";

    private static readonly Regex TomlBareKey = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    private readonly string _executablePath;
    private readonly IReadOnlyDictionary<string, string>? _envOverride;
    private readonly JsonObject? _configOverrides;

    public CodexExec(
        string? executablePath = null,
        IReadOnlyDictionary<string, string>? envOverride = null,
        JsonObject? configOverrides = null)
    {
        _executablePath = ResolveCodexPath(executablePath);
        _envOverride = envOverride;
        _configOverrides = configOverrides;
    }

    public async IAsyncEnumerable<string> RunAsync(
        CodexExecArgs args,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in BuildCommandArguments(args))
        {
            startInfo.ArgumentList.Add(argument);
        }

        ConfigureEnvironment(startInfo, args);

        using var process = new Process
        {
            StartInfo = startInfo,
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start Codex process.");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start Codex executable '{_executablePath}'.",
                ex);
        }

        using var cancellationRegistration = cancellationToken.Register(() => TryKill(process));

        await process.StandardInput.WriteAsync(args.Input.AsMemory(), cancellationToken);
        process.StandardInput.Close();

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            yield return line;
        }

        await process.WaitForExitAsync(cancellationToken);

        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Codex exec exited with code {process.ExitCode}: {stderr}");
        }
    }

    private IEnumerable<string> BuildCommandArguments(CodexExecArgs args)
    {
        var commandArgs = new List<string>
        {
            "exec",
            "--experimental-json",
        };

        if (_configOverrides is not null)
        {
            foreach (var overrideValue in SerializeConfigOverrides(_configOverrides))
            {
                commandArgs.Add("--config");
                commandArgs.Add(overrideValue);
            }
        }

        if (!string.IsNullOrWhiteSpace(args.Model))
        {
            commandArgs.Add("--model");
            commandArgs.Add(args.Model);
        }

        if (!string.IsNullOrWhiteSpace(args.SandboxMode))
        {
            commandArgs.Add("--sandbox");
            commandArgs.Add(args.SandboxMode);
        }

        if (!string.IsNullOrWhiteSpace(args.WorkingDirectory))
        {
            commandArgs.Add("--cd");
            commandArgs.Add(args.WorkingDirectory);
        }

        if (args.AdditionalDirectories?.Count > 0)
        {
            foreach (var directory in args.AdditionalDirectories)
            {
                commandArgs.Add("--add-dir");
                commandArgs.Add(directory);
            }
        }

        if (args.SkipGitRepoCheck)
        {
            commandArgs.Add("--skip-git-repo-check");
        }

        if (!string.IsNullOrWhiteSpace(args.OutputSchemaFile))
        {
            commandArgs.Add("--output-schema");
            commandArgs.Add(args.OutputSchemaFile);
        }

        if (!string.IsNullOrWhiteSpace(args.ModelReasoningEffort))
        {
            commandArgs.Add("--config");
            commandArgs.Add($"model_reasoning_effort=\"{args.ModelReasoningEffort}\"");
        }

        if (args.NetworkAccessEnabled.HasValue)
        {
            commandArgs.Add("--config");
            commandArgs.Add(
                $"sandbox_workspace_write.network_access={(args.NetworkAccessEnabled.Value ? "true" : "false")}");
        }

        if (!string.IsNullOrWhiteSpace(args.WebSearchMode))
        {
            commandArgs.Add("--config");
            commandArgs.Add($"web_search=\"{args.WebSearchMode}\"");
        }
        else if (args.WebSearchEnabled.HasValue)
        {
            commandArgs.Add("--config");
            commandArgs.Add($"web_search=\"{(args.WebSearchEnabled.Value ? "live" : "disabled")}\"");
        }

        if (!string.IsNullOrWhiteSpace(args.ApprovalPolicy))
        {
            commandArgs.Add("--config");
            commandArgs.Add($"approval_policy=\"{args.ApprovalPolicy}\"");
        }

        if (!string.IsNullOrWhiteSpace(args.ThreadId))
        {
            commandArgs.Add("resume");
            commandArgs.Add(args.ThreadId);
        }

        if (args.Images?.Count > 0)
        {
            foreach (var image in args.Images)
            {
                commandArgs.Add("--image");
                commandArgs.Add(image);
            }
        }

        return commandArgs;
    }

    private void ConfigureEnvironment(ProcessStartInfo startInfo, CodexExecArgs args)
    {
        if (_envOverride is not null)
        {
            startInfo.Environment.Clear();
            foreach (var variable in _envOverride)
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }
        }

        if (!startInfo.Environment.ContainsKey(InternalOriginatorEnv))
        {
            startInfo.Environment[InternalOriginatorEnv] = CSharpSdkOriginator;
        }

        if (!string.IsNullOrWhiteSpace(args.BaseUrl))
        {
            startInfo.Environment["OPENAI_BASE_URL"] = args.BaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(args.ApiKey))
        {
            startInfo.Environment["CODEX_API_KEY"] = args.ApiKey;
        }
    }

    private static string ResolveCodexPath(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        var envPath = Environment.GetEnvironmentVariable("CODEX_EXECUTABLE");
        return string.IsNullOrWhiteSpace(envPath) ? "codex" : envPath;
    }

    private static IEnumerable<string> SerializeConfigOverrides(JsonObject configOverrides)
    {
        var overrides = new List<string>();
        FlattenConfigOverrides(configOverrides, string.Empty, overrides);
        return overrides;
    }

    private static void FlattenConfigOverrides(
        JsonObject value,
        string prefix,
        List<string> overrides)
    {
        if (prefix.Length > 0 && !value.Any())
        {
            overrides.Add($"{prefix}={{}}");
            return;
        }

        foreach (var entry in value)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                throw new InvalidOperationException("Codex config override keys must be non-empty strings.");
            }

            if (entry.Value is null)
            {
                throw new InvalidOperationException(
                    $"Codex config override at '{entry.Key}' cannot be null.");
            }

            var path = prefix.Length == 0 ? entry.Key : $"{prefix}.{entry.Key}";
            if (entry.Value is JsonObject objectValue)
            {
                FlattenConfigOverrides(objectValue, path, overrides);
            }
            else
            {
                overrides.Add($"{path}={ToTomlValue(entry.Value, path)}");
            }
        }
    }

    private static string ToTomlValue(JsonNode value, string path)
    {
        using var document = JsonDocument.Parse(value.ToJsonString());
        return ToTomlValue(document.RootElement, path);
    }

    private static string ToTomlValue(JsonElement value, string path)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => JsonSerializer.Serialize(value.GetString()),
            JsonValueKind.Number => SerializeJsonNumber(value, path),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => SerializeArray(value, path),
            JsonValueKind.Object => SerializeInlineTable(value, path),
            JsonValueKind.Null => throw new InvalidOperationException(
                $"Codex config override at '{path}' cannot be null."),
            _ => throw new InvalidOperationException(
                $"Unsupported Codex config override at '{path}' with JSON kind '{value.ValueKind}'."),
        };
    }

    private static string SerializeArray(JsonElement value, string path)
    {
        var rendered = new List<string>();
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            rendered.Add(ToTomlValue(item, $"{path}[{index}]"));
            index++;
        }

        return $"[{string.Join(", ", rendered)}]";
    }

    private static string SerializeInlineTable(JsonElement value, string path)
    {
        var entries = new List<string>();
        foreach (var property in value.EnumerateObject())
        {
            entries.Add($"{FormatTomlKey(property.Name)} = {ToTomlValue(property.Value, $"{path}.{property.Name}")}");
        }

        return $"{{{string.Join(", ", entries)}}}";
    }

    private static string SerializeJsonNumber(JsonElement value, string path)
    {
        var rawNumber = value.GetRawText();
        if (double.TryParse(rawNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && !double.IsFinite(parsed))
        {
            throw new InvalidOperationException($"Codex config override at '{path}' must be finite.");
        }

        return rawNumber;
    }

    private static string FormatTomlKey(string key)
    {
        return TomlBareKey.IsMatch(key)
            ? key
            : JsonSerializer.Serialize(key);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Suppress cleanup failures.
        }
    }
}

internal sealed class CodexExecArgs
{
    public required string Input { get; init; }

    public string? BaseUrl { get; init; }

    public string? ApiKey { get; init; }

    public string? ThreadId { get; init; }

    public IReadOnlyList<string>? Images { get; init; }

    public string? Model { get; init; }

    public string? SandboxMode { get; init; }

    public string? WorkingDirectory { get; init; }

    public IReadOnlyList<string>? AdditionalDirectories { get; init; }

    public bool SkipGitRepoCheck { get; init; }

    public string? OutputSchemaFile { get; init; }

    public string? ModelReasoningEffort { get; init; }

    public bool? NetworkAccessEnabled { get; init; }

    public string? WebSearchMode { get; init; }

    public bool? WebSearchEnabled { get; init; }

    public string? ApprovalPolicy { get; init; }
}
