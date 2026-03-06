using System.Text.Json.Nodes;

namespace CodexSdk;

internal sealed class OutputSchemaTempFile : IAsyncDisposable
{
    private readonly string? _directory;

    private OutputSchemaTempFile(string? schemaPath, string? directory)
    {
        SchemaPath = schemaPath;
        _directory = directory;
    }

    public string? SchemaPath { get; }

    public static async Task<OutputSchemaTempFile> CreateAsync(
        JsonObject? schema,
        CancellationToken cancellationToken)
    {
        if (schema is null)
        {
            return new OutputSchemaTempFile(null, null);
        }

        var schemaDirectory = Path.Combine(
            Path.GetTempPath(),
            $"codex-output-schema-{Guid.NewGuid():N}");
        Directory.CreateDirectory(schemaDirectory);

        var schemaPath = Path.Combine(schemaDirectory, "schema.json");
        try
        {
            await File.WriteAllTextAsync(schemaPath, schema.ToJsonString(), cancellationToken);
            return new OutputSchemaTempFile(schemaPath, schemaDirectory);
        }
        catch
        {
            TryDeleteDirectory(schemaDirectory);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_directory is not null)
        {
            TryDeleteDirectory(_directory);
        }

        return ValueTask.CompletedTask;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Suppress cleanup failures.
        }
    }
}
