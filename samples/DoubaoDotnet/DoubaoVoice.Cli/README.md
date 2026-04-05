# DoubaoVoice CLI 使用文档

DoubaoVoice CLI 是一个基于 DoubaoVoice .NET SDK 的控制台应用程序，用于实时语音转文字。

## 安装

### 从源码构建

```bash
git clone <repository-url>
cd samples/DoubaoDotnet
dotnet build -c Release
```

### 运行

```bash
cd DoubaoVoice.Cli/bin/Release/net8.0
dotnet DoubaoVoice.Cli.dll <AppId> <AccessToken>
```

## 命令行参数

### 必需参数

| 参数 | 说明 |
|------|------|
| AppId | 豆包语音应用 ID |
| AccessToken | 豆包语音访问令牌 |

### 可选参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| --url | 服务 URL | wss://openspeech.bytedance.com/api/v2/asr |
| --sample-rate | 采样率 (Hz) | 16000 |
| --model | 模型名称 | bigmodel |

## 使用示例

### 基本用法

```bash
dotnet DoubaoVoice.Cli.dll your_app_id your_access_token
```

### 自定义服务 URL

```bash
dotnet DoubaoVoice.Cli.dll your_app_id your_access_token --url wss://your-custom-url.com/api/v2/asr
```

### 使用 8000Hz 采样率

```bash
dotnet DoubaoVoice.Cli.dll your_app_id your_access_token --sample-rate 8000
```

### 指定模型

```bash
dotnet DoubaoVoice.Cli.dll your_app_id your_access_token --model bigmodel_v2
```

## 交互操作

### 开始识别

程序启动后会自动开始监听麦克风并识别语音。

### 停止识别

按 `Ctrl+C` 优雅退出程序。

## 输出格式

```
[Connected] Connected to DoubaoVoice service

Listening... Press Ctrl+C to stop.

[14:30:25] 你好世界
[14:30:30] 这是语音识别测试
[Completed] Recognition finished
  Final text: 你好世界这是语音识别测试
  Total segments: 5
```

### 输出说明

- `[Connected]` - 连接成功
- `[Disconnected]` - 连接断开
- `[Error]` - 发生错误
- `[时间戳] 文本` - 识别结果
- `[Completed]` - 识别完成

## 音频设备要求

### Windows

- 支持 WASAPI API 的声卡
- 已连接并启用的麦克风

### Linux

- 支持 ALSA 或 PulseAudio 的声卡
- 已配置并启用的麦克风设备

### macOS

- 支持 CoreAudio 的声卡
- 已授权麦克风访问权限

## 音频要求

- 采样率: 16000 Hz 或 8000 Hz
- 位深度: 16-bit
- 声道数: 单声道 (mono)

## 常见问题

### 无法检测到麦克风设备

**问题**: 程序启动时显示 "No audio recording devices available"

**解决方案**:
1. 检查麦克风是否正确连接
2. 确认麦克风设备已在系统设置中启用
3. Windows: 在"声音设置"中检查麦克风是否被禁用
4. Linux: 使用 `arecord -l` 检查录音设备
5. macOS: 在"系统偏好设置 > 安全性与隐私 > 隐私 > 麦克风"中授权

### 连接失败

**问题**: 程序显示 "Connection failed" 或 "Authentication failed"

**解决方案**:
1. 检查 App ID 和 Access Token 是否正确
2. 确认网络连接正常
3. 检查防火墙设置
4. 验证服务 URL 是否正确

### 识别结果不准确

**问题**: 识别结果不准确或为空

**解决方案**:
1. 确保麦克风音量适中
2. 减少环境噪音
3. 清晰缓慢地说话
4. 检查音频格式是否符合要求

### 权限错误 (macOS)

**问题**: 程序无法访问麦克风

**解决方案**:
1. 打开"系统偏好设置 > 安全性与隐私 > 隐私 > 麦克风"
2. 找到终端或对应的终端应用
3. 勾选允许访问麦克风

### ALSA 错误 (Linux)

**问题**: 程序报错 ALSA 相关错误

**解决方案**:
1. 安装 ALSA 库: `sudo apt-get install libasound2-dev`
2. 检查设备权限: `ls -l /dev/snd/`
3. 将用户添加到 audio 组: `sudo usermod -a -G audio $USER`

## 系统要求

- .NET 8.0 或更高版本
- Windows 10/11, Linux (glibc 2.28+), macOS 10.15+
- 麦克风设备

## 许可证

MIT License
