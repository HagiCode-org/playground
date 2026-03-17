namespace HermesAcpSdk.Tests;

public sealed class ProtocolParsingTests
{
    [Fact]
    public void TryParseFrame_RecognizesNotificationsAndResponses()
    {
        const string notificationJson = """
        {"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"kind":"assistant","text":"hello"}}}
        """;
        const string responseJson = """
        {"jsonrpc":"2.0","id":7,"result":{"sessionId":"s-1"}}
        """;

        Assert.True(HermesAcpMessageParser.TryParseFrame(notificationJson, out var notification));
        Assert.Equal(HermesFrameKind.Notification, notification.Kind);
        Assert.Equal("session/update", notification.Method);

        Assert.True(HermesAcpMessageParser.TryParseFrame(responseJson, out var response));
        Assert.Equal(HermesFrameKind.Response, response.Kind);
        Assert.Equal(7, response.RequestId);

        var update = HermesAcpMessageParser.ParsePromptUpdate(notification.Payload);
        Assert.NotNull(update);
        Assert.Equal("assistant", update!.Kind);
        Assert.Equal("hello", update.Text);

        var finalText = HermesAcpMessageParser.ExtractPromptText(JsonDocument.Parse("""
        {"result":{"content":[{"type":"text","text":"world"}],"stopReason":"end_turn"}}
        """).RootElement);
        Assert.Equal("world", finalText);
    }
}
