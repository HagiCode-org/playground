using CopilotSdk.Models;
using CopilotSdk.Processing;
using FluentAssertions;

namespace CopilotSdk.Tests;

public sealed class ResponseProcessorTests
{
    [Fact]
    public void NormalizeSuccess_ShouldCombineDeltaChunks()
    {
        var processor = new CopilotResponseProcessor();
        var request = new CopilotPromptRequest("req-1", "gpt-5", "hello", TimeSpan.FromSeconds(30), Streaming: true);
        var gateway = new CopilotGatewayResponse(new[] { "hel", "lo" }, FinalContent: null, TimeSpan.FromMilliseconds(120), Streaming: true);

        var result = processor.NormalizeSuccess(request, gateway, retriedAfterRefresh: false);

        result.Success.Should().BeTrue();
        result.Content.Should().Be("hello");
    }

    [Theory]
    [InlineData("401 unauthorized", CopilotErrorCategory.Authentication)]
    [InlineData("rate limit exceeded", CopilotErrorCategory.RateLimit)]
    [InlineData("too many requests (429)", CopilotErrorCategory.RateLimit)]
    public void ClassifyError_ShouldMapKnownPatterns(string message, CopilotErrorCategory expected)
    {
        var processor = new CopilotResponseProcessor();

        var category = processor.ClassifyError(new InvalidOperationException(message));

        category.Should().Be(expected);
    }

    [Fact]
    public void ClassifyError_ShouldMapTimeoutException()
    {
        var processor = new CopilotResponseProcessor();

        var category = processor.ClassifyError(new TimeoutException("operation timed out"));

        category.Should().Be(CopilotErrorCategory.Timeout);
    }
}
