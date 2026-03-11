using IFlowSdk.Client;
using IFlowSdk.Models;
using IFlowSdk.Protocol;

namespace IFlowSdk.StreamingTest;

/// <summary>
/// 流式测试客户端 - 扩展 IFlowClient 以支持测试功能
/// </summary>
public sealed class StreamingTestClient : IFlowClient
{
    private readonly TestResults _results = new();
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private DateTimeOffset? _firstMessageTime;
    private DateTimeOffset? _lastChunkTime;
    private TimeSpan _totalChunkInterval = TimeSpan.Zero;
    private int _chunkIntervalCount = 0;
    private readonly TaskCompletionSource _completionTcs = new();

    private string _accumulatedAssistantText = string.Empty;
    private string _accumulatedThoughtText = string.Empty;

    public bool ShowRawMessages { get; set; } = false;

    public StreamingTestClient(IFlowOptions? options = null) : base(options)
    {
    }

    protected override bool CaptureRawMessages => true;

    public TestResults GetTestResults()
    {
        _results.TotalTestDuration = DateTimeOffset.UtcNow - _startTime;
        if (_chunkIntervalCount > 0 && _totalChunkInterval > TimeSpan.Zero)
        {
            _results.AverageChunkInterval = TimeSpan.FromTicks(_totalChunkInterval.Ticks / _chunkIntervalCount);
        }
        return _results;
    }

    public async Task WaitForCompletionAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await _completionTcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"测试在 {timeout.TotalSeconds} 秒后超时（可能仍在运行）");
        }
    }

    public void CompleteTest()
    {
        _completionTcs.TrySetResult();
    }

    protected override async ValueTask PublishRawMessageAsync(RawMessage rawMessage)
    {
        _results.RawMessageCount++;

        // 分析原始消息类型
        if (rawMessage.Kind.Equals("Control", StringComparison.Ordinal))
        {
            _results.RecordMessage("Control");
        }
        else if (rawMessage.Kind.Equals("SessionUpdate", StringComparison.Ordinal))
        {
            _results.SessionUpdateCount++;
            _results.RecordMessage("SessionUpdate");
        }
        else if (rawMessage.Kind.Equals("PermissionRequest", StringComparison.Ordinal))
        {
            _results.PermissionRequestCount++;
            _results.RecordMessage("PermissionRequest");
        }
        else if (rawMessage.Kind.Equals("Response", StringComparison.Ordinal))
        {
            _results.JsonRpcResponseCount++;
            _results.RecordMessage("JsonRpcResponse");
        }
        else if (rawMessage.Kind.Equals("Error", StringComparison.Ordinal))
        {
            _results.ErrorCount++;
            _results.RecordMessage("Error");
        }

        await base.PublishRawMessageAsync(rawMessage);
    }

    protected override async ValueTask HandleInboundFrameAsync(AcpInboundFrame frame)
    {
        // 记录帧接收时间
        var now = DateTimeOffset.UtcNow;
        if (_firstMessageTime is null)
        {
            _firstMessageTime = now;
            _results.FirstMessageLatency = now - _startTime;
        }

        // 处理特定消息类型
        switch (frame.Kind)
        {
            case AcpFrameKind.SessionUpdate when frame.SessionUpdateType == "agent_message_chunk":
                _results.RecordMessage("AgentMessageChunk");
                break;

            case AcpFrameKind.SessionUpdate when frame.SessionUpdateType == "agent_thought_chunk":
                _results.RecordMessage("AgentThoughtChunk");
                break;

            case AcpFrameKind.SessionUpdate when frame.SessionUpdateType == "tool_call":
                _results.ToolCallCount++;
                _results.RecordMessage("ToolCall");
                break;

            case AcpFrameKind.SessionUpdate when frame.SessionUpdateType == "tool_call_update":
                _results.ToolResultCount++;
                _results.RecordMessage("ToolResult");
                break;

            case AcpFrameKind.LegacyToolCall:
                _results.ToolCallCount++;
                _results.RecordMessage("LegacyToolCall");
                break;

            case AcpFrameKind.LegacyToolUpdate:
                _results.ToolResultCount++;
                _results.RecordMessage("LegacyToolUpdate");
                break;

            case AcpFrameKind.LegacyTaskFinish:
                _results.RecordMessage("TaskFinish");
                CompleteTest();
                break;

            case AcpFrameKind.Response:
                // 检查是否有 stopReason
                if (frame.Payload.TryGetProperty("result", out var result) &&
                    result.TryGetProperty("stopReason", out var stopReason))
                {
                    _results.RecordMessage($"TaskFinish:{stopReason.GetString()}");
                    CompleteTest();
                }
                break;
        }

        await base.HandleInboundFrameAsync(frame);
    }

    protected override Message? MapFrameToMessage(AcpInboundFrame frame)
    {
        var message = base.MapFrameToMessage(frame);

        if (message is null)
        {
            return null;
        }

        // 计算块间隔
        var now = DateTimeOffset.UtcNow;
        if (_lastChunkTime.HasValue)
        {
            _totalChunkInterval += now - _lastChunkTime.Value;
            _chunkIntervalCount++;
        }
        _lastChunkTime = now;

        switch (message)
        {
            case AssistantMessage assistant:
                return HandleAssistantMessage(assistant);

            case UserMessage user:
                _results.RecordMessage("UserMessage");
                DisplayUserMessage(user);
                break;

            case ToolCallMessage toolCall:
                _results.ToolCallCount++;
                _results.RecordMessage("ToolCall");
                DisplayToolCall(toolCall);
                break;

            case ToolResultMessage toolResult:
                _results.ToolResultCount++;
                _results.RecordMessage("ToolResult");
                DisplayToolResult(toolResult);
                break;

            case ToolConfirmationRequestMessage confirmation:
                _results.PermissionRequestCount++;
                _results.RecordMessage("ToolConfirmationRequest");
                DisplayPermissionRequest(confirmation);
                break;

            case PlanMessage plan:
                _results.RecordMessage("Plan");
                DisplayPlan(plan);
                break;

            case TaskFinishMessage finish:
                _results.RecordMessage($"TaskFinish:{finish.StopReason ?? "unknown"}");
                DisplayTaskFinish(finish);
                CompleteTest();
                break;

            case ErrorMessage error:
                _results.ErrorCount++;
                _results.RecordMessage($"Error:{error.Code}");
                DisplayError(error);
                break;

            default:
                _results.RecordMessage(message.GetType().Name);
                break;
        }

        return message;
    }

    private Message HandleAssistantMessage(AssistantMessage assistant)
    {
        // 记录块统计
        if (!string.IsNullOrWhiteSpace(assistant.Chunk.Text))
        {
            _results.AssistantChunkCount++;
            _results.TotalAssistantTextLength += assistant.Chunk.Text.Length;
            _accumulatedAssistantText += assistant.Chunk.Text;

            // 显示流式输出（单行更新）
            DisplayStreamingText(assistant.Chunk.Text, isThought: false);
        }

        if (!string.IsNullOrWhiteSpace(assistant.Chunk.Thought))
        {
            _results.ThoughtChunkCount++;
            _results.TotalThoughtTextLength += assistant.Chunk.Thought.Length;
            _accumulatedThoughtText += assistant.Chunk.Thought;

            // 显示流式 thought
            DisplayStreamingText(assistant.Chunk.Thought, isThought: true);
        }

        return assistant;
    }

    private void DisplayStreamingText(string text, bool isThought)
    {
        var color = isThought ? ConsoleColor.DarkCyan : ConsoleColor.Green;
        var prefix = isThought ? "💭 " : "🤖 ";

        // 流式显示：输出文本而不换行
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();

        // 检测是否有换行，如果有则输出前缀
        if (text.Contains('\n'))
        {
            var lines = text.Split('\n');
            if (lines.Length > 1)
            {
                Console.Write(prefix);
            }
        }
    }

    private void DisplayUserMessage(UserMessage user)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"\n👤 用户消息: {string.Join("", user.Chunks.Select(c => c.Text))}");
        Console.ResetColor();
    }

    private void DisplayToolCall(ToolCallMessage toolCall)
    {
        // 累积文本已完成，显示为完整段落
        FlushAccumulatedText();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n🔧 工具调用: {toolCall.Label} (ID: {toolCall.Id})");
        if (!string.IsNullOrWhiteSpace(toolCall.ToolName))
        {
            Console.WriteLine($"   工具: {toolCall.ToolName}");
        }
        Console.WriteLine($"   状态: {toolCall.Status}");
        if (toolCall.Args != null && toolCall.Args.Count > 0)
        {
            Console.WriteLine($"   参数: {JsonSerializer.Serialize(toolCall.Args, new JsonSerializerOptions { WriteIndented = false })}");
        }
        Console.ResetColor();
    }

    private void DisplayToolResult(ToolResultMessage toolResult)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"\n📊 工具结果: {toolResult.Id}");
        Console.WriteLine($"   状态: {toolResult.Status}");
        if (!string.IsNullOrWhiteSpace(toolResult.Content?.Markdown))
        {
            Console.WriteLine($"   内容:\n{toolResult.Content.Markdown}");
        }
        Console.ResetColor();
    }

    private void DisplayPermissionRequest(ToolConfirmationRequestMessage confirmation)
    {
        // 累积文本已完成，显示为完整段落
        FlushAccumulatedText();

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n⚠️  权限请求 (ID: {confirmation.RequestId})");
        Console.WriteLine($"   工具: {confirmation.ToolCall.Title}");
        Console.WriteLine($"   选项:");
        foreach (var option in confirmation.Options)
        {
            Console.WriteLine($"     - [{option.Id}] {option.Name}{(!string.IsNullOrWhiteSpace(option.Description) ? $": {option.Description}" : string.Empty)}");
        }
        Console.ResetColor();
    }

    private void DisplayPlan(PlanMessage plan)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n📋 计划:");
        foreach (var entry in plan.Entries)
        {
            var statusIcon = entry.Status switch
            {
                "pending" => "⏳",
                "in_progress" => "🔄",
                "completed" => "✅",
                "failed" => "❌",
                _ => "❓"
            };
            Console.WriteLine($"  {statusIcon} [{entry.Priority}] {entry.Content}");
        }
        Console.ResetColor();
    }

    private void DisplayTaskFinish(TaskFinishMessage finish)
    {
        // 累积文本已完成，显示为完整段落
        FlushAccumulatedText();

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"\n✓ 任务完成: {finish.StopReason ?? "unknown"}");
        Console.ResetColor();

        // 显示完整累积的文本
        if (!string.IsNullOrWhiteSpace(_accumulatedAssistantText))
        {
            Console.WriteLine("\n══ 完整 Assistant 响应 ══");
            Console.WriteLine(_accumulatedAssistantText);
            Console.WriteLine("══════════════════════════════════════════════════════════════\n");
        }

        if (!string.IsNullOrWhiteSpace(_accumulatedThoughtText))
        {
            Console.WriteLine("\n══ 完整 Thought 内容 ══");
            Console.WriteLine(_accumulatedThoughtText);
            Console.WriteLine("══════════════════════════════════════════════════════════════\n");
        }
    }

    private void DisplayError(ErrorMessage error)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n✗ 错误 [{error.Code}]: {error.MessageText}");
        if (!string.IsNullOrWhiteSpace(error.Details))
        {
            Console.WriteLine($"   详情: {error.Details}");
        }
        Console.ResetColor();
    }

    private void FlushAccumulatedText()
    {
        if (!string.IsNullOrWhiteSpace(_accumulatedAssistantText) || !string.IsNullOrWhiteSpace(_accumulatedThoughtText))
        {
            Console.WriteLine();
            _accumulatedAssistantText = string.Empty;
            _accumulatedThoughtText = string.Empty;
        }
    }
}
