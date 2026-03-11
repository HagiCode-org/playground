using IFlowSdk.Client;
using IFlowSdk.Models;

namespace IFlowSdk.StreamingTest;

/// <summary>
/// 流式输出和 ACP 对接测试程序
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  iFlow C# SDK - 流式输出 & ACP 对接测试程序                     ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var settings = TestSettings.Load(args);

        // 创建客户端，启用原始消息捕获
        await using var client = new StreamingTestClient(settings.ToOptions());

        Console.WriteLine($"测试配置:");
        Console.WriteLine($"  模式: {(string.IsNullOrWhiteSpace(settings.Url) ? "自动启动 CLI" : $"连接到 {settings.Url}")}");
        Console.WriteLine($"  工作目录: {settings.Cwd}");
        Console.WriteLine($"  超时: {settings.Timeout.TotalSeconds} 秒");
        Console.WriteLine($"  测试提示词: {settings.TestPrompt ?? "（交互模式）"}");
        Console.WriteLine();

        try
        {
            Console.Write("正在连接...");
            await client.ConnectAsync();
            Console.WriteLine(" ✓ 已连接");
            Console.WriteLine();

            PrintConnectionInfo(client);
            Console.WriteLine();

            // 运行测试
            if (!string.IsNullOrWhiteSpace(settings.TestPrompt))
            {
                await RunAutomatedTest(client, settings.TestPrompt);
            }
            else
            {
                await RunInteractiveTest(client);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine($"✗ 错误: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  内部错误: {ex.InnerException.Message}");
            }
            Console.ResetColor();
        }
        finally
        {
            Console.WriteLine();
            Console.Write("正在断开连接...");
            await client.DisposeAsync();
            Console.WriteLine(" ✓ 已断开");
        }

        // 打印测试结果
        PrintTestResults(client.GetTestResults());
    }

    static void PrintConnectionInfo(StreamingTestClient client)
    {
        Console.WriteLine("══ 连接信息 ══");
        Console.WriteLine($"  Session ID: {client.SessionId ?? "N/A"}");
        Console.WriteLine($"  连接 URL: {client.ConnectionUrl ?? "N/A"}");
        Console.WriteLine($"  进程 ID: {client.ProcessId?.ToString() ?? "外部进程"}");
        Console.WriteLine($"  拥有进程: {client.OwnsProcess}");
        Console.WriteLine($"  ═══════════════════════════════════════════════════════════════");
    }

    static async Task RunAutomatedTest(StreamingTestClient client, string prompt)
    {
        Console.WriteLine("══ 自动化测试模式 ══");
        Console.WriteLine($"发送测试提示词: {prompt}");
        Console.WriteLine();

        await client.SendMessageAsync(prompt);

        // 等待任务完成或超时
        await client.WaitForCompletionAsync(TimeSpan.FromMinutes(2));
    }

    static async Task RunInteractiveTest(StreamingTestClient client)
    {
        Console.WriteLine("══ 交互测试模式 ══");
        Console.WriteLine("命令:");
        Console.WriteLine("  /test <text>      - 发送测试提示词");
        Console.WriteLine("  /interrupt         - 中断当前请求");
        Console.WriteLine("  /status           - 显示连接状态");
        Console.WriteLine("  /raw              - 切换原始消息显示");
        Console.WriteLine("  /results          - 显示测试结果");
        Console.WriteLine("  /exit             - 退出");
        Console.WriteLine();

        bool showRaw = false;

        while (true)
        {
            Console.Write("test> ");
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (string.Equals(line.Trim(), "/exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            try
            {
                if (line.StartsWith("/test ", StringComparison.OrdinalIgnoreCase))
                {
                    var prompt = line[6..].Trim();
                    Console.WriteLine($"发送: {prompt}");
                    await client.SendMessageAsync(prompt);
                    continue;
                }

                if (string.Equals(line.Trim(), "/interrupt", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("发送中断请求...");
                    await client.InterruptAsync();
                    Console.WriteLine("✓ 已中断");
                    continue;
                }

                if (string.Equals(line.Trim(), "/status", StringComparison.OrdinalIgnoreCase))
                {
                    PrintConnectionInfo(client);
                    continue;
                }

                if (string.Equals(line.Trim(), "/raw", StringComparison.OrdinalIgnoreCase))
                {
                    showRaw = !showRaw;
                    client.ShowRawMessages = showRaw;
                    Console.WriteLine($"原始消息显示: {(showRaw ? "启用" : "禁用")}");
                    continue;
                }

                if (string.Equals(line.Trim(), "/results", StringComparison.OrdinalIgnoreCase))
                {
                    PrintTestResults(client.GetTestResults());
                    continue;
                }

                // 直接发送文本
                await client.SendMessageAsync(line.Trim());
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ 错误: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    static void PrintTestResults(TestResults results)
    {
        Console.WriteLine();
        Console.WriteLine("══ 测试结果 ══");
        Console.WriteLine($"  消息类型计数:");
        foreach (var kvp in results.MessageTypeCounts.OrderByDescending(x => x.Value))
        {
            Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
        }

        Console.WriteLine();
        Console.WriteLine($"  流式文本聚合:");
        Console.WriteLine($"    Assistant 块数: {results.AssistantChunkCount}");
        Console.WriteLine($"    Assistant 总文本长度: {results.TotalAssistantTextLength} 字符");
        Console.WriteLine($"    Thought 块数: {results.ThoughtChunkCount}");
        Console.WriteLine($"    Thought 总文本长度: {results.TotalThoughtTextLength} 字符");

        Console.WriteLine();
        Console.WriteLine($"  ACP 协议消息:");
        Console.WriteLine($"    原始消息总数: {results.RawMessageCount}");
        Console.WriteLine($"    JSON-RPC 响应: {results.JsonRpcResponseCount}");
        Console.WriteLine($"    Session Update: {results.SessionUpdateCount}");
        Console.WriteLine($"    Tool Call: {results.ToolCallCount}");
        Console.WriteLine($"    Tool Result: {results.ToolResultCount}");
        Console.WriteLine($"    Permission Request: {results.PermissionRequestCount}");

        Console.WriteLine();
        Console.WriteLine($"  时序统计:");
        Console.WriteLine($"    首个消息延迟: {results.FirstMessageLatency?.TotalMilliseconds ?? 0:F2} ms");
        Console.WriteLine($"    平均块间隔: {results.AverageChunkInterval?.TotalMilliseconds ?? 0:F2} ms");
        Console.WriteLine($"    总测试时间: {results.TotalTestDuration.TotalSeconds:F2} 秒");

        if (results.ErrorCount > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  错误数: {results.ErrorCount}");
            Console.ResetColor();
        }

        Console.WriteLine($"  ═══════════════════════════════════════════════════════════════");
    }
}

/// <summary>
/// 测试配置设置
/// </summary>
internal sealed class TestSettings
{
    public string? Url { get; init; }

    public string Cwd { get; init; } = Directory.GetCurrentDirectory();

    public string? ExecutablePath { get; init; }

    public int ProcessStartPort { get; init; } = 8090;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public string? AuthMethodId { get; init; }

    public string? TestPrompt { get; init; }

    public static TestSettings Load(string[] args)
    {
        var timeoutSeconds = Environment.GetEnvironmentVariable("IFLOW_TIMEOUT_SECONDS");
        return new TestSettings
        {
            Url = GetArg(args, "--url") ?? Environment.GetEnvironmentVariable("IFLOW_URL"),
            Cwd = GetArg(args, "--cwd") ?? Environment.GetEnvironmentVariable("IFLOW_CWD") ?? Directory.GetCurrentDirectory(),
            ExecutablePath = GetArg(args, "--executable") ?? Environment.GetEnvironmentVariable("IFLOW_EXECUTABLE"),
            AuthMethodId = GetArg(args, "--auth-method") ?? Environment.GetEnvironmentVariable("IFLOW_AUTH_METHOD_ID"),
            TestPrompt = GetArg(args, "--test") ?? Environment.GetEnvironmentVariable("IFLOW_TEST_PROMPT"),
            ProcessStartPort = int.TryParse(GetArg(args, "--port") ?? Environment.GetEnvironmentVariable("IFLOW_START_PORT"), out var port) ? port : 8090,
            Timeout = int.TryParse(timeoutSeconds, out var seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.FromSeconds(30),
        };
    }

    static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    public IFlowOptions ToOptions()
    {
        return new IFlowOptions
        {
            Url = Url,
            Cwd = Cwd,
            ExecutablePath = ExecutablePath,
            ProcessStartPort = ProcessStartPort,
            Timeout = Timeout,
            AuthMethodId = AuthMethodId,
            AutoStartProcess = string.IsNullOrWhiteSpace(Url),
        };
    }
}

/// <summary>
/// 测试结果统计
/// </summary>
internal sealed class TestResults
{
    public Dictionary<string, int> MessageTypeCounts { get; } = new();
    public int AssistantChunkCount { get; set; }
    public int ThoughtChunkCount { get; set; }
    public int TotalAssistantTextLength { get; set; }
    public int TotalThoughtTextLength { get; set; }
    public int RawMessageCount { get; set; }
    public int JsonRpcResponseCount { get; set; }
    public int SessionUpdateCount { get; set; }
    public int ToolCallCount { get; set; }
    public int ToolResultCount { get; set; }
    public int PermissionRequestCount { get; set; }
    public int ErrorCount { get; set; }
    public TimeSpan? FirstMessageLatency { get; set; }
    public TimeSpan? AverageChunkInterval { get; set; }
    public TimeSpan TotalTestDuration { get; set; }

    public void RecordMessage(string messageType)
    {
        if (MessageTypeCounts.ContainsKey(messageType))
        {
            MessageTypeCounts[messageType]++;
        }
        else
        {
            MessageTypeCounts[messageType] = 1;
        }
    }
}
