# CopilotDotnet Playground

A focused .NET playground for validating GitHub Copilot SDK integration patterns in `repos/playground`.

## What this validates

- Startup configuration validation with clear failure output
- Credential acquisition and token refresh retry flow
- Copilot SDK request invocation with correlation IDs
- Stream/final response normalization and error categorization
- Run outcome persistence to JSONL and SQLite for diagnostics

## Prerequisites

- .NET SDK 8.0+
- GitHub Copilot CLI installed and authenticated (`copilot --version`)
- Optional: `GITHUB_TOKEN` if you do not want to use logged-in-user mode

## Configuration

Create `appsettings.json` in `repos/playground/CopilotDotnet` (or pass `--config <path>`):

```json
{
  "Model": "gpt-5",
  "WorkingDirectory": "/home/newbe36524/repos/newbe36524/hagicode-mono",
  "RunStorePath": "./.copilot-runs",
  "CliPath": "copilot",
  "CliUrl": null,
  "UseLoggedInUser": true,
  "GitHubToken": null,
  "UseSqlite": true,
  "TimeoutSeconds": 90
}
```

Environment overrides are supported with `COPILOT_` prefix, for example:

- `COPILOT_MODEL`
- `COPILOT_WORKINGDIRECTORY`
- `COPILOT_RUNSTOREPATH`
- `COPILOT_USELOGGEDINUSER`
- `COPILOT_TIMEOUTSECONDS`

`GITHUB_TOKEN` is read directly if `GitHubToken` is not set in config.

## Build and test

```bash
cd repos/playground/CopilotDotnet
DOTNET_CLI_HOME=/tmp/dotnet-home \
NUGET_PACKAGES=/tmp/nuget-packages \
NUGET_HTTP_CACHE_PATH=/tmp/nuget-http-cache \
dotnet test CopilotDotnet.slnx
```

## Run

Interactive:

```bash
cd repos/playground/CopilotDotnet
DOTNET_CLI_HOME=/tmp/dotnet-home \
NUGET_PACKAGES=/tmp/nuget-packages \
NUGET_HTTP_CACHE_PATH=/tmp/nuget-http-cache \
dotnet run --project CopilotSdk.ConsoleDemo/CopilotSdk.ConsoleDemo.csproj -- --config appsettings.json
```

Single prompt mode:

```bash
dotnet run --project CopilotSdk.ConsoleDemo/CopilotSdk.ConsoleDemo.csproj -- "Summarize this repository architecture"
```

## Reproducible validation scenarios

1. **Success path**
   - Provide valid config and logged-in Copilot CLI
   - Send a prompt, verify `success=true` and non-empty content
   - Confirm run record in `.copilot-runs/runs.jsonl` and `runs.db`
2. **Auth failure**
   - Set `UseLoggedInUser=false` without `GITHUB_TOKEN`
   - Run demo and verify startup/config validation failure or auth diagnostic output
3. **Timeout**
   - Set `TimeoutSeconds` to a small value (for example `1`)
   - Trigger a longer prompt and verify timeout classification in run output
4. **Retry recovery**
   - Simulate auth expiry in tests (see integration tests)
   - Verify first call fails with auth error, refresh occurs, second call succeeds

## Troubleshooting

- `Model is required` / `RunStorePath is required`
  - Add missing values in `appsettings.json` or `COPILOT_*` environment variables
- `No credentials available for Copilot authentication`
  - Either log into Copilot CLI and set `UseLoggedInUser=true`, or set `GITHUB_TOKEN`
- `copilot executable not found`
  - Install Copilot CLI or set `CliPath` to the executable location
- `Permission denied` under `~/.nuget` in sandboxed environments
  - Set `DOTNET_CLI_HOME`, `NUGET_PACKAGES`, and `NUGET_HTTP_CACHE_PATH` to writable paths (for example `/tmp/...`)
- Empty response with no errors
  - Confirm CLI authentication state and model availability (`copilot` CLI settings)
