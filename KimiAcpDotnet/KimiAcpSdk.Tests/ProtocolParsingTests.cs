namespace KimiAcpSdk.Tests;

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

        Assert.True(KimiAcpMessageParser.TryParseFrame(notificationJson, out var notification));
        Assert.Equal(KimiFrameKind.Notification, notification.Kind);
        Assert.Equal("session/update", notification.Method);

        Assert.True(KimiAcpMessageParser.TryParseFrame(responseJson, out var response));
        Assert.Equal(KimiFrameKind.Response, response.Kind);
        Assert.Equal(7, response.RequestId);

        var update = KimiAcpMessageParser.ParsePromptUpdate(notification.Payload);
        Assert.NotNull(update);
        Assert.Equal("assistant", update!.Kind);
        Assert.Equal("hello", update.Text);

        var finalText = KimiAcpMessageParser.ExtractPromptText(JsonDocument.Parse("""
        {"result":{"content":[{"type":"text","text":"world"}],"stopReason":"end_turn"}}
        """).RootElement);
        Assert.Equal("world", finalText);
    }

    [Fact]
    public async Task Client_BootstrapsWithoutReadyMarker_AndBuildsPromptFromUpdates()
    {
        var transport = new FakeAcpTransport(static payload =>
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var id = root.GetProperty("id").GetInt32();
            var method = root.GetProperty("method").GetString();

            return method switch
            {
                "initialize" =>
                [
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"isAuthenticated\":false,\"authMethods\":[]}}}}"
                ],
                "session/new" =>
                [
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"sessionId\":\"fixture-session\"}}}}"
                ],
                "session/prompt" =>
                [
                    """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"fixture-session","update":{"kind":"assistant","text":"fixture response"}}}""",
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"stopReason\":\"end_turn\",\"content\":[]}}}}"
                ],
                _ => throw new InvalidOperationException($"Unsupported method '{method}'."),
            };
        });

        var profile = new KimiLaunchProfile
        {
            ExecutablePath = "kimi",
            Arguments = ["acp"],
            WorkingDirectory = Directory.GetCurrentDirectory(),
        };

        await using var client = new KimiAcpClient(transport, TimeSpan.FromSeconds(3));
        await client.StartAsync();

        var initialize = await client.InitializeAsync(profile);
        var session = await client.CreateSessionAsync(profile);
        var prompt = await client.PromptAsync(session.SessionId, "Hello");

        Assert.False(initialize.IsAuthenticated);
        Assert.Empty(initialize.AuthMethods);
        Assert.Equal("fixture-session", session.SessionId);
        Assert.Equal("fixture response", prompt.FinalText);
        Assert.Equal("end_turn", prompt.StopReason);
        Assert.Equal(3, transport.SentPayloads.Count);
    }
}
