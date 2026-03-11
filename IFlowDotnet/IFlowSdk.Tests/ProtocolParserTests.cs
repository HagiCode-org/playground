using IFlowSdk.Client;
using IFlowSdk.Models;
using IFlowSdk.Protocol;

namespace IFlowSdk.Tests;

public sealed class ProtocolParserTests
{
    [Fact]
    public void ControlMessage_IsParsedAsControlFrame()
    {
        var parsed = AcpMessageParser.TryParseInbound("//ready", out var frame);

        Assert.True(parsed);
        Assert.Equal(AcpFrameKind.Control, frame.Kind);
        Assert.Equal("//ready", frame.ControlMessage);
    }

    [Fact]
    public void AssistantChunk_IsMappedToAssistantMessage()
    {
        const string raw = """
        {"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s1","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"hello"},"agentId":"agent-1"}}}
        """;

        AcpMessageParser.TryParseInbound(raw, out var frame);
        var client = new TestClient();

        var message = client.Map(frame);

        var assistant = Assert.IsType<AssistantMessage>(message);
        Assert.Equal("hello", assistant.Chunk.Text);
        Assert.Equal("agent-1", assistant.AgentId);
    }

    [Fact]
    public void PermissionRequest_IsMappedToToolConfirmationRequest()
    {
        const string raw = """
        {"jsonrpc":"2.0","id":42,"method":"session/request_permission","params":{"sessionId":"s1","toolCall":{"id":"tool-1","title":"Run command","kind":"execute","status":"pending","confirmation":{"type":"execute","command":"pwd"}},"options":[{"id":"proceed_once","name":"Approve once"},{"id":"cancel","name":"Reject"}]}}
        """;

        AcpMessageParser.TryParseInbound(raw, out var frame);
        var client = new TestClient();

        var message = client.Map(frame);

        var confirmation = Assert.IsType<ToolConfirmationRequestMessage>(message);
        Assert.Equal(42, confirmation.RequestId);
        Assert.Equal("tool-1", confirmation.ToolCall.Id);
        Assert.Equal(2, confirmation.Options.Count);
        Assert.Equal("proceed_once", confirmation.Options[0].Id);
    }

    [Fact]
    public async Task DropOldestQueueStrategy_KeepsLatestMessage()
    {
        var options = new IFlowOptions
        {
            MaxMessageQueueSize = 1,
            QueueOverflowStrategy = QueueOverflowStrategy.DropOldest,
        };
        var client = new TestClient(options);

        const string firstRaw = """
        {"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s1","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"first"}}}}
        """;
        const string secondRaw = """
        {"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s1","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"second"}}}}
        """;

        AcpMessageParser.TryParseInbound(firstRaw, out var firstFrame);
        AcpMessageParser.TryParseInbound(secondRaw, out var secondFrame);

        await client.PublishAsync(firstFrame);
        await client.PublishAsync(secondFrame);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await foreach (var message in client.ReceiveMessagesAsync(cts.Token))
        {
            var assistant = Assert.IsType<AssistantMessage>(message);
            Assert.Equal("second", assistant.Chunk.Text);
            break;
        }
    }

    private sealed class TestClient : IFlowClient
    {
        public TestClient(IFlowOptions? options = null) : base(options)
        {
        }

        public Message? Map(AcpInboundFrame frame) => MapFrameToMessage(frame);

        public ValueTask PublishAsync(AcpInboundFrame frame) => HandleInboundFrameAsync(frame);
    }
}
