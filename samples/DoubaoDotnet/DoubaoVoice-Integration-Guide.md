# DoubaoVoice 语音识别服务对接指南

## 目录

- [1. 概述](#1-概述)
- [2. 系统架构](#2-系统架构)
- [3. 快速开始](#3-快速开始)
- [4. 前端接入指南](#4-前端接入指南)
- [5. 后端接入指南](#5-后端接入指南)
- [6. WebSocket 协议](#6-websocket-协议)
- [7. 配置参数](#7-配置参数)
- [8. 错误处理](#8-错误处理)
- [9. SDK 使用](#9-sdk-使用)
- [10. 完整示例](#10-完整示例)
- [11. 常见问题](#11-常见问题)

---

## 1. 概述

DoubaoVoice 语音识别服务提供实时语音转文字（ASR）功能，支持流式识别，适用于语音助手、会议记录、实时字幕等场景。

### 核心特性

- ✅ 实时流式语音识别
- ✅ 支持中文识别
- ✅ 16kHz/8kHz 采样率
- ✅ 16-bit PCM 音频格式
- ✅ WebSocket 双向通信
- ✅ 完整的事件通知机制

---

## 2. 系统架构

```
┌─────────────┐         ┌─────────────┐         ┌─────────────┐
│  前端客户端   │────────▶│  WebProxy   │────────▶│  豆包 API     │
│ (Web/App)   │  WebSocket │ (中转服务)  │  WebSocket   │
└─────────────┘         └─────────────┘         └─────────────┘
                                ▲                         ▲
                                │                         │
                           音频录制                    认证+识别结果
```

### 组件说明

| 组件 | 说明 |
|------|------|
| **前端客户端** | 负责音频采集、WebSocket 连接、结果展示 |
| **WebProxy** | WebSocket 中转服务，处理认证、音频转发 |
| **Doubao API** | 字节跳动豆包语音识别 API |
| **SDK** | .NET SDK 用于 Doubao API 通信 |

---

## 3. 快速开始

### 3.1 前端快速接入

```html
<!DOCTYPE html>
<html>
<head>
    <title>DoubaoVoice 语音识别示例</title>
</head>
<body>
    <button onclick="startRecognition()">开始识别</button>
    <button onclick="stopRecognition()">停止识别</button>
    <div id="result"></div>

    <script>
        let ws = null;

        function connect() {
            const url = new URL('ws://localhost:5000/ws');
            url.searchParams.set('appId', 'your-app-id');
            url.searchParams.set('accessToken', 'your-access-token');

            ws = new WebSocket(url);

            ws.onopen = () => console.log('Connected');
            ws.onmessage = (event) => {
                const message = JSON.parse(event.data);
                if (message.type === 'result') {
                    document.getElementById('result').textContent =
                        message.payload.text;
                }
            };
        }

        async function startRecognition() {
            ws.send(JSON.stringify({
                type: 'control',
                payload: { command: 'StartRecognition' }
            }));

            // 获取麦克风并发送音频
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            // ... 音频处理代码
        }

        function stopRecognition() {
            ws.send(JSON.stringify({
                type: 'control',
                payload: { command: 'EndRecognition' }
            }));
        }

        connect();
    </script>
</body>
</html>
```

### 3.2 后端快速接入

```bash
# 克隆仓库
git clone <repository>
cd samples/DoubaoDotnet/DoubaoVoice.WebProxy

# 修改 appsettings.json 中的配置
nano appsettings.json

# 启动服务
dotnet run
```

---

## 4. 前端接入指南

### 4.1 WebSocket 连接

#### 连接 URL 格式

```
ws://host:port/ws?appId={appId}&accessToken={accessToken}&sampleRate=16000&bitsPerSample=16&channels=1
```

#### 连接参数

| 参数 | 必填 | 说明 | 默认值 |
|------|------|------|--------|
| `appId` | ✅ | 豆包应用 ID | - |
| `accessToken` | ✅ | 访问令牌 | - |
| `serviceUrl` | ❌ | API 服务地址 | wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async |
| `resourceId` | ❌ | 资源 ID | volc.bigasr.sauc.duration |
| `sampleRate` | ❌ | 采样率 (Hz) | 16000 |
| `bitsPerSample` | ❌ | 采样深度 | 16 |
| `channels` | ❌ | 声道数 | 1 |

#### 连接示例

```javascript
const ws = new WebSocket('ws://localhost:5000/ws?appId=xxx&accessToken=xxx');

ws.onopen = () => {
    console.log('WebSocket 已连接');
};

ws.onmessage = (event) => {
    const message = JSON.parse(event.data);
    handleMessage(message);
};

ws.onerror = (error) => {
    console.error('WebSocket 错误:', error);
};

ws.onclose = (event) => {
    console.log('WebSocket 已关闭:', event.code, event.reason);
};
```

### 4.2 消息协议

#### 4.2.1 控制消息（前端 → 后端）

**开始识别**
```json
{
    "type": "control",
    "messageId": "msg_123",
    "timestamp": "2026-03-03T10:00:00Z",
    "payload": {
        "command": "StartRecognition",
        "parameters": {}
    }
}
```

**停止识别**
```json
{
    "type": "control",
    "messageId": "msg_124",
    "timestamp": "2026-03-03T10:00:01Z",
    "payload": {
        "command": "EndRecognition",
        "parameters": {}
    }
}
```

#### 4.2.2 音频数据（前端 → 后端）

使用 `WebSocket.send()` 发送二进制 PCM 数据：

```javascript
// 16-bit PCM 数据
const pcmData = new Int16Array(8000); // 500ms @ 16kHz
// ... 填充音频数据

// 发送二进制数据
ws.send(pcmData.buffer);
```

**音频格式要求**

| 参数 | 值 |
|------|-----|
| 采样率 | 16000 Hz (推荐) 或 8000 Hz |
| 位深度 | 16-bit |
| 声道 | 单声道 (mono) |
| 编码 | PCM (raw) |

#### 4.2.3 识别结果（后端 → 前端）

**状态消息**
```json
{
    "type": "status",
    "messageId": "msg_125",
    "timestamp": "2026-03-03T10:00:02Z",
    "payload": {
        "status": "recognizing",
        "sessionId": "session-uuid"
    }
}
```

状态值：`connected`、`recognizing`、`paused`、`closed`

**识别结果**
```json
{
    "type": "result",
    "messageId": "msg_126",
    "timestamp": "2026-03-03T10:00:03Z",
    "payload": {
        "text": "你好世界",
        "confidence": 0.95,
        "duration": 1500,
        "isFinal": true,
        "utterances": [
            {
                "text": "你好",
                "startTime": 0,
                "endTime": 800,
                "definite": true
            },
            {
                "text": "世界",
                "startTime": 900,
                "endTime": 1500,
                "definite": true,
                "words": [
                    { "text": "世", "startTime": 900, "endTime": 1100 },
                    { "text": "界", "startTime": 1100, "endTime": 1500 }
                ]
            }
        ]
    }
}
```

### 4.3 音频采集

#### 使用 AudioWorkletNode（推荐）

```javascript
// 1. 创建 AudioWorklet 文件: audio-worklet.js
class AudioProcessorWorklet extends AudioWorkletProcessor {
    process(inputs, outputs, parameters) {
        const input = inputs[0]?.[0];
        if (!input) return true;

        // 转换为 16-bit PCM
        const pcm = new Int16Array(input.length);
        for (let i = 0; i < input.length; i++) {
            pcm[i] = Math.max(-32768, Math.min(32767, input[i] * 32767));
        }

        this.port.postMessage({
            type: 'audioData',
            data: pcm.buffer
        }, [pcm.buffer]);

        return true;
    }
}

registerProcessor('audio-processor', AudioProcessorWorklet);

// 2. 主线程代码
async function startAudioRecording() {
    const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
            echoCancellation: true,
            noiseSuppression: true,
            autoGainControl: true,
            sampleRate: 48000
        }
    });

    const audioContext = new AudioContext();
    const audioSource = audioContext.createMediaStreamSource(stream);

    await audioContext.audioWorklet.addModule('/audio-worklet.js');
    const audioWorkletNode = new AudioWorkletNode(audioContext, 'audio-processor');

    audioWorkletNode.port.onmessage = (event) => {
        if (event.data.type === 'audioData' && ws?.readyState === WebSocket.OPEN) {
            ws.send(event.data.data);
        }
    };

    audioSource.connect(audioWorkletNode);
}
```

#### 使用 MediaRecorder（备用方案）

```javascript
async function startAudioRecording() {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

    const mediaRecorder = new MediaRecorder(stream, {
        mimeType: 'audio/webm;codecs=opus'
    });

    const audioChunks = [];

    mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
            audioChunks.push(event.data);
        }
    };

    mediaRecorder.start(100); // 每100ms触发一次

    // 发送音频
    setInterval(() => {
        if (audioChunks.length > 0 && ws?.readyState === WebSocket.OPEN) {
            const chunk = audioChunks.shift();
            ws.send(chunk);
        }
    }, 100);
}
```

### 4.4 完整前端示例

```javascript
class DoubaoVoiceClient {
    constructor(config) {
        this.config = config;
        this.ws = null;
        this.mediaStream = null;
        this.audioContext = null;
        this.audioWorkletNode = null;
    }

    async connect() {
        const url = new URL(this.config.wsUrl);
        Object.entries(this.config.params).forEach(([key, value]) => {
            url.searchParams.set(key, value);
        });

        this.ws = new WebSocket(url);

        return new Promise((resolve, reject) => {
            this.ws.onopen = () => {
                console.log('[DoubaoVoice] Connected');
                resolve();
            };

            this.ws.onmessage = (event) => {
                this._handleMessage(JSON.parse(event.data));
            };

            this.ws.onerror = reject;
            this.ws.onclose = () => {
                console.log('[DoubaoVoice] Disconnected');
            };
        });
    }

    async startRecording(onResult) {
        // 获取麦克风
        this.mediaStream = await navigator.mediaDevices.getUserMedia({
            audio: {
                echoCancellation: true,
                noiseSuppression: true,
                autoGainControl: true
            }
        });

        // 创建音频处理
        this.audioContext = new AudioContext();
        const audioSource = this.audioContext.createMediaStreamSource(this.mediaStream);

        await this.audioContext.audioWorklet.addModule('/audio-worklet.js');
        this.audioWorkletNode = new AudioWorkletNode(this.audioContext, 'audio-processor');

        // 处理音频数据
        this.audioWorkletNode.port.onmessage = (event) => {
            if (event.data.type === 'audioData' && this.ws?.readyState === WebSocket.OPEN) {
                this.ws.send(event.data.data);
            }
        };

        audioSource.connect(this.audioWorkletNode);
        this.onResult = onResult;

        // 发送开始识别命令
        this._sendCommand('StartRecognition');
    }

    stopRecording() {
        this._sendCommand('EndRecognition');

        if (this.audioWorkletNode) {
            this.audioWorkletNode.disconnect();
            this.audioWorkletNode = null;
        }

        if (this.audioSource) {
            this.audioSource.disconnect();
            this.audioSource = null;
        }

        if (this.audioContext) {
            this.audioContext.close();
            this.audioContext = null;
        }

        if (this.mediaStream) {
            this.mediaStream.getTracks().forEach(track => track.stop());
            this.mediaStream = null;
        }
    }

    disconnect() {
        this.ws?.close();
    }

    _handleMessage(message) {
        switch (message.type) {
            case 'status':
                this._handleStatus(message.payload);
                break;
            case 'result':
                if (this.onResult) {
                    this.onResult(message.payload);
                }
                break;
            case 'error':
                console.error('[DoubaoVoice] Error:', message.payload);
                break;
        }
    }

    _sendCommand(command, params = {}) {
        this.ws.send(JSON.stringify({
            type: 'control',
            messageId: this._generateMessageId(),
            timestamp: new Date().toISOString(),
            payload: { command, parameters: params }
        }));
    }

    _generateMessageId() {
        return 'msg_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    }
}

// 使用示例
const client = new DoubaoVoiceClient({
    wsUrl: 'ws://localhost:5000/ws',
    params: {
        appId: 'your-app-id',
        accessToken: 'your-access-token',
        sampleRate: 16000
    }
});

await client.connect();
await client.startRecording((result) => {
    console.log('识别结果:', result.text);
});
```

---

## 5. 后端接入指南

### 5.1 使用 WebProxy 服务

#### 5.1.1 启动 WebProxy

```bash
cd DoubaoVoice.WebProxy
dotnet run
```

#### 5.1.2 配置 appsettings.json

```json
{
  "DoubaoProxy": {
    "AppId": "your-app-id",
    "AccessToken": "your-access-token",
    "ServiceUrl": "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async",
    "ResourceId": "volc.bigasr.sauc.duration",
    "SampleRate": 16000,
    "BitsPerSample": 16,
    "Channels": 1,
    "AudioFormat": "wav",
    "AudioCodec": "raw",
    "ModelName": "bigmodel"
  }
}
```

#### 5.1.3 访问服务

- **WebSocket 端点**: `ws://localhost:5000/ws`
- **静态文件**: `http://localhost:5000/index.html`
- **健康检查**: `http://localhost:5000/`

### 5.2 直接使用 SDK

```csharp
using DoubaoVoice.SDK;

// 创建配置
var config = new DoubaoVoiceConfig
{
    AppId = "your-app-id",
    AccessToken = "your-access-token",
    Did = Guid.NewGuid().ToString("N"),
    ServiceUrl = "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async",
    ResourceId = "volc.bigasr.sauc.duration",
    SampleRate = 16000,
    BitsPerSample = 16,
    Channels = 1,
    ModelName = "bigmodel"
};

// 创建客户端
using var client = new DoubaoVoiceClient(config);

// 订阅事件
client.OnResultReceived += (s, e) => {
    Console.WriteLine($"识别结果: {e.Result.Text}");
};

client.OnError += (s, e) => {
    Console.WriteLine($"错误: {e.ErrorMessage}");
};

// 连接并开始识别
await client.ConnectAsync();
await client.SendFullClientRequest();

// 启动接收任务
var receiveTask = Task.Run(() => client.ReceiveMessagesAsync());

// 发送音频数据
var audioData = File.ReadAllBytes("audio.pcm");
await client.SendAudioSegment(audioData, false);

// 发送结束标记
await client.SendAudioSegment(Array.Empty<byte>(), true);

await receiveTask;
await client.DisconnectAsync();
```

---

## 6. WebSocket 协议

### 6.1 消息结构

所有消息遵循以下结构：

```json
{
    "type": "消息类型",
    "messageId": "消息ID",
    "timestamp": "ISO 8601 时间戳",
    "payload": {}
}
```

### 6.2 消息类型

| 类型 | 方向 | 说明 |
|------|------|------|
| `control` | 前端 → 后端 | 控制命令（开始/停止识别） |
| `status` | 后端 → 前端 | 状态通知 |
| `result` | 后端 → 前端 | 识别结果 |
| `error` | 后端 → 前端 | 错误通知 |
| `audio` | 前端 → 后端 | 二进制音频数据 |

### 6.3 控制命令

| 命令 | 参数 | 说明 |
|------|------|------|
| `StartRecognition` | 无 | 开始语音识别 |
| `EndRecognition` | 无 | 结束语音识别 |
| `PauseRecognition` | 无 | 暂停识别 |

### 6.4 状态值

| 状态 | 说明 |
|------|------|
| `connected` | WebSocket 已连接，会话已创建 |
| `recognizing` | 正在进行语音识别 |
| `paused` | 识别已暂停 |
| `closed` | 识别已结束 |

---

## 7. 配置参数

### 7.1 音频参数

| 参数 | 类型 | 说明 | 范围 |
|------|------|------|------|
| `sampleRate` | int | 采样率 | 8000, 16000 |
| `bitsPerSample` | int | 采样深度 | 16 |
| `channels` | int | 声道数 | 1 |
| `audioFormat` | string | 音频格式 | wav |
| `audioCodec` | string | 音频编码 | raw |

### 7.2 认证参数

| 参数 | 类型 | 说明 |
|------|------|------|
| `appId` | string | 豆包应用 ID |
| `accessToken` | string | 访问令牌 |
| `did` | string | 设备 ID（自动生成） |
| `uid` | string | 用户 ID |

### 7.3 识别参数

| 参数 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| `modelName` | string | 模型名称 | bigmodel |
| `enableITN` | bool | ITN（数字格式化） | true |
| `enablePunctuation` | bool | 标点符号 | true |
| `enableDDC` | bool | DDC | true |
| `showUtterances` | bool | 显示分词 | true |

### 7.4 热词参数（可选）

| 参数 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| `hotwordId` | string | 热词配置ID | 无 |

### 7.5 热词配置

热词配置用于提高特定领域词汇的识别准确率。通过在前端发送 `hotwordId` 参数，WebProxy 会自动加载对应的热词上下文并传递给豆包 API。

#### 7.5.1 配置热词文件

在 `WebProxy` 目录下创建或编辑 `hotwords.json` 文件：

```json
{
  "hotwords": [
    {
      "id": "medical",
      "name": "医疗术语",
      "description": "常见医疗术语",
      "contexts": [
        "高血压糖尿病冠心病",
        "肺炎支气管炎肺结核"
      ]
    },
    {
      "id": "tech",
      "name": "科技术语",
      "description": "常见科技术语",
      "contexts": [
        "人工智能机器学习深度学习",
        "云计算大数据物联网"
      ]
    }
  ]
}
```

#### 7.5.2 使用热词

在前端发送 `StartRecognition` 命令时，在 `parameters` 中指定 `hotwordId`：

```javascript
// 开始识别时指定热词
ws.send(JSON.stringify({
    type: 'control',
    messageId: 'msg_' + Date.now(),
    timestamp: new Date().toISOString(),
    payload: {
        command: 'StartRecognition',
        parameters: {
            hotwordId: 'medical'  // 使用医疗术语热词
        }
    }
}));

// 或者使用科技术语热词
ws.send(JSON.stringify({
    type: 'control',
    messageId: 'msg_' + Date.now(),
    timestamp: new Date().toISOString(),
    payload: {
        command: 'StartRecognition',
        parameters: {
            hotwordId: 'tech'
        }
    }
}));

// 不使用热词（默认）
ws.send(JSON.stringify({
    type: 'control',
    messageId: 'msg_' + Date.now(),
    timestamp: new Date().toISOString(),
    payload: {
        command: 'StartRecognition',
        parameters: {}
    }
}));
```

#### 7.5.3 SDK 中使用热词

如果直接使用 SDK，可以在配置中设置 `HotwordContexts`：

```csharp
var config = new DoubaoVoiceConfig
{
    AppId = "your-app-id",
    AccessToken = "your-access-token",
    // ... 其他配置
    HotwordContexts = new List<string>
    {
        "高血压糖尿病冠心病",
        "人工智能机器学习"
    }
};

using var client = new DoubaoVoiceClient(config);
```

#### 7.5.4 注意事项

1. **热词内容**：热词应该是连续的无空格文本，如"高血压糖尿病"而非"高血压 糖尿病"
2. **热词长度**：建议单个热词不超过 20 个字符，总热词内容不超过 2000 字符
3. **配置生效**：修改 `hotwords.json` 后需要重启 WebProxy 服务
4. **无效热词 ID**：如果指定的 `hotwordId` 不存在，服务会记录警告并继续使用空热词进行识别

---

## 8. 错误处理

### 8.1 WebSocket 错误

| 错误代码 | 说明 | 处理方式 |
|----------|------|----------|
| 1000 | 连接失败 | 检查网络和 URL |
| 1001 | 认证失败 | 检查 AppId 和 Token |
| 1002 | 参数缺失 | 检查必需参数 |
| 1003 | 会话不存在 | 重新连接 |

### 8.2 音频错误

| 错误类型 | 说明 | 处理方式 |
|----------|------|----------|
| `WebSocketException` | WebSocket 连接断开 | 重新连接 |
| `IOException` | 数据发送失败 | 检查连接状态 |
| `ArgumentException` | 音频格式错误 | 转换音频格式 |

### 8.3 豆包 API 错误

| 错误码 | 说明 | 解决方案 |
|--------|------|----------|
| 401 | 认证失败 | 检查 AppId 和 Token |
| 403 | 权限不足 | 检查 API 权限 |
| 429 | 请求过多 | 降低请求频率 |
| 500 | 服务器错误 | 稍后重试 |

---

## 9. SDK 使用

### 9.1 安装 SDK

```bash
dotnet add package DoubaoVoice.SDK
```

### 9.2 核心类

```csharp
// 客户端类
public class DoubaoVoiceClient : IDisposable

// 配置类
public class DoubaoVoiceConfig

// 事件参数
public class ResultReceivedEventArgs
public class ErrorEventArgs
public class ConnectedEventArgs
public class DisconnectedEventArgs
public class RecognitionCompletedEventArgs
```

### 9.3 事件订阅

```csharp
var client = new DoubaoVoiceClient(config);

// 连接事件
client.OnConnected += (s, e) => Console.WriteLine("Connected");

// 断开事件
client.OnDisconnected += (s, e) => Console.WriteLine($"Disconnected: {e.Reason}");

// 结果事件
client.OnResultReceived += (s, e) => {
    Console.WriteLine($"Result: {e.Result.Text}");
    Console.WriteLine($"Is Final: {e.IsFinal}");
};

// 错误事件
client.OnError += (s, e) => {
    Console.WriteLine($"Error: {e.ErrorMessage}");
    if (e.Exception != null) {
        Console.WriteLine($"Exception: {e.Exception.Message}");
    }
};

// 完成事件
client.OnRecognitionCompleted += (s, e) => {
    Console.WriteLine($"Completed: {e.Result.Text}");
    Console.WriteLine($"Total Segments: {e.TotalSegments}");
};
```

---

## 10. 完整示例

### 10.1 简单的 HTML 示例

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>DoubaoVoice 语音识别</title>
</head>
<body>
    <h1>语音识别</h1>

    <div>
        <button id="btnConnect">连接</button>
        <button id="btnDisconnect" disabled>断开</button>
    </div>

    <div>
        <button id="btnStart" disabled>开始识别</button>
        <button id="btnStop" disabled>停止识别</button>
    </div>

    <div>
        <p>识别结果:</p>
        <pre id="result"></pre>
    </div>

    <script>
        const ws = new WebSocket('ws://localhost:5000/ws?appId=xxx&accessToken=xxx');

        const btnConnect = document.getElementById('btnConnect');
        const btnDisconnect = document.getElementById('btnDisconnect');
        const btnStart = document.getElementById('btnStart');
        const btnStop = document.getElementById('btnStop');
        const result = document.getElementById('result');

        let audioContext = null;
        let audioWorkletNode = null;

        ws.onopen = () => {
            btnConnect.disabled = true;
            btnDisconnect.disabled = false;
            btnStart.disabled = false;
            console.log('已连接');
        };

        ws.onmessage = (event) => {
            const msg = JSON.parse(event.data);

            if (msg.type === 'result') {
                result.textContent = msg.payload.text;
            } else if (msg.type === 'status') {
                console.log('状态:', msg.payload.status);
                if (msg.payload.status === 'recognizing') {
                    btnStart.disabled = true;
                    btnStop.disabled = false;
                } else if (msg.payload.status === 'closed') {
                    btnStart.disabled = false;
                    btnStop.disabled = true;
                }
            }
        };

        ws.onclose = () => {
            btnConnect.disabled = false;
            btnDisconnect.disabled = true;
            btnStart.disabled = true;
            btnStop.disabled = true;
        };

        btnDisconnect.onclick = () => ws.close();

        btnStart.onclick = async () => {
            ws.send(JSON.stringify({
                type: 'control',
                messageId: 'msg_' + Date.now(),
                timestamp: new Date().toISOString(),
                payload: { command: 'StartRecognition' }
            }));

            // 启动音频录制
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

            audioContext = new AudioContext();
            const audioSource = audioContext.createMediaStreamSource(stream);

            await audioContext.audioWorklet.addModule('/audio-worklet.js');
            audioWorkletNode = new AudioWorkletNode(audioContext, 'audio-processor');

            audioWorkletNode.port.onmessage = (event) => {
                if (event.data.type === 'audioData' && ws.readyState === WebSocket.OPEN) {
                    ws.send(event.data.data);
                }
            };

            audioSource.connect(audioWorkletNode);
        };

        btnStop.onclick = () => {
            ws.send(JSON.stringify({
                type: 'control',
                messageId: 'msg_' + Date.now(),
                timestamp: new Date().toISOString(),
                payload: { command: 'EndRecognition' }
            }));

            if (audioWorkletNode) {
                audioWorkletNode.disconnect();
                audioWorkletNode = null;
            }

            if (audioSource) {
                audioSource.disconnect();
                audioSource = null;
            }

            if (audioContext) {
                audioContext.close();
                audioContext = null;
            }
        };
    </script>
</body>
</html>
```

### 10.2 React 示例

```tsx
import React, { useState, useEffect, useRef } from 'react';

export default function VoiceRecognition() {
    const [isConnected, setIsConnected] = useState(false);
    const [isRecognizing, setIsRecognizing] = useState(false);
    const [result, setResult] = useState('');

    const wsRef = useRef<WebSocket | null>(null);
    const mediaStreamRef = useRef<MediaStream | null>(null);

    useEffect(() => {
        connect();
        return () => {
            wsRef.current?.close();
            mediaStreamRef.current?.getTracks().forEach(track => track.stop());
        };
    }, []);

    const connect = () => {
        const url = new URL('ws://localhost:5000/ws');
        url.searchParams.set('appId', 'your-app-id');
        url.searchParams.set('accessToken', 'your-access-token');

        wsRef.current = new WebSocket(url);

        wsRef.current.onopen = () => setIsConnected(true);
        wsRef.current.onclose = () => {
            setIsConnected(false);
            setIsRecognizing(false);
        };

        wsRef.current.onmessage = (event) => {
            const msg = JSON.parse(event.data);
            if (msg.type === 'result') {
                setResult(msg.payload.text);
            } else if (msg.type === 'status' && msg.payload.status === 'recognizing') {
                setIsRecognizing(true);
            } else if (msg.type === 'status' && msg.payload.status === 'closed') {
                setIsRecognizing(false);
            }
        };
    };

    const startRecognition = async () => {
        wsRef.current?.send(JSON.stringify({
            type: 'control',
            messageId: 'msg_' + Date.now(),
            timestamp: new Date().toISOString(),
            payload: { command: 'StartRecognition' }
        }));

        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        mediaStreamRef.current = stream;

        const audioContext = new AudioContext();
        const audioSource = audioContext.createMediaStreamSource(stream);

        await audioContext.audioWorklet.addModule('/audio-worklet.js');
        const audioWorkletNode = new AudioWorkletNode(audioContext, 'audio-processor');

        audioWorkletNode.port.onmessage = (event) => {
            if (event.data.type === 'audioData' && wsRef.current?.readyState === WebSocket.OPEN) {
                wsRef.current.send(event.data.data);
            }
        };

        audioSource.connect(audioWorkletNode);
    };

    const stopRecognition = () => {
        wsRef.current?.send(JSON.stringify({
            type: 'control',
            messageId: 'msg_' + Date.now(),
            timestamp: new Date().toISOString(),
            payload: { command: 'EndRecognition' }
        }));

        mediaStreamRef.current?.getTracks().forEach(track => track.stop());
        setIsRecognizing(false);
    };

    return (
        <div>
            <button disabled={isConnected} onClick={connect}>连接</button>
            <button disabled={!isConnected} onClick={() => wsRef.current?.close()}>
                断开
            </button>
            <button disabled={!isConnected || isRecognizing} onClick={startRecognition}>
                开始识别
            </button>
            <button disabled={!isRecognizing} onClick={stopRecognition}>
                停止识别
            </button>
            <div>识别结果: {result}</div>
        </div>
    );
}
```

---

## 11. 常见问题

### 11.1 连接问题

**Q: 无法连接到 WebSocket 服务**

A: 检查以下几点：
1. WebProxy 服务是否已启动
2. 防火墙是否允许 WebSocket 连接
3. URL 和端口是否正确

---

### 11.2 认证问题

**Q: 返回 401 认证失败**

A: 检查：
1. AppId 是否正确
2. AccessToken 是否有效
3. 账号是否有访问权限

---

### 11.3 音频问题

**Q: 没有识别结果**

A: 可能的原因：
1. 音频数据未正确发送（检查浏览器控制台）
2. 音频格式不正确（必须是 16kHz 16-bit mono PCM）
3. 音量太低或没有声音
4. 网络延迟导致超时

---

### 11.4 AudioWorklet 加载失败

**Q: AudioWorklet 加载失败**

A: 检查：
1. `audio-worklet.js` 文件是否在 `wwwroot` 目录
2. 文件是否可通过 HTTP 访问
3. 文件内容是否正确

---

### 11.5 回声问题

**Q: 能听到自己的声音**

A: 确保音频图没有连接到 `destination`：
```javascript
// ❌ 不要这样做
audioSource.connect(audioContext.destination);

// ✅ 正确的做法
audioSource.connect(audioWorkletNode);
// 不要连接到 destination
```

---

## 12. 技术支持

如有问题，请联系：
- GitHub Issues
- 项目 Wiki
- 技术文档

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| 1.0.0 | 2026-03-03 | 初始版本 |

---

*最后更新：2026-03-03*
