# CodeBuddyDotnet

A standalone .NET playground for validating CodeBuddy behavior from a black-box perspective.

## What this validates

- Independent CodeBuddy test scaffolding inside `repos/playground`
- Deterministic fixture scenarios for startup validation, event normalization, and auth classification
- Opt-in live scenarios for prompt success and timeout classification
- Structured run artifacts under `.codebuddy-runs/` for diagnostics and regression comparisons

## Projects

- `CodeBuddySdk/` - configuration loading, process runtime, scenario catalog, and artifact writer
- `CodeBuddySdk.ConsoleDemo/` - CLI entry point for running fixture or live scenarios
- `CodeBuddySdk.Tests/` - unit tests for configuration, runtime behavior, artifact writing, and scenario gating

## Configuration

Fixture mode works without local credentials. For live scenarios, create `appsettings.json` in `repos/playground/CodeBuddyDotnet` by copying `appsettings.example.json` and adjusting the values for your local CodeBuddy CLI contract.

Supported top-level settings:

- `CliPath` - executable name or full path, default `codebuddy`
- `WorkingDirectory` - workspace sent to the CLI, default current directory
- `RunStorePath` - output directory for run artifacts, default `./.codebuddy-runs`
- `EnableLiveScenarios` - opt-in switch for live runs
- `PromptTransport` - `Stdin` or `Arguments`
- `Arguments` - optional argument array supporting `{prompt}`, `{promptFile}`, and `{workingDirectory}` placeholders
- `StartupTimeoutSeconds` - timeout for startup-oriented checks
- `CommandTimeoutSeconds` - timeout for prompt execution
- `EnvironmentVariables` - additional environment variables forwarded to the child process

Environment overrides are supported with the `CODEBUDDY_` prefix for simple scalar values.

## Build and test

```bash
cd repos/playground/CodeBuddyDotnet
DOTNET_CLI_HOME=$PWD/.dotnet-home \
NUGET_PACKAGES=$PWD/.nuget/packages \
NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache \
  dotnet test CodeBuddyDotnet.slnx
```

## Run fixture scenarios

```bash
cd repos/playground/CodeBuddyDotnet
DOTNET_CLI_HOME=$PWD/.dotnet-home \
NUGET_PACKAGES=$PWD/.nuget/packages \
NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache \
  dotnet run --project CodeBuddySdk.ConsoleDemo/CodeBuddySdk.ConsoleDemo.csproj -- --mode fixture
```

## Run live scenarios

```bash
cd repos/playground/CodeBuddyDotnet
cp appsettings.example.json appsettings.json
# edit appsettings.json so CliPath/Arguments/PromptTransport match your local CodeBuddy CLI usage
DOTNET_CLI_HOME=$PWD/.dotnet-home \
NUGET_PACKAGES=$PWD/.nuget/packages \
NUGET_HTTP_CACHE_PATH=$PWD/.nuget/http-cache \
  dotnet run --project CodeBuddySdk.ConsoleDemo/CodeBuddySdk.ConsoleDemo.csproj -- --mode live
```

## Scenario matrix

| Scenario | Mode | Purpose |
| --- | --- | --- |
| `startup-smoke` | fixture | Validate startup configuration and prove fixture pipeline can pass |
| `tool-event-observation` | fixture | Verify event normalization and transcript capture |
| `auth-classification` | fixture | Verify auth-style failures classify predictably |
| `prompt-success` | live | Send a real prompt and require non-empty output |
| `timeout-classification` | live | Force a tiny timeout and require timeout classification |

## Troubleshooting

- `WorkingDirectory does not exist`
  - Point `WorkingDirectory` at a valid local checkout before running
- `Arguments mode requires at least one argument containing {prompt} or {promptFile}`
  - Add a prompt placeholder when using `PromptTransport=Arguments`
- `Live scenarios are disabled`
  - Set `EnableLiveScenarios` to `true` in `appsettings.json`
- `MissingExecutable`
  - Install CodeBuddy CLI or set `CliPath` to the executable location
- Live prompt returns empty output
  - Adjust `Arguments`, `PromptTransport`, or environment variables to match your local CodeBuddy CLI contract
