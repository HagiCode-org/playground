using CopilotSdk.Models;

namespace CopilotSdk.Processing;

public sealed class CopilotResponseProcessor
{
    public CopilotNormalizedResponse NormalizeSuccess(
        CopilotPromptRequest request,
        CopilotGatewayResponse gatewayResponse,
        bool retriedAfterRefresh)
    {
        var content = gatewayResponse.DeltaChunks.Count > 0
            ? string.Concat(gatewayResponse.DeltaChunks)
            : gatewayResponse.FinalContent ?? string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            content = gatewayResponse.FinalContent ?? string.Empty;
        }

        return new CopilotNormalizedResponse(
            CorrelationId: request.CorrelationId,
            Success: true,
            Content: content,
            ErrorCategory: null,
            ErrorMessage: null,
            RetriedAfterRefresh: retriedAfterRefresh,
            Streaming: gatewayResponse.Streaming,
            Duration: gatewayResponse.Duration);
    }

    public CopilotNormalizedResponse NormalizeFailure(
        CopilotPromptRequest request,
        Exception exception,
        bool retriedAfterRefresh,
        TimeSpan duration)
    {
        return new CopilotNormalizedResponse(
            CorrelationId: request.CorrelationId,
            Success: false,
            Content: string.Empty,
            ErrorCategory: ClassifyError(exception),
            ErrorMessage: exception.Message,
            RetriedAfterRefresh: retriedAfterRefresh,
            Streaming: request.Streaming,
            Duration: duration);
    }

    public CopilotErrorCategory ClassifyError(Exception exception)
    {
        if (exception is TimeoutException || exception is OperationCanceledException)
        {
            return CopilotErrorCategory.Timeout;
        }

        var message = exception.Message.ToLowerInvariant();

        if (message.Contains("429") || message.Contains("rate limit") || message.Contains("too many requests"))
        {
            return CopilotErrorCategory.RateLimit;
        }

        if (message.Contains("401") ||
            message.Contains("unauthorized") ||
            message.Contains("authentication") ||
            message.Contains("token") ||
            message.Contains("credential"))
        {
            return CopilotErrorCategory.Authentication;
        }

        return CopilotErrorCategory.UnknownError;
    }
}
