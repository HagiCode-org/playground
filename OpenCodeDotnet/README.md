# OpenCodeDotnet

A .NET playground for experimenting with an OpenCode C# SDK.

## Projects

- `OpenCodeSdk/` - typed C# client, generated API surface, and per-session process runtime
- `OpenCodeSdk.ConsoleDemo/` - interactive console demo for dedicated OpenCode sessions
- `OpenCodeSdk.Tests/` - unit, concurrency, and opt-in integration tests

## Build

Use a writable local NuGet cache when building in restricted environments:

```bash
cd repos/playground/OpenCodeDotnet
DOTNET_CLI_HOME=$PWD/.dotnet-home \
NUGET_PACKAGES=$PWD/.nuget/packages \
NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache \
  dotnet build OpenCodeDotnet.slnx
```

## Run the demo

```bash
cd repos/playground/OpenCodeDotnet
DOTNET_CLI_HOME=$PWD/.dotnet-home \
NUGET_PACKAGES=$PWD/.nuget/packages \
NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache \
  dotnet run --project OpenCodeSdk.ConsoleDemo/OpenCodeSdk.ConsoleDemo.csproj
```

## Refresh generated client/models

The SDK keeps generated files under `OpenCodeSdk/Generated/`.
Regenerate them from the checked-in OpenAPI contract with:

```bash
cd repos/playground/OpenCodeDotnet
python scripts/generate_openapi_client.py
```

The generator consumes `../../opencode/packages/sdk/openapi.json` and rewrites:

- `OpenCodeSdk/Generated/OpenCodeModels.g.cs`
- `OpenCodeSdk/Generated/OpenCodeGeneratedClient.g.cs`

## Demo configuration

Supported environment variables:

- `OPENCODE_BASE_URL` - attach to an existing server instead of spawning a dedicated process
- `OPENCODE_EXECUTABLE` - override the `opencode` executable path
- `OPENCODE_DIRECTORY` - send `x-opencode-directory` for session requests
- `OPENCODE_WORKSPACE` - optional workspace query parameter
- `OPENCODE_STARTUP_TIMEOUT_SECONDS` - process startup timeout for dedicated sessions
- `OPENCODE_LOG_LEVEL` - optional `opencode serve --log-level` value
- `OPENCODE_CONFIG_JSON` - JSON content forwarded to `OPENCODE_CONFIG_CONTENT`
- `OPENCODE_SESSION_TITLE` - default title for created sessions

## Test

```bash
cd repos/playground/OpenCodeDotnet
DOTNET_CLI_HOME=$PWD/.dotnet-home \
NUGET_PACKAGES=$PWD/.nuget/packages \
NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache \
  dotnet test OpenCodeSdk.Tests/OpenCodeSdk.Tests.csproj
```

Integration tests are opt-in. Set `OPENCODE_DOTNET_RUN_INTEGRATION=1` when local OpenCode configuration is ready for a real prompt lifecycle.

## Current limitations

- The generated surface is intentionally representative rather than a full 1:1 port of every OpenCode endpoint.
- The primary abstraction is a per-session process runtime. Shared attach mode is supported for experimentation, but not the default workflow.
- Event subscription support focuses on basic Server-Sent Events decoding for diagnostics and demos.
