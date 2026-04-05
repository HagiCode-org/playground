# DoubaoVoice.WebProxy

A WebSocket proxy service for DoubaoVoice SDK, enabling real-time speech recognition via WebSocket connections.

## Overview

DoubaoVoice.WebProxy provides a WebSocket interface for the DoubaoVoice speech recognition SDK, allowing web clients to perform real-time audio streaming and receive recognition results. The proxy manages WebSocket connections, Doubao API sessions, audio buffering, and result aggregation.

## Features

- **Real-time Audio Streaming**: Stream audio data via WebSocket for immediate recognition
- **Session Management**: Automatic session creation and cleanup
- **Multiple Control Commands**: Start, stop, pause, and resume recognition
- **Result Aggregation**: Intelligent filtering and deduplication of recognition results
- **Binary Audio Protocol**: Efficient binary audio data transfer
- **Configurable Settings**: Full configuration via YAML file
- **Built-in Test Page**: Interactive HTML test interface

## Architecture

```
Web Client --WebSocket--> DoubaoWebSocketHandler --DoubaoSessionManager--> Doubao SDK
                                                              |
                                                              v
                                                         Doubao API
```

## Configuration

The service is configured via `appsettings.yml`:

```yaml
DoubaoProxy:
  # Doubao API credentials (required)
  AppId: "your-app-id"
  AccessToken: "your-access-token"
  ServiceUrl: "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async"
  ResourceId: "volc.bigasr.sauc.duration"

  # Audio settings
  SampleRate: 16000
  BitsPerSample: 16
  Channels: 1
  AudioFormat: "wav"
  AudioCodec: "raw"

  # Recognition settings
  ModelName: "bigmodel"
  EnableITN: true
  EnablePunctuation: true
  EnableDDC: true
  ShowUtterances: true
  EnableNonstream: false

  # Buffer settings
  BufferSize: 10
  BufferTimeoutMs: 5000
  ChunkSizeBytes: 3200

  # Result processing
  ConfidenceThreshold: 0.5

  # Server settings
  ListenUrl: "http://localhost:5000"
```

### Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `AppId` | Doubao API App ID | - (required) |
| `AccessToken` | Doubao API Access Token | - (required) |
| `ServiceUrl` | Doubao WebSocket service URL | `wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async` |
| `ResourceId` | API resource identifier | `volc.bigasr.sauc.duration` |
| `SampleRate` | Audio sample rate in Hz | `16000` |
| `BitsPerSample` | Audio bit depth | `16` |
| `Channels` | Number of audio channels | `1` |
| `AudioFormat` | Audio format | `wav` |
| `AudioCodec` | Audio codec | `raw` |
| `ModelName` | Recognition model | `bigmodel` |
| `EnableITN` | Enable inverse text normalization | `true` |
| `EnablePunctuation` | Enable punctuation | `true` |
| `EnableDDC` | Enable domain adaptation | `true` |
| `ShowUtterances` | Show utterance details | `true` |
| `EnableNonstream` | Enable streaming + non-stream second-pass recognition | `false` |
| `EndWindowSize` | VAD endpoint window in milliseconds (effective when `EnableNonstream=true`) | `800` (200-5000) |
| `BufferSize` | Max buffered audio segments | `10` |
| `BufferTimeoutMs` | Buffer timeout in milliseconds | `5000` |
| `ConfidenceThreshold` | Minimum confidence for results | `0.5` |

## API Usage

### WebSocket Endpoint

Connect to: `ws://localhost:5000/ws`

### Query Parameters (Second-pass Recognition Experiment)

| Parameter | Type | Required | Default | Notes |
|-----------|------|----------|---------|-------|
| `enableNonstream` | bool | No | `false` | Enables streaming + non-stream second-pass mode |
| `endWindowSize` | int | No | `800` | Valid range `200-5000` ms, only used when `enableNonstream=true` |

Example:

```
ws://localhost:5000/ws?appId=xxx&accessToken=yyy&enableNonstream=true&endWindowSize=800
```

### Message Protocol

All text messages use JSON format:

```json
{
  "type": "control|result|error|status",
  "messageId": "unique-id",
  "timestamp": "2024-01-01T00:00:00Z",
  "payload": { /* message-specific data */ }
}
```

### Control Commands

#### Start Recognition

```json
{
  "type": "control",
  "messageId": "msg_123",
  "timestamp": "2024-01-01T00:00:00Z",
  "payload": {
    "command": "StartRecognition",
    "parameters": {}
  }
}
```

#### End Recognition

```json
{
  "type": "control",
  "messageId": "msg_124",
  "timestamp": "2024-01-01T00:00:00Z",
  "payload": {
    "command": "EndRecognition",
    "parameters": {}
  }
}
```

#### Pause Recognition

```json
{
  "type": "control",
  "messageId": "msg_125",
  "timestamp": "2024-01-01T00:00:00Z",
  "payload": {
    "command": "PauseRecognition",
    "parameters": {}
  }
}
```

#### Resume Recognition

```json
{
  "type": "control",
  "messageId": "msg_126",
  "timestamp": "2024-01-01T00:00:00Z",
  "payload": {
    "command": "ResumeRecognition",
    "parameters": {}
  }
}
```

### Audio Streaming

Send audio data as binary WebSocket messages. The audio should be:
- PCM format
- 16kHz sample rate
- 16-bit
- Mono

### Response Messages

#### Status Message

```json
{
  "type": "status",
  "payload": {
    "status": "connected|recognizing|paused|closed",
    "sessionId": "session-id",
    "timestamp": "2024-01-01T00:00:00Z"
  }
}
```

#### Result Message

```json
{
  "type": "result",
  "payload": {
    "text": "recognized text",
    "confidence": 0.95,
    "duration": 2000,
    "isFinal": false,
    "definite": false,
    "utterances": [
      {
        "text": "text",
        "startTime": 0,
        "endTime": 2000,
        "definite": true,
        "words": []
      }
    ],
    "timestamp": "2024-01-01T00:00:00Z"
  }
}
```

### `definite` Semantics

- `definite=false`: streaming interim result, prioritizes low latency ("快")
- `definite=true`: non-stream second-pass result for an utterance, prioritizes accuracy ("准")
- When second-pass mode is disabled (`enableNonstream=false`), returned results are expected to stay `definite=false`.

### Expected Second-pass Behavior

1. Enable `enableNonstream=true` and start recognition.
2. Streaming text appears immediately with `definite=false`.
3. After VAD detects pause (`endWindowSize`, default 800ms), the same utterance is re-recognized by non-stream mode.
4. Frontend receives replacement utterance with `definite=true` and renders it as final text.

#### Error Message

```json
{
  "type": "error",
  "payload": {
    "errorMessage": "Error description",
    "errorCode": "ERROR_CODE",
    "timestamp": "2024-01-01T00:00:00Z"
  }
}
```

## Client-Side WebSocket Usage

### JavaScript Example

```javascript
const ws = new WebSocket('ws://localhost:5000/ws');

ws.onopen = () => {
    console.log('Connected to DoubaoVoice.WebProxy');
};

ws.onmessage = (event) => {
    const message = JSON.parse(event.data);

    switch (message.type) {
        case 'status':
            console.log('Status:', message.payload.status);
            if (message.payload.status === 'connected') {
                // Start recognition
                ws.send(JSON.stringify({
                    type: 'control',
                    messageId: generateId(),
                    timestamp: new Date().toISOString(),
                    payload: { command: 'StartRecognition', parameters: {} }
                }));
            }
            break;
        case 'result':
            console.log('Recognized:', message.payload.text);
            break;
        case 'error':
            console.error('Error:', message.payload.errorMessage);
            break;
    }
};

// Send audio data
function sendAudio(audioData) {
    ws.send(audioData);
}
```

## Running the Service

### Prerequisites

- .NET 8.0 SDK
- Valid Doubao API credentials (AppId and AccessToken)

### Start the Service

```bash
cd samples/DoubaoDotnet/DoubaoVoice.WebProxy
dotnet run
```

The service will start on `http://localhost:5000` by default.

### Test the Service

1. Open your browser to `http://localhost:5000/index.html`
2. Click "Connect" to establish WebSocket connection
3. Click "Start Recognition" to begin
4. Speak into your microphone
5. Recognition results will appear in real-time

## Building

### Debug Build

```bash
dotnet build
```

### Release Build

```bash
dotnet build -c Release
```

### Publish for Deployment

```bash
dotnet publish -c Release -o ./publish
```

## Logging

Logs are written to:
- Console output
- `logs/doubao-proxy-{date}.txt`

Log levels are configurable in `appsettings.yml`:

```yaml
Serilog:
  MinimumLevel:
    Default: Information
    Override:
      Microsoft: Warning
```

## Troubleshooting

### Connection Issues

- Verify `AppId` and `AccessToken` are correct in `appsettings.yml`
- Check network connectivity to Doubao API
- Review logs for specific error messages

### Audio Issues

- Ensure audio format is PCM 16kHz 16-bit mono
- Check microphone permissions in browser
- Verify audio data is being sent as binary WebSocket messages

### Session Issues

- Sessions are automatically cleaned up after inactivity
- Check logs for session creation and removal events

## Project Structure

```
DoubaoVoice.WebProxy/
├── Program.cs                          # Application entry point
├── appsettings.yml                     # Configuration file
├── Handlers/
│   ├── DoubaoWebSocketHandler.cs        # WebSocket handler
│   └── IMessageProtocol.cs             # Message protocol interface
├── Services/
│   ├── Configuration/
│   │   ├── DoubaoProxyOptions.cs        # Configuration options
│   │   └── IDoubaoProxyOptions.cs      # Configuration interface
│   ├── DoubaoSessionManager.cs          # Session manager
│   ├── IDoubaoSessionManager.cs        # Session manager interface
│   ├── AudioBuffer.cs                   # Audio buffering
│   └── ResultAggregator.cs            # Result aggregation
├── Models/
│   ├── AudioSegmentRequest.cs           # Audio request DTO
│   ├── RecognitionResultDto.cs         # Recognition result DTO
│   ├── UtteranceDto.cs                # Utterance DTO
│   ├── SessionControlRequest.cs        # Session control DTO
│   └── ControlMessage.cs               # Control message model
└── wwwroot/
    └── index.html                      # Test page
```

## Dependencies

- **DoubaoVoice.SDK**: Internal SDK for Doubao API communication
- **Serilog.AspNetCore**: Structured logging
- **YamlDotNet**: YAML configuration support
- **Microsoft.AspNetCore.WebSockets**: WebSocket support

## License

This project is part of the hagicode-mono playground.
