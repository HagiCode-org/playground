using KimiAcpSdk.Configuration;
using KimiAcpSdk.Runtime;
using KimiAcpSdk.Transport;

var arguments = DemoArguments.Parse(args);
var options = ConfigurationLoader.Load(arguments.ConfigPath);
var request = arguments.ToRunRequest();
var profile = options.ResolveProfile(request);
var profileName = string.IsNullOrWhiteSpace(request.ProfileName) ? options.ActiveProfile : request.ProfileName;
await using var session = new KimiSessionRunner(profileName, profile, request);

var showRaw = arguments.ShowRaw;
session.TranscriptObserved += entry =>
{
    if (!showRaw)
    {
        return;
    }

    Console.WriteLine($"raw[{entry.Channel.ToString().ToLowerInvariant()}] {entry.Content}");
};

Console.WriteLine("Kimi ACP Console Demo");
Console.WriteLine($"Profile: {(string.IsNullOrWhiteSpace(request.ProfileName) ? options.ActiveProfile : request.ProfileName)}");
Console.WriteLine($"Executable: {profile.ExecutablePath}");
Console.WriteLine($"Args: {string.Join(' ', profile.Arguments)}");
Console.WriteLine($"Cwd: {profile.WorkingDirectory}");
Console.WriteLine($"Artifacts: {profile.Artifacts.RunStorePath}");
Console.WriteLine("Commands: /connect, /prompt <text>, /raw, /report, /exit");
Console.WriteLine();

if (!string.IsNullOrWhiteSpace(arguments.OneShotPrompt))
{
    await ExecutePromptAsync(arguments.OneShotPrompt);
    return;
}

while (true)
{
    Console.Write("kimi> ");
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    if (string.Equals(line.Trim(), "/exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    try
    {
        if (string.Equals(line.Trim(), "/connect", StringComparison.OrdinalIgnoreCase))
        {
            await session.ConnectAsync();
            var connectedSnapshot = await session.PersistSnapshotAsync();
            Console.WriteLine($"connected: session={session.SessionId}");
            if (connectedSnapshot.Artifact is not null)
            {
                Console.WriteLine($"report: {connectedSnapshot.Artifact.MarkdownReportPath}");
            }

            continue;
        }

        if (line.StartsWith("/prompt ", StringComparison.OrdinalIgnoreCase))
        {
            await ExecutePromptAsync(line[8..].Trim());
            continue;
        }

        if (string.Equals(line.Trim(), "/raw", StringComparison.OrdinalIgnoreCase))
        {
            showRaw = !showRaw;
            Console.WriteLine($"raw transcript printing {(showRaw ? "enabled" : "disabled")}");
            continue;
        }

        if (string.Equals(line.Trim(), "/report", StringComparison.OrdinalIgnoreCase))
        {
            var reportSnapshot = await session.PersistSnapshotAsync();
            PrintFeatureSummary(reportSnapshot);
            if (reportSnapshot.Artifact is not null)
            {
                Console.WriteLine($"report: {reportSnapshot.Artifact.MarkdownReportPath}");
            }

            continue;
        }

        Console.WriteLine("Unknown command. Use /connect, /prompt <text>, /raw, /report, or /exit.");
    }
    catch (Exception exception)
    {
        Console.WriteLine($"error: {exception.Message}");
    }
}

async Task ExecutePromptAsync(string prompt)
{
    await session.ConnectAsync();
    var result = await session.PromptAsync(prompt);
    var snapshot = await session.PersistSnapshotAsync();
    Console.WriteLine($"session: {result.SessionId}");
    Console.WriteLine($"stop reason: {result.StopReason ?? "unknown"}");
    Console.WriteLine($"response: {result.FinalText}");
    if (snapshot.Artifact is not null)
    {
        Console.WriteLine($"report: {snapshot.Artifact.MarkdownReportPath}");
    }
}

static void PrintFeatureSummary(KimiRunResult result)
{
    foreach (var feature in result.Features)
    {
        Console.WriteLine($"- {feature.FeatureId}: {feature.Status} ({feature.Details})");
    }
}

internal sealed class DemoArguments
{
    public string? ConfigPath { get; set; }

    public string? ProfileName { get; set; }

    public string? OneShotPrompt { get; set; }

    public bool ShowRaw { get; set; }

    public List<string> ArgumentOverrides { get; } = [];

    public Dictionary<string, string?> EnvironmentOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? WorkingDirectoryOverride { get; set; }

    public string? ArtifactOutputOverride { get; set; }

    public string? AuthMethodOverride { get; set; }

    public KimiRunRequest ToRunRequest()
    {
        return new KimiRunRequest
        {
            ProfileName = ProfileName ?? string.Empty,
            Prompt = OneShotPrompt,
            ArgumentOverrides = ArgumentOverrides.ToArray(),
            EnvironmentOverrides = new Dictionary<string, string?>(EnvironmentOverrides, StringComparer.OrdinalIgnoreCase),
            WorkingDirectoryOverride = WorkingDirectoryOverride,
            ArtifactOutputOverride = ArtifactOutputOverride,
            AuthMethodOverride = AuthMethodOverride,
        };
    }

    public static DemoArguments Parse(string[] args)
    {
        var parsed = new DemoArguments();
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--config":
                    parsed.ConfigPath = RequireValue(args, ref index, "--config");
                    break;
                case "--profile":
                    parsed.ProfileName = RequireValue(args, ref index, "--profile");
                    break;
                case "--prompt":
                    parsed.OneShotPrompt = RequireValue(args, ref index, "--prompt");
                    break;
                case "--raw":
                    parsed.ShowRaw = true;
                    break;
                case "--arg":
                    parsed.ArgumentOverrides.Add(RequireValue(args, ref index, "--arg"));
                    break;
                case "--cwd":
                    parsed.WorkingDirectoryOverride = RequireValue(args, ref index, "--cwd");
                    break;
                case "--artifact-output":
                    parsed.ArtifactOutputOverride = RequireValue(args, ref index, "--artifact-output");
                    break;
                case "--auth-method":
                    parsed.AuthMethodOverride = RequireValue(args, ref index, "--auth-method");
                    break;
                case "--env":
                    var envAssignment = RequireValue(args, ref index, "--env");
                    var separatorIndex = envAssignment.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        throw new InvalidOperationException("--env expects KEY=value.");
                    }

                    parsed.EnvironmentOverrides[envAssignment[..separatorIndex]] = envAssignment[(separatorIndex + 1)..];
                    break;
                default:
                    throw new InvalidOperationException($"Unknown argument '{args[index]}'.");
            }
        }

        return parsed;
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new InvalidOperationException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}
