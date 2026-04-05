# IFlowDotnet

C# playground for validating an `iflow-cli-sdk` style integration in `samples/IFlowDotnet`.

## Projects

- `IFlowSdk/` - reusable ACP/WebSocket client, process bootstrap, query helpers, and raw-data access
- `IFlowSdk.ConsoleDemo/` - interactive console demo for prompt, approval, interrupt, and diagnostics workflows
- `IFlowSdk.Tests/` - unit tests plus opt-in integration coverage for a local `iflow` executable

## Prerequisites

- .NET SDK 8.0+
- `iflow` CLI available on `PATH`, or `IFLOW_EXECUTABLE` pointing to the executable
- Valid iFlow authentication for prompt execution when running live end-to-end flows

## Build

```bash
cd samples/IFlowDotnet
DOTNET_CLI_HOME=/tmp/dotnet-home NUGET_PACKAGES=/tmp/nuget-packages NUGET_HTTP_CACHE_PATH=/tmp/nuget-http-cache dotnet build IFlowDotnet.slnx
```

## Test

Unit tests only:

```bash
cd samples/IFlowDotnet
DOTNET_CLI_HOME=/tmp/dotnet-home NUGET_PACKAGES=/tmp/nuget-packages NUGET_HTTP_CACHE_PATH=/tmp/nuget-http-cache dotnet test IFlowDotnet.slnx
```

Opt-in integration run:

```bash
cd samples/IFlowDotnet
IFLOW_INTEGRATION=1 DOTNET_CLI_HOME=/tmp/dotnet-home NUGET_PACKAGES=/tmp/nuget-packages NUGET_HTTP_CACHE_PATH=/tmp/nuget-http-cache dotnet test IFlowDotnet.slnx
```

`IFLOW_INTEGRATION=1` expects a usable local `iflow` installation and valid auth state for prompt execution.

## Run the console demo

```bash
cd samples/IFlowDotnet
DOTNET_CLI_HOME=/tmp/dotnet-home NUGET_PACKAGES=/tmp/nuget-packages NUGET_HTTP_CACHE_PATH=/tmp/nuget-http-cache dotnet run --project IFlowSdk.ConsoleDemo/IFlowSdk.ConsoleDemo.csproj
```

### Demo commands

- `/prompt <text>` - send a prompt explicitly
- `/interrupt` - cancel the current turn
- `/approve <requestId> <optionId>` - respond to a tool confirmation request
- `/reject <requestId>` - reject a tool confirmation request
- `/raw` - toggle raw message printing when raw capture is enabled
- `/exit` - quit the demo

## Configuration knobs

Environment variables used by the demo and SDK playground:

- `IFLOW_URL` - connect to an existing ACP WebSocket endpoint instead of auto-starting a local CLI
- `IFLOW_CWD` - working directory used for `session/new` or `session/load`
- `IFLOW_EXECUTABLE` - full path to the `iflow` executable
- `IFLOW_START_PORT` - preferred starting port for auto-started ACP sessions
- `IFLOW_TIMEOUT_SECONDS` - connection and request timeout
- `IFLOW_AUTH_METHOD_ID` - authentication method override, defaulting to `iflow`
- `IFLOW_DEMO_CAPTURE_RAW=true` - use `RawDataClient` in the demo
- `IFLOW_DEMO_PRINT_RAW=true` - print raw frames immediately in the demo
- `IFLOW_INTEGRATION=1` - opt into the live integration test

## What the playground currently covers

- Auto-start or attach-to-existing ACP connection flow
- JSON-RPC initialization, authentication, session creation, load-session, prompt, and cancel operations
- Typed parsing for assistant chunks, thoughts, tool calls, tool updates, plans, task finish, and permission requests
- Query helpers for one-shot and streaming scenarios
- Optional raw-frame capture for protocol debugging

## Current limitations

- The first C# delivery focuses on the core interactive/query path and does not yet replicate every Python-only diagnostic utility
- Integration tests are intentionally opt-in because they depend on local auth state and a working `iflow` CLI
- Prompt execution may fail if the local iFlow API token is expired; the SDK surfaces that as a typed error message with diagnostics

## Troubleshooting

- `Unable to find iflow on PATH`
  - Set `IFLOW_EXECUTABLE` or install the CLI so `iflow --version` works
- `Timed out waiting for iFlow ACP //ready`
  - Increase `IFLOW_TIMEOUT_SECONDS` and verify the CLI can start locally
- Prompt requests return auth or token errors
  - Refresh the local iFlow authentication/token before rerunning the demo or integration test
- You want to inspect raw ACP traffic
  - Start the demo with `IFLOW_DEMO_CAPTURE_RAW=true IFLOW_DEMO_PRINT_RAW=true`
