# CodexSdk Usage Guide (C#)

This document explains how to use the new `CodexSdk` in `repos/playground/CodexDotnet/CodexSdk`.

## 1) Add the SDK to your app

If your app is in the same repository, add a project reference:

```bash
dotnet add <YourApp>.csproj reference repos/playground/CodexDotnet/CodexSdk/CodexSdk.csproj
```

Then import:

```csharp
using CodexSdk;
```

## 2) Quick start

If your local Codex CLI is already configured (for example via `~/.codex/config.toml`), you usually do not need to set `BaseUrl` or API key in code.

```csharp
using CodexSdk;

var codex = new Codex();
var thread = codex.StartThread();

var result = await thread.RunAsync("Summarize this repository in 3 bullet points.");
Console.WriteLine(result.FinalResponse);
```

## 3) Continue the same conversation

Call `RunAsync` multiple times on the same `CodexThread`:

```csharp
await thread.RunAsync("Find the main build command.");
await thread.RunAsync("Now suggest one optimization.");
```

## 4) Stream events in real time

Use `RunStreamedAsync` when you want intermediate events (tool calls, updates, final usage):

```csharp
await foreach (var ev in thread.RunStreamedAsync("Diagnose test failures and propose a fix."))
{
    switch (ev)
    {
        case ItemCompletedEvent itemCompleted when itemCompleted.Item is AgentMessageItem msg:
            Console.WriteLine($"Assistant: {msg.Text}");
            break;
        case TurnCompletedEvent completed:
            Console.WriteLine($"Tokens: in={completed.Usage.InputTokens}, out={completed.Usage.OutputTokens}");
            break;
    }
}
```

## 5) Thread options (model, sandbox, working directory)

Set execution behavior when creating the thread:

```csharp
var thread = codex.StartThread(new ThreadOptions
{
    Model = "gpt-5.3-codex",
    SandboxMode = "workspace-write",
    WorkingDirectory = "/path/to/project",
    ApprovalPolicy = "on-request",
});
```

Supported key option fields:

- `Model`
- `SandboxMode` (`read-only`, `workspace-write`, `danger-full-access`)
- `WorkingDirectory`
- `SkipGitRepoCheck`
- `ModelReasoningEffort`
- `NetworkAccessEnabled`
- `WebSearchMode`
- `WebSearchEnabled`
- `ApprovalPolicy`
- `AdditionalDirectories`

## 6) Structured output (JSON schema)

Pass a schema through `TurnOptions.OutputSchema`:

```csharp
using System.Text.Json.Nodes;

var schema = new JsonObject
{
    ["type"] = "object",
    ["properties"] = new JsonObject
    {
        ["summary"] = new JsonObject { ["type"] = "string" },
        ["status"] = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = new JsonArray("ok", "action_required"),
        },
    },
    ["required"] = new JsonArray("summary", "status"),
    ["additionalProperties"] = false,
};

var result = await thread.RunAsync(
    "Summarize repository status.",
    new TurnOptions { OutputSchema = schema });

Console.WriteLine(result.FinalResponse);
```

## 7) Send text + local images

Use the `UserInputItem` list overload:

```csharp
var result = await thread.RunAsync(new UserInputItem[]
{
    new TextInput("Describe what changed in this screenshot."),
    new LocalImageInput("./ui.png"),
});
```

## 8) Resume an existing thread

```csharp
var resumed = codex.ResumeThread("your-thread-id");
var result = await resumed.RunAsync("Continue from where we stopped.");
```

`resumed.Id` remains the same thread id.

## 9) Optional Codex constructor options

You can still override defaults when needed:

```csharp
using System.Text.Json.Nodes;

var codex = new Codex(new CodexOptions
{
    CodexPathOverride = "/custom/path/to/codex",
    BaseUrl = "https://api.openai.com/v1",
    ApiKey = "YOUR_KEY",
    Config = new JsonObject
    {
        ["show_raw_agent_reasoning"] = true,
    },
});
```

Notes:

- `BaseUrl` and `ApiKey` are optional. If omitted, Codex CLI uses its normal local config/env resolution.
- `Env` lets you fully control process environment variables for the spawned `codex` process.

## 10) Run the included demo

Interactive sample app:

```bash
cd repos/playground/CodexDotnet
dotnet run --project CodexSdk.ConsoleDemo/CodexSdk.ConsoleDemo.csproj
```

It will print streamed events, command execution updates, and token usage.
