# CodexDotnet

Minimal C# port of the `repo/codex/sdk/typescript` SDK, plus a console demo app.

Detailed usage guide: `repos/playground/CodexDotnet/CodexSdk.USAGE.md`

## Projects

- `CodexSdk/` - C# SDK wrapper around `codex exec --experimental-json`
- `CodexSdk.ConsoleDemo/` - interactive console app that uses the SDK

## Build

```bash
cd repos/playground/CodexDotnet
dotnet build CodexSdk/CodexSdk.csproj
dotnet build CodexSdk.ConsoleDemo/CodexSdk.ConsoleDemo.csproj
```

## Run demo

```bash
cd repos/playground/CodexDotnet
dotnet run --project CodexSdk.ConsoleDemo/CodexSdk.ConsoleDemo.csproj
```

Optional environment variables:

- `CODEX_EXECUTABLE`: full path to `codex` binary (default: `codex` from PATH)
- `CODEX_WORKING_DIR`: working directory for the agent (`--cd`)
- `CODEX_SANDBOX_MODE`: `read-only`, `workspace-write`, or `danger-full-access`
- `CODEX_MODEL`: model name passed to `--model`
- `CODEX_APPROVAL_POLICY`: `never`, `on-request`, `on-failure`, or `untrusted`
- `OPENAI_BASE_URL`: forwarded to Codex CLI environment
- `CODEX_API_KEY`: forwarded to Codex CLI environment

In the demo:

- type a prompt to execute a Codex turn
- type `/exit` to quit
