using IFlowSdk.Models;

namespace IFlowSdk.Client;

public static class QueryHelpers
{
    public static async Task<string> QueryAsync(string prompt, IReadOnlyList<string>? files = null, IFlowOptions? options = null, CancellationToken cancellationToken = default)
    {
        await using var client = new IFlowClient(options);
        await client.ConnectAsync(cancellationToken);
        await client.SendMessageAsync(prompt, files, cancellationToken);

        var builder = new StringBuilder();
        await foreach (var message in client.ReceiveMessagesAsync(cancellationToken))
        {
            switch (message)
            {
                case AssistantMessage assistant when !string.IsNullOrWhiteSpace(assistant.Chunk.Text):
                    builder.Append(assistant.Chunk.Text);
                    break;
                case TaskFinishMessage:
                    return builder.ToString();
                case ErrorMessage error:
                    throw new InvalidOperationException($"iFlow returned an error: {error.MessageText} {error.Details}".Trim());
            }
        }

        return builder.ToString();
    }

    public static async IAsyncEnumerable<string> QueryStreamAsync(string prompt, IReadOnlyList<string>? files = null, IFlowOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var client = new IFlowClient(options);
        await client.ConnectAsync(cancellationToken);
        await client.SendMessageAsync(prompt, files, cancellationToken);

        await foreach (var message in client.ReceiveMessagesAsync(cancellationToken))
        {
            switch (message)
            {
                case AssistantMessage assistant when !string.IsNullOrWhiteSpace(assistant.Chunk.Text):
                    yield return assistant.Chunk.Text!;
                    break;
                case TaskFinishMessage:
                    yield break;
                case ErrorMessage error:
                    throw new InvalidOperationException($"iFlow returned an error: {error.MessageText} {error.Details}".Trim());
            }
        }
    }

    public static string Query(string prompt, IReadOnlyList<string>? files = null, IFlowOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => QueryAsync(prompt, files, options, cancellationToken), cancellationToken).GetAwaiter().GetResult();
    }
}
