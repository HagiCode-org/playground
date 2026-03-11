namespace CodeBuddySdk.Runtime;

public sealed class ResponseClassifier
{
    public (bool Success, ProcessFailureCategory FailureCategory, string? FailureMessage) Classify(
        RawProcessResult rawResult,
        NormalizedProcessOutput normalizedOutput)
    {
        if (rawResult.StartFailureCategory != ProcessFailureCategory.None)
        {
            return (false, rawResult.StartFailureCategory, rawResult.StartFailureMessage);
        }

        if (rawResult.TimedOut)
        {
            return (false, ProcessFailureCategory.Timeout, "The CodeBuddy process timed out.");
        }

        var combinedText = string.Join(
            Environment.NewLine,
            new[] { rawResult.StdErr, rawResult.StdOut, normalizedOutput.Transcript }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        if ((rawResult.ExitCode ?? 0) != 0)
        {
            if (LooksLikeAuthenticationFailure(combinedText))
            {
                return (false, ProcessFailureCategory.Authentication, FirstNonEmpty(rawResult.StdErr, rawResult.StdOut, "Authentication failure detected."));
            }

            return (false, ProcessFailureCategory.ProcessExit, FirstNonEmpty(rawResult.StdErr, rawResult.StdOut, $"Process exited with code {rawResult.ExitCode}."));
        }

        if (string.IsNullOrWhiteSpace(normalizedOutput.FinalContent) && normalizedOutput.Events.Count == 0)
        {
            return (false, ProcessFailureCategory.Protocol, "The process completed without producing any observable output.");
        }

        return (true, ProcessFailureCategory.None, null);
    }

    private static bool LooksLikeAuthenticationFailure(string combinedText)
    {
        return combinedText.Contains("auth", StringComparison.OrdinalIgnoreCase)
            || combinedText.Contains("login", StringComparison.OrdinalIgnoreCase)
            || combinedText.Contains("token", StringComparison.OrdinalIgnoreCase)
            || combinedText.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || combinedText.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
            || combinedText.Contains("credential", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
