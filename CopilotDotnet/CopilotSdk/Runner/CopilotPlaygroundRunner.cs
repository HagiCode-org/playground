using CopilotSdk.Client;
using CopilotSdk.Models;
using CopilotSdk.Storage;

namespace CopilotSdk.Runner;

public sealed class CopilotPlaygroundRunner
{
    private readonly CopilotClientAdapter _adapter;
    private readonly CopilotRunStore _runStore;
    private readonly Func<DateTimeOffset> _clock;

    public CopilotPlaygroundRunner(
        CopilotClientAdapter adapter,
        CopilotRunStore runStore,
        Func<DateTimeOffset>? clock = null)
    {
        _adapter = adapter;
        _runStore = runStore;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<CopilotRunRecord> RunAsync(string model, string prompt, CancellationToken cancellationToken)
    {
        var startedAt = _clock();
        var (response, authDiagnostic) = await _adapter.ExecuteAsync(prompt, cancellationToken);
        var completedAt = _clock();

        var record = new CopilotRunRecord(
            CorrelationId: response.CorrelationId,
            Model: model,
            Prompt: prompt,
            StartedAtUtc: startedAt,
            CompletedAtUtc: completedAt,
            Success: response.Success,
            Content: response.Content,
            ErrorCategory: response.ErrorCategory,
            ErrorMessage: response.ErrorMessage,
            RetriedAfterRefresh: response.RetriedAfterRefresh,
            Streaming: response.Streaming,
            DurationMs: Math.Max(0, (long)response.Duration.TotalMilliseconds),
            AuthFailureCategory: authDiagnostic?.Category,
            AuthFailureMessage: authDiagnostic?.Message);

        await _runStore.PersistAsync(record, cancellationToken);
        return record;
    }
}
