# Hermes Main Project Integration Guide

## Purpose

This document is for the future migration from the playground-only `HermesAcpDotnet` experiment into a main HagiCode project. It summarizes what has already been proven, what still belongs to the playground, and how to integrate Hermes with less rediscovery work later.

The current verified implementation lives under:

- `repos/playground/HermesAcpDotnet/HermesAcpSdk/`
- `repos/playground/HermesAcpDotnet/HermesAcpSdk.ConsoleDemo/`
- `repos/playground/HermesAcpDotnet/HermesAcpSdk.Tests/`

## What Has Already Been Proven

The playground is no longer only a mock design. It has been validated against a real local `hermes` executable.

### Confirmed ACP behavior

- Hermes can be launched locally with `hermes acp`
- Hermes accepts ACP `initialize`
- Hermes may not emit `//ready`; it can reply directly to `initialize`
- Hermes can advertise auth methods dynamically during `initialize`
- Hermes accepts `authenticate`
- Hermes requires `mcpServers` in `session/new`, even when empty
- Hermes can create a session with `session/new`
- Hermes can answer `session/prompt`
- Hermes may stream the final useful text through `session/update` notifications instead of only placing it in the final RPC result

### Confirmed stable scenarios

The following real scenarios now exist as regression coverage:

- `PingPrompt_ReturnsPong_WhenRealHermesRegressionIsEnabled`
- `RepositoryAnalysis_ReturnsRepoSpecificPaths_WhenRealHermesRegressionIsEnabled`

See:

- `repos/playground/HermesAcpDotnet/HermesAcpSdk.Tests/RealHermesRegressionTests.cs`

## What Should Be Reused in Main Project

If Hermes is migrated into a production-facing project, the most reusable parts are the SDK and protocol layers, not the console shell.

### Reuse directly

- `HermesAcpSdk/Configuration/`
  - launch profile binding
  - auth/session/artifact configuration models
- `HermesAcpSdk/Protocol/`
  - ACP message builders
  - ACP response parsing
  - prompt update aggregation
- `HermesAcpSdk/Transport/`
  - process startup
  - stdio transport
  - transcript capture
- `HermesAcpSdk/Runtime/`
  - bootstrap orchestration
  - feature-stage reporting

### Do not migrate as-is

- `HermesAcpSdk.ConsoleDemo/`
  - this is a manual operator surface, not a production API
- Playground-specific fixture tests
  - keep them in playground unless the main project needs local adapter simulation

## Recommended Migration Shape

If Hermes is added to a main project such as `repos/hagicode-core`, use a layered migration instead of copying the whole playground.

### Suggested layering

```text
Main Project Application Layer
|-- Hermes provider service / app service
|-- session orchestration
|-- persistence / telemetry / API exposure

Hermes ACP Integration Layer
|-- HermesSessionRunner
|-- HermesAcpClient
|-- HermesAcpMessageFactory
|-- HermesAcpMessageParser
|-- HermesProcessRunner
|-- StdioAcpTransport

Infrastructure / Host Layer
|-- config binding
|-- executable resolution
|-- secrets / environment variables
|-- artifact policy
```

### Practical recommendation

Extract the reusable playground code into one of these shapes:

1. A new shared project inside the target repo, such as `PCode.HermesAcp`
2. A transplanted SDK folder kept internal to the target repo first, then generalized later

For the first migration, prefer option 2 if speed matters. Generalization can happen after the first production use case is stable.

## Candidate Integration Targets

The exact final home depends on the product requirement.

### If Hermes becomes a backend provider

Likely target:

- `repos/hagicode-core/`

Potential responsibilities:

- create Hermes-backed sessions
- send prompts through ACP
- map Hermes output into existing domain/session models
- record telemetry and diagnostics

### If Hermes becomes a desktop-managed runtime

Likely touch points:

- `repos/hagicode-desktop/src/main/`

Potential responsibilities:

- configure local Hermes executable path
- validate runtime prerequisites
- manage launch profiles and diagnostics

### If Hermes remains an operator-only experiment

Keep it in:

- `repos/playground/HermesAcpDotnet/`

and continue using it as a compatibility harness before touching production.

## Mapping From Playground to Main Project Responsibilities

| Playground Piece | Main Project Responsibility |
| --- | --- |
| `HermesLaunchProfile` | environment-aware runtime/provider configuration |
| `HermesProcessRunner` | child process bootstrap and lifecycle |
| `StdioAcpTransport` | ACP message transport |
| `HermesAcpClient` | request/response bridge to Hermes ACP |
| `HermesSessionRunner` | end-to-end bootstrap and prompt orchestration |
| `RawTranscriptCapture` | diagnostics, supportability, audit trail |
| `RunArtifactWriter` | optional debug artifact export, not always production default |
| `RealHermesRegressionTests` | production integration acceptance gate |

## Integration Contract to Preserve

When migrating, preserve these behaviors because they were learned from the real Hermes implementation:

### 1. Do not require `//ready`

The integration must tolerate either of these startup styles:

- provider emits `//ready` before RPC responses
- provider immediately responds to `initialize`

The current implementation already supports both.

### 2. Always send `mcpServers`

`session/new` must include:

- `cwd`
- `mcpServers`

Even if there are no MCP servers, Hermes currently expects `mcpServers: []`.

### 3. Aggregate final text from streaming updates

Do not assume the final prompt answer only comes from the final `result`.

The implementation must read both:

- `session/update`
- final `session/prompt` response

### 4. Make authentication capability-driven

Auth must be based on `initialize.authMethods`, not on hard-coded assumptions.

Good production behavior:

- if auth methods exist, select a configured or first available method
- if auth methods do not exist, record auth as skipped

## Configuration Model to Carry Forward

The following settings have proven useful and should survive migration:

- executable path
- argument list
- working directory
- environment variables
- timeout seconds
- auth method preference
- session defaults
- artifact output policy

Current examples:

- `repos/playground/HermesAcpDotnet/appsettings.example.json`
- `repos/playground/HermesAcpDotnet/HermesAcpSdk/Configuration/`

For production migration, add two more concerns:

- secrets source integration
  - environment variables, vault, or secure server-side config
- policy separation
  - operator diagnostics settings should not be mixed with user-facing runtime settings

## Suggested Production Interfaces

Before moving code into a main project, define narrow interfaces around the ACP runtime so the application layer does not depend on every playground detail.

Example direction:

```csharp
public interface IHermesSessionGateway
{
    Task<HermesSessionHandle> ConnectAsync(CancellationToken cancellationToken);
    Task<HermesPromptOutcome> PromptAsync(string sessionId, string prompt, CancellationToken cancellationToken);
}
```

```csharp
public interface IHermesLaunchProfileProvider
{
    Task<HermesLaunchProfile> GetProfileAsync(string profileName, CancellationToken cancellationToken);
}
```

The main project should depend on interfaces like these, while the ACP-specific implementation can stay in a dedicated infrastructure layer.

## Logging, Artifacts, and Supportability

The playground writes rich local artifacts because debugging protocol mismatches is hard without evidence.

Production integration should keep that idea, but adjust the policy:

### Keep by default

- structured stage result
- sanitized launch summary
- failure stage and message
- timing metrics

### Keep only for debug or opt-in support mode

- full raw transcript
- stderr dumps
- markdown report files

### Always redact

- keys
- tokens
- secrets
- passwords

See the existing redaction-friendly launch summary logic in:

- `repos/playground/HermesAcpDotnet/HermesAcpSdk/Configuration/HermesLaunchProfile.cs`

## Recommended Migration Steps

### Phase 1 - Internal extraction

- copy or move the reusable ACP integration layer into the target repo
- keep public surface area small
- do not expose playground-specific console features

### Phase 2 - Host integration

- bind configuration from the target project's config system
- wire logging and telemetry
- define artifact policy
- add graceful process cleanup and timeout handling to the host lifecycle

### Phase 3 - Domain mapping

- map Hermes prompt output into existing session/message abstractions
- decide where streamed updates should appear in the product
- normalize Hermes-specific statuses into project-specific result models

### Phase 4 - Verification gates

- port the real Hermes regression tests
- keep one simple probe test (`PONG`)
- keep one realistic repository/task test
- run them in CI only when Hermes credentials/runtime are intentionally available

## Acceptance Checklist for Production Migration

- [ ] `initialize` succeeds against the real Hermes runtime
- [ ] auth method selection is dynamic
- [ ] `session/new` sends `mcpServers`
- [ ] `session/prompt` supports streamed text from updates
- [ ] errors identify the failed stage clearly
- [ ] secrets are redacted from persisted diagnostics
- [ ] a simple probe test passes
- [ ] a realistic task-oriented regression test passes
- [ ] shutdown does not leave orphaned Hermes processes

## Known Risks

- Hermes CLI arguments may change across versions
- Hermes ACP payload shapes may evolve
- authentication method identifiers may differ by runtime/provider setup
- streamed response semantics may become richer than the current parser expects

This is why the playground should remain the first compatibility checkpoint even after production integration begins.

## Recommended Reading Order

If someone needs to integrate Hermes into the main project later, this is the fastest path:

1. `repos/playground/HermesAcpDotnet/README.md`
2. `repos/playground/HermesAcpDotnet/HERMES_MAIN_PROJECT_INTEGRATION.md`
3. `repos/playground/HermesAcpDotnet/HermesAcpSdk/Runtime/HermesSessionRunner.cs`
4. `repos/playground/HermesAcpDotnet/HermesAcpSdk/Protocol/HermesAcpMessageFactory.cs`
5. `repos/playground/HermesAcpDotnet/HermesAcpSdk/Protocol/HermesAcpMessageParser.cs`
6. `repos/playground/HermesAcpDotnet/HermesAcpSdk.Tests/RealHermesRegressionTests.cs`

That sequence is usually enough to understand both the integration shape and the real-world protocol differences already discovered.
