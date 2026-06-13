# Slash Command Recognition Implementation Guide Based on Codex

Date: 2026-05-20

## Research Context

This document consolidates findings from the CodeRef vault about implementing slash command recognition, drawing from:
- `codexsharp-sdk-replacement-research.md` - Codex CLI event protocol
- `seomachine-usage-and-hagicode-integration-research.md` - Claude Code slash command patterns
- `npm-wrap-argument-passthrough-cliwrap-research.md` - CLI argument handling best practices
- Local CLI implementations in `samples/`

## Executive Summary

Based on the vault evidence, implementing slash command recognition requires understanding **two different approaches**:

1. **Codex-style**: Protocol-level `command_execution` events in JSONL stream
2. **Claude Code-style**: File-based `.claude/commands/*.md` definitions

The two approaches serve different purposes and can complement each other in a complete implementation.

---

## Approach 1: Codex Protocol-Level Command Recognition

### What Codex Provides

Based on the CodexSharpSDK research, Codex exposes a `command_execution` event type in its JSONL protocol:

```json
{
  "type": "item",
  "item_type": "command_execution",
  "status": "running",
  "command": "/research",
  "arguments": {...}
}
```

### Key Implementation Points

From `codexsharp-sdk-replacement-research.md`:

1. **Typed Event Models**: CodexSharpSDK models `command_execution` as a strong type:
   ```csharp
   // Models/Items.cs includes:
   // - agent_message
   // - command_execution
   // - mcp_tool_call
   // - collab_tool_call
   // - web_search
   // - todo_list
   ```

2. **Event Parser**: `ThreadEventParser.cs` parses JSONL into typed events
   - Unknown event types throw exceptions (migration risk noted in research)
   - Needs fallback to `UnknownThreadEvent` for protocol drift tolerance

3. **Lifecycle Events**: Command execution follows:
   - `item.updated` with `status: "running"`
   - `item.completed` or `item.failed` with final result
   - `turn.failed` for timeout/backfill scenarios

### Implementation Pattern for Codex Commands

```csharp
// Pattern from CodexSharpSDK research
public class CommandExecutionContext
{
    public string Command { get; set; }        // e.g., "/research"
    public JsonElement Arguments { get; set; } // Command-specific args
    public string Status { get; set; }         // running/completed/failed
    public string ToolCallId { get; set; }     // For UI binding
}

// Recognition logic from JSONL stream
if (eventType == "item" && itemType == "command_execution")
{
    var commandEvent = ParseCommandEvent(jsonLine);
    // Dispatch based on commandEvent.Command
}
```

### Critical Risk from Research

> "CodexSharpSDK 当前 `ThreadEventParser` 对未知 event / item 类型会直接抛异常。这意味着一旦 OpenAI 给 CLI 增了新事件，我们的生产链路可能会从"新字段暂时忽略"变成"直接失败"。"

**Mitigation Strategy**: 
- Implement unknown event/item fallback with raw `JsonElement` preservation
- Log unknown types for monitoring without failing the stream

---

## Approach 2: Claude Code-Style File-Based Commands

### What seomachine Demonstrates

From `seomachine-usage-and-hagicode-integration-research.md`:

- **24 command files** under `.claude/commands/`
- Commands include: `research.md`, `write.md`, `rewrite.md`, `optimize.md`, `performance-review.md`, `publish-draft.md`
- Each `.md` file contains:
  - Command description
  - Parameter documentation
  - Prompt template
  - Execution instructions

### File Structure Pattern

```
.claude/
  commands/
    research.md
    write.md
    rewrite.md
  agents/
    research-agent.md
  skills/
    research-skill/
      SKILL.md
```

### Recognition Strategy

For file-based commands:

1. **Discovery Phase**: Scan `.claude/commands/*.md`
2. **Parsing Phase**: Extract frontmatter and content
3. **Registration Phase**: Map command name to file path
4. **Execution Phase**: Load file content, substitute parameters, execute

```csharp
// Pseudo-pattern for file-based command registry
public class SlashCommandRegistry
{
    private Dictionary<string, SlashCommandDefinition> _commands;

    public void DiscoverCommands(string commandsPath)
    {
        foreach (var mdFile in Directory.EnumerateFiles(commandsPath, "*.md"))
        {
            var definition = ParseCommandDefinition(mdFile);
            _commands[definition.Name] = definition;
        }
    }

    public SlashCommandDefinition GetCommand(string commandName)
    {
        // Support: /research, research, / research
        var normalized = commandName.TrimStart('/');
        return _commands.GetValueOrDefault(normalized);
    }
}
```

---

## Approach 3: Hybrid Recognition Strategy

Based on the evidence, a production system should support **both approaches**:

### Recognition Flow

```
User Input: "/research topic"
     ↓
[1] Input Normalization
     - Trim whitespace
     - Strip leading "/" if present
     - Extract command and arguments
     ↓
[2] Command Lookup (Priority Order)
     a. Check Codex protocol events first (if in streaming session)
     b. Check registered file-based commands
     c. Check built-in system commands
     ↓
[3] Command Validation
     - Verify parameters match schema
     - Check permissions/allowed tools
     - Validate argument types
     ↓
[4] Execution Dispatch
     - Codex: Forward to CLI with proper JSON structure
     - File-based: Load template, substitute, execute
     - Built-in: Direct method invocation
```

### Argument Parsing Pattern

From `GitCompatibilityTest/Program.cs` and `DoubaoVoice.Cli/Program.cs`:

```csharp
// Pattern: Custom argument parser for CLI tools
public class SlashCommandParser
{
    public static (string command, Dictionary<string, string> args) Parse(string input)
    {
        var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].TrimStart('/');
        
        var args = new Dictionary<string, string>();
        for (int i = 1; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("--"))
            {
                var key = parts[i].Substring(2);
                if (i + 1 < parts.Length && !parts[i + 1].StartsWith("--"))
                {
                    args[key] = parts[++i];
                }
            }
        }
        
        return (command, args);
    }
}
```

---

## CLI Argument Handling Best Practices

From `npm-wrap-argument-passthrough-cliwrap-research.md`:

### Safe Argument Construction

```csharp
// WRONG: String concatenation
var args = $"run {scriptName} -- {string.Join(" ", userArgs)}";

// CORRECT: Array/builder pattern
using CliWrap;
var command = Cli.Wrap("npm")
    .WithArguments(args =>
    {
        args.Add("run");
        args.Add(scriptName);
        if (userArgs.Count > 0)
        {
            args.Add("--");
            args.Add(userArgs);
        }
    });
```

### Key Principles

1. **Treat each argument as independent token**
2. **Let the argument builder handle escaping**
3. **Avoid pre-constructing argument strings**
4. **Use structured parameter passing**

---

## Implementation Recommendations

### Phase 1: Foundation

1. **Create core abstractions**:
   ```csharp
   public interface ISlashCommand
   {
       string Name { get; }
       string Description { get; }
       Task<CommandResult> ExecuteAsync(CommandContext context);
   }

   public interface ICommandRegistry
   {
       void Register(ISlashCommand command);
       ISlashCommand Resolve(string commandName);
   }
   ```

2. **Implement argument normalizer**:
   - Handle `/command`, `command`, `  command  `
   - Extract key-value pairs with `--key value` syntax
   - Support quoted arguments

3. **Add unknown command handling**:
   - Log attempts
   - Suggest similar commands (fuzzy match)
   - Return helpful error message

### Phase 2: Codex Integration

1. **Adapt CodexSharpSDK patterns** with protocol drift tolerance:
   ```csharp
   public class TolerantEventParser
   {
       public ThreadEvent Parse(JsonElement json)
       {
           try
           {
               return originalParser.Parse(json);
           }
           catch (UnknownEventTypeException ex)
           {
               return new UnknownThreadEvent
               {
                   OriginalJson = json,
                   DetectedType = ex.Type
               };
           }
       }
   }
   ```

2. **Map Codex `command_execution` to internal `ISlashCommand`**:
   - Extract command name from event
   - Bind arguments to context
   - Stream status updates back to Codex

### Phase 3: File-Based Commands

1. **Implement command discovery**:
   - Scan `.claude/commands/` directory
   - Parse markdown frontmatter
   - Register discovered commands

2. **Template execution**:
   - Load command template
   - Substitute parameters
   - Execute and capture results

### Phase 4: Cross-Platform CLI Support

1. **Use CliWrap patterns** for command execution
2. **Handle platform differences**:
   - Windows: `.cmd`/`.bat` detection
   - Unix: direct executable lookup
3. **Safe argument passing** using builder pattern

---

## Risk Assessment

### High Risks

1. **Protocol Drift** (Codex)
   - Unknown event types cause failures
   - Mitigation: Tolerant parser with fallback

2. **Command Collision** (Hybrid)
   - Same name in Codex and file-based
   - Mitigation: Priority ordering + conflict detection

### Medium Risks

1. **Argument Injection**
   - User input in command arguments
   - Mitigation: Strict typing, builder pattern

2. **Permission Escalation**
   - Commands accessing unauthorized resources
   - Mitigation: Permission checks per command

### Low Risks

1. **File System Layout Changes**
   - `.claude/` structure variations
   - Mitigation: Configurable discovery paths

---

## Testing Strategy

### Unit Tests

- Command name normalization
- Argument parsing edge cases
- Unknown command handling
- Protocol drift tolerance

### Integration Tests

- Codex event stream processing
- File-based command discovery
- Cross-platform command execution

### Validation Fixtures

Use pattern from `codex-tool-call-adapter-display/`:
```json
// fixtures/command-success.json
{
  "type": "item",
  "item_type": "command_execution",
  "status": "running",
  "command": "/test"
}
```

---

## Limitations

1. **No actual Codex repository** was available in this vault
2. **Analysis is based on secondary documentation** and research notes
3. **No live protocol testing** was performed
4. **Cross-platform behavior** needs CI validation

---

## References

- CodexSharpSDK Research: `docs/codexsharp-sdk-replacement-research.md`
- SEO Machine Integration: `docs/seomachine-usage-and-hagicode-integration-research.md`
- CLI Argument Handling: `docs/npm-wrap-argument-passthrough-cliwrap-research.md`
- Tool Call Events: `samples/codex-tool-call-adapter-display/`
- CLI Patterns: `samples/GitCompatibilityTest/Program.cs`, `samples/DoubaoDotnet/DoubaoVoice.Cli/Program.cs`
