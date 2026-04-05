# DoubaoVoice .NET SDK

DoubaoVoice .NET SDK 是一个用于连接豆包语音服务的 .NET 客户端库，支持实时语音转文字功能。

## 快速开始

### 安装

```bash
dotnet add package DoubaoVoice.SDK
```

### 基本使用

```csharp
using DoubaoVoice.SDK;

// 创建配置
var config = new DoubaoVoiceConfig
{
    AppId = "your_app_id",
    AccessToken = "your_access_token",
    ServiceUrl = "wss://openspeech.bytedance.com/api/v2/asr",
    SampleRate = 16000,
    BitsPerSample = 16,
    Channels = 1
};

// 创建客户端
using var client = new DoubaoVoiceClient(config);

// 订阅事件
client.OnConnected += (s, e) => Console.WriteLine("已连接");
client.OnResultReceived += (s, e) => Console.WriteLine(e.Result.Text);
client.OnRecognitionCompleted += (s, e) => Console.WriteLine("识别完成");
client.OnError += (s, e) => Console.WriteLine($"错误: {e.ErrorMessage}");

// 读取音频文件
var audioData = File.ReadAllBytes("audio.wav");

// 开始识别
await client.RecognizeAudioAsync(audioData);
```

## API 参考

### DoubaoVoiceConfig

配置类，用于设置连接和音频参数。

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| AppId | string | - | 应用 ID（必需） |
| AccessToken | string | - | 访问令牌（必需） |
| ServiceUrl | string | wss://openspeech.bytedance.com/api/v2/asr | 服务 URL |
| ResourceId | string | volc.bigasr.sauc.duration | 资源 ID |
| SampleRate | int | 16000 | 采样率 (Hz) |
| BitsPerSample | int | 16 | 位深度 |
| Channels | int | 1 | 声道数 |
| AudioFormat | string | wav | 音频格式 |
| AudioCodec | string | raw | 音频编码 |
| ModelName | string | bigmodel | 模型名称 |
| EnableITN | bool | true | 启用 ITN（逆文本标准化） |
| EnablePunctuation | bool | true | 启用标点 |
| EnableDDC | bool | true | 启用 DDC |
| ShowUtterances | bool | true | 显示语句 |
| EnableNonstream | bool | false | 启用非流模式 |
| Uid | string | demo_uid | 用户 ID |

### DoubaoVoiceClient

主客户端类。

#### 方法

| 方法 | 说明 |
|------|------|
| `ConnectAsync(cancellationToken)` | 建立连接 |
| `SendFullClientRequest(cancellationToken)` | 发送完整客户端请求 |
| `SendAudioSegment(audioData, isLastSegment, cancellationToken)` | 发送音频片段 |
| `ReceiveMessagesAsync(cancellationToken)` | 接收消息 |
| `DisconnectAsync()` | 断开连接 |
| `RecognizeAudioAsync(audioData, segmentDurationMs, cancellationToken)` | 识别音频文件 |
| `RecognizeStreamAsync(audioStream, segmentDurationMs, cancellationToken)` | 识别音频流 |

#### 事件

| 事件 | 参数 | 说明 |
|------|------|------|
| OnConnected | ConnectedEventArgs | 连接成功时触发 |
| OnDisconnected | DisconnectedEventArgs | 连接断开时触发 |
| OnError | ErrorEventArgs | 发生错误时触发 |
| OnResultReceived | ResultReceivedEventArgs | 收到识别结果时触发 |
| OnRecognitionCompleted | RecognitionCompletedEventArgs | 识别完成时触发 |

### 音频处理

#### WavParser

```csharp
// 解析 WAV 文件
var wavInfo = WavParser.ReadWavInfo(wavData);
Console.WriteLine($"采样率: {wavInfo.SampleRate}, 声道数: {wavInfo.Channels}");

// 提取纯音频数据
var audioData = WavParser.ExtractAudioData(wavData);
```

#### AudioSegmenter

```csharp
// 将音频分段
var segments = AudioSegmenter.SegmentAudio(
    audioData,
    sampleRate: 16000,
    channels: 1,
    bitsPerSample: 16,
    segmentDurationMs: 200
);
```

## 音频格式要求

- **格式**: WAV (PCM)
- **采样率**: 16000 Hz 或 8000 Hz
- **位深度**: 16-bit
- **声道数**: 单声道 (mono)

## 异常处理

| 异常类型 | 说明 |
|----------|------|
| `DoubaoVoiceException` | SDK 基础异常 |
| `AuthenticationException` | 认证失败异常 |
| `InvalidAudioFormatException` | 音频格式异常 |
| `ConnectionException` | 连接异常 |

## 示例

### 实时识别

```csharp
using var client = new DoubaoVoiceClient(config);
await client.ConnectAsync();
await client.SendFullClientRequest();

// 启动消息接收
var receiveTask = client.ReceiveMessagesAsync();

// 持续发送音频片段
foreach (var segment in audioSegments)
{
    await client.SendAudioSegment(segment, isLast: false);
    await Task.Delay(200);
}

// 发送结束标记
await client.SendAudioSegment(Array.Empty<byte>(), isLast: true);
await receiveTask;
```

### 处理错误

```csharp
client.OnError += (s, e) =>
{
    if (e.IsAuthenticationError)
    {
        Console.WriteLine("认证失败，请检查 App ID 和 Access Token");
    }
    else
    {
        Console.WriteLine($"错误: {e.ErrorMessage}");
        if (e.Exception != null)
        {
            Console.WriteLine($"异常: {e.Exception}");
        }
    }
};
```

## 系统要求

- .NET 8.0 或更高版本
- 支持 Windows、Linux、macOS

## 许可证

MIT License