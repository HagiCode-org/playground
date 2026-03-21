# KimiAcpDotnet

Standalone .NET playground for validating Kimi ACP bootstrap, prompt execution, and transcript capture without touching production services.

The first phase stays inside `repos/playground`: there is no root-level `npm run example:kimi-acp-dotnet` shortcut yet, so use the direct project commands in this README.

## Projects

- `KimiAcpSdk/` - reusable configuration loader, Kimi process runner, stdio ACP client, transcript capture, and report writer
- `KimiAcpSdk.ConsoleDemo/` - console demo with `/connect`, `/prompt`, `/raw`, `/report`, and `/exit`
- `KimiAcpSdk.Tests/` - unit tests plus opt-in live integration coverage for a local Kimi executable or the bundled adapter fixture

## Local prerequisites

- .NET SDK 10.0.201 or later
- A local Kimi executable that can speak ACP over stdio, or a thin local adapter that preserves the same `initialize` / `authenticate` / `session/new` / `session/prompt` contract
- Optional: Python 3 if you want to use the bundled fake adapter fixture for opt-in integration tests

## Configuration

Copy `appsettings.example.json` to `appsettings.json` in this directory when you want local settings that differ from the defaults.

Supported profile settings:

- `ExecutablePath` - executable name or absolute path for Kimi or a wrapper adapter
- `Arguments` - ordered argument values passed to the executable
- `WorkingDirectory` - cwd used to launch Kimi and to seed `session/new`
- `EnvironmentVariables` - additional environment variables forwarded to the child process; report output redacts obvious secret-like keys
- `DefaultPrompt` - prompt used by one-shot or default validation runs
- `TimeoutSeconds` - timeout used while waiting for `//ready` and JSON-RPC responses
- `Authentication.PreferredMethodId` - optional auth method hint when `initialize` advertises more than one method
- `Authentication.MethodInfo` - optional key/value payload echoed into the `authenticate` request
- `SessionDefaults` - model, mode, and extra metadata emitted in `session/new`
- `Artifacts.RunStorePath` - root directory where transcripts and reports are written

Minimal example:

```json
{
  "ActiveProfile": "kimi-local",
  "Profiles": {
    "kimi-local": {
      "ExecutablePath": "kimi",
      "Arguments": ["acp"],
      "WorkingDirectory": "../..",
      "Artifacts": {
        "RunStorePath": "./.kimi-acp-runs"
      }
    }
  }
}
```

## Build

```bash
cd repos/playground/KimiAcpDotnet
DOTNET_CLI_HOME=$PWD/.dotnet-home NUGET_PACKAGES=$PWD/.nuget/packages NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache   dotnet build KimiAcpDotnet.slnx
```

## Test

Unit tests only:

```bash
cd repos/playground/KimiAcpDotnet
DOTNET_CLI_HOME=$PWD/.dotnet-home NUGET_PACKAGES=$PWD/.nuget/packages NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache   dotnet test KimiAcpDotnet.slnx
```

Fixture and unit coverage only:

```bash
cd repos/playground/KimiAcpDotnet
DOTNET_CLI_HOME=$PWD/.dotnet-home NUGET_PACKAGES=$PWD/.nuget/packages NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache   dotnet test KimiAcpDotnet.slnx
```

Opt-in live integration coverage against a real Kimi executable or the bundled adapter fixture:

```bash
cd repos/playground/KimiAcpDotnet
KIMI_INTEGRATION=1 DOTNET_CLI_HOME=$PWD/.dotnet-home NUGET_PACKAGES=$PWD/.nuget/packages NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache   dotnet test KimiAcpDotnet.slnx
```

Real Kimi regression coverage for the two validated scenarios (`PONG` ping and repository analysis):

```bash
cd repos/playground/KimiAcpDotnet
KIMI_REAL_REGRESSION=1 DOTNET_CLI_HOME=$PWD/.dotnet-home NUGET_PACKAGES=$PWD/.nuget/packages NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache   dotnet test KimiAcpDotnet.slnx --filter FullyQualifiedName~RealKimiRegressionTests
```

Use these environment overrides when you want to target a real executable instead of the bundled adapter fixture:

- `KIMI_INTEGRATION_EXECUTABLE` - executable path
- `KIMI_INTEGRATION_ARGS` - space-separated argument string
- `KIMI_REAL_EXECUTABLE` - override the real Kimi executable used by regression tests
- `KIMI_REAL_ARGS` - override the real Kimi ACP arguments, default `acp`
- `KIMI_KEEP_ARTIFACTS=1` - keep generated run artifacts from live regression tests for later inspection

## Run the console demo

Interactive demo:

```bash
cd repos/playground/KimiAcpDotnet
cp appsettings.example.json appsettings.json
DOTNET_CLI_HOME=$PWD/.dotnet-home NUGET_PACKAGES=$PWD/.nuget/packages NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache   dotnet run --project KimiAcpSdk.ConsoleDemo/KimiAcpSdk.ConsoleDemo.csproj
```

One-shot validation with overrides:

```bash
cd repos/playground/KimiAcpDotnet
DOTNET_CLI_HOME=$PWD/.dotnet-home NUGET_PACKAGES=$PWD/.nuget/packages NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache   dotnet run --project KimiAcpSdk.ConsoleDemo/KimiAcpSdk.ConsoleDemo.csproj --     --profile kimi-local     --prompt "Summarize the repo layout."     --raw     --arg acp
```

### Demo commands

- `/connect` - launch Kimi, run `initialize`, optional `authenticate`, and `session/new`
- `/prompt <text>` - send `session/prompt`, print the final text, and persist a report snapshot
- `/raw` - toggle raw transcript printing while keeping transcript capture on disk
- `/report` - persist and print the current feature matrix snapshot without sending a new prompt
- `/exit` - close the session and stop the demo

## Saved artifacts

Each snapshot writes a timestamped directory under `Artifacts.RunStorePath` containing:

- `summary.json` - redacted launch contract, typed run result, and feature matrix
- `report.md` - human-readable compatibility summary
- `transcript.log` - flattened raw ACP frames and stderr lines
- `transcript.json` - structured transcript entries
- `diagnostics.json` - failure stage/message metadata when a run fails or emits stderr
- `stderr.log` - only present when Kimi writes to stderr

## Assumptions

- Kimi may emit `//ready`, but the playground now also supports providers that reply directly to `initialize` without a readiness marker.
- The first validation pass only requires the core ACP lifecycle. Rich callback coverage can be layered on later once the bootstrap path is stable.
- Authentication is capability-driven: if `initialize` advertises no auth methods, the run marks auth as skipped rather than failed.

## Troubleshooting

- `Kimi request 1 timed out`
  - Verify `Arguments` match the local CLI or adapter contract. Keep the executable path and argument list in config rather than hard-coding them in the demo command.
- `Unknown Kimi profile`
  - Check `ActiveProfile`, `Profiles`, and any `--profile` override you passed to the console demo.
- `Prompt completed without usable text`
  - Inspect `transcript.log`, `summary.json`, and `diagnostics.json` to confirm whether Kimi emitted notifications but no final text payload.
- `No transcript frames captured`
  - Make sure the executable is running and writing line-delimited ACP frames to stdout.
- Secret values appear in reports
  - Use environment keys containing `KEY`, `TOKEN`, `SECRET`, or `PASSWORD` so the report redaction rule can mask them automatically.
