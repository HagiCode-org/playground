using System.Text.Json;
using CopilotSdk.Models;
using Microsoft.Data.Sqlite;

namespace CopilotSdk.Storage;

public sealed class CopilotRunStore
{
    private readonly string _storePath;
    private readonly bool _useSqlite;

    public CopilotRunStore(string storePath, bool useSqlite)
    {
        _storePath = storePath;
        _useSqlite = useSqlite;
    }

    public async Task PersistAsync(CopilotRunRecord record, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_storePath);

        var jsonlPath = Path.Combine(_storePath, "runs.jsonl");
        var json = JsonSerializer.Serialize(record);
        await File.AppendAllTextAsync(jsonlPath, json + Environment.NewLine, cancellationToken);

        if (!_useSqlite)
        {
            return;
        }

        SQLitePCL.Batteries.Init();

        var sqlitePath = Path.Combine(_storePath, "runs.db");
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync(cancellationToken);

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = @"
CREATE TABLE IF NOT EXISTS run_outcomes (
    correlation_id TEXT PRIMARY KEY,
    model TEXT NOT NULL,
    prompt TEXT NOT NULL,
    started_at_utc TEXT NOT NULL,
    completed_at_utc TEXT NOT NULL,
    success INTEGER NOT NULL,
    content TEXT NOT NULL,
    error_category TEXT NULL,
    error_message TEXT NULL,
    retried_after_refresh INTEGER NOT NULL,
    streaming INTEGER NOT NULL,
    duration_ms INTEGER NOT NULL,
    auth_failure_category TEXT NULL,
    auth_failure_message TEXT NULL
);";
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var insert = connection.CreateCommand();
        insert.CommandText = @"
INSERT OR REPLACE INTO run_outcomes (
    correlation_id,
    model,
    prompt,
    started_at_utc,
    completed_at_utc,
    success,
    content,
    error_category,
    error_message,
    retried_after_refresh,
    streaming,
    duration_ms,
    auth_failure_category,
    auth_failure_message
) VALUES (
    $correlation_id,
    $model,
    $prompt,
    $started_at_utc,
    $completed_at_utc,
    $success,
    $content,
    $error_category,
    $error_message,
    $retried_after_refresh,
    $streaming,
    $duration_ms,
    $auth_failure_category,
    $auth_failure_message
);";

        insert.Parameters.AddWithValue("$correlation_id", record.CorrelationId);
        insert.Parameters.AddWithValue("$model", record.Model);
        insert.Parameters.AddWithValue("$prompt", record.Prompt);
        insert.Parameters.AddWithValue("$started_at_utc", record.StartedAtUtc.ToString("O"));
        insert.Parameters.AddWithValue("$completed_at_utc", record.CompletedAtUtc.ToString("O"));
        insert.Parameters.AddWithValue("$success", record.Success ? 1 : 0);
        insert.Parameters.AddWithValue("$content", record.Content);
        insert.Parameters.AddWithValue("$error_category", (object?)record.ErrorCategory?.ToString() ?? DBNull.Value);
        insert.Parameters.AddWithValue("$error_message", (object?)record.ErrorMessage ?? DBNull.Value);
        insert.Parameters.AddWithValue("$retried_after_refresh", record.RetriedAfterRefresh ? 1 : 0);
        insert.Parameters.AddWithValue("$streaming", record.Streaming ? 1 : 0);
        insert.Parameters.AddWithValue("$duration_ms", record.DurationMs);
        insert.Parameters.AddWithValue("$auth_failure_category", (object?)record.AuthFailureCategory?.ToString() ?? DBNull.Value);
        insert.Parameters.AddWithValue("$auth_failure_message", (object?)record.AuthFailureMessage ?? DBNull.Value);

        await insert.ExecuteNonQueryAsync(cancellationToken);
    }
}
