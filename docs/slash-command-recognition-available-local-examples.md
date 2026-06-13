# Slash Command Recognition - 可用本地示例代码盘点

Date: 2026-05-20

## 结论先行

本 monorepo 中有 **三组可靠示例** 可直接作为 playground 参考，覆盖 slash command 识别的三个核心环节：

| 环节 | 示例来源 | 关键文件 | 演示的模式 |
|------|----------|----------|------------|
| 命令解析与分发 | `skillsbase` CLI | `cli.ts` + `parse-argv.ts` | commandMap dispatch、参数结构化解析 |
| 技能注册与发现 | `myskills` + `skillsbase` | `manifest.ts` + `sources.yaml` + `SKILL.md` | YAML manifest 驱动的技能发现 |
| 命令执行事件处理 | `codex-tool-call-adapter-display` | `validate-tool-events.mjs` + fixtures | JSONL 事件归一化、状态机、回退 ID |

三组代码形成完整链路：**解析输入 → 查找注册表 → 归一化执行事件**。

---

## 一、命令解析与分发：skillsbase CLI

**源路径**: `/home/newbe36524/repos/hagicode-mono/repos/skillsbase/`

### 1.1 核心：commandMap 分发模式

`src/cli.ts:21-28`:

```typescript
type CommandHandler = (context: CommandContext) => Promise<CommandResult>;

const commandMap = new Map<string, CommandHandler>([
  ["init", runInitCommand],
  ["sync", runSyncCommand],
  ["add", runAddCommand],
  ["remove", runRemoveCommand],
  ["github_action", runGithubActionCommand],
  ["github-action", runGithubActionCommand],  // 别名支持
]);
```

**模式要点**:
- 字符串到处理函数的 Map 注册
- 支持命令别名（`github_action` / `github-action` 映射同一处理函数）
- 统一的 `CommandContext` 输入和 `CommandResult` 输出

分发逻辑 (`src/cli.ts:58-66`):

```typescript
const command = commandMap.get(parsed.command);
if (!command) {
  throw new CliError(`Unknown command: ${parsed.command}`, {
    exitCode: 1,
    details: ["Use `skillsbase --help` to view supported commands."],
  });
}

const result = await command({
  cwd, env, io,
  command: parsed.command,
  args: parsed.args,
  flags: parsed.flags,
  rawArgv: argv,
});
```

**可复用到 slash command 的关键设计**:
- `CliError` 携带 `exitCode`、`details`、`nextSteps` — 结构化错误信息
- 未知命令不 crash，返回引导性错误
- `CommandContext` 包含完整的执行环境（cwd、env、io、args、flags）

### 1.2 参数解析器：parse-argv.ts

`src/lib/parse-argv.ts:12-74`:

```typescript
export function parseArgv(argv: string[]): ParsedArgv {
  const result: ParsedArgv = {
    command: null,
    args: [],
    flags: {},
    help: false,
    version: false,
  };

  let index = 0;
  while (index < argv.length) {
    const token = argv[index];

    // 首个非 '-' token 作为命令名
    if (result.command == null && !token.startsWith("-")) {
      result.command = token;
      index += 1;
      continue;
    }

    // 布尔标志（--help, --version, --check, --force 等）
    if (token.startsWith("--")) {
      const [flagName, inlineValue] = token.slice(2).split("=", 2);
      if (booleanFlags.has(flagName)) {
        result.flags[flagName] = inlineValue == null ? true : inlineValue !== "false";
        index += 1;
        continue;
      }
      // 键值标志：--key value 或 --key=value
      const nextValue = inlineValue ?? argv[index + 1];
      if (nextValue == null) {
        throw new CliError(`Missing value for --${flagName}.`);
      }
      result.flags[flagName] = nextValue;
      index += inlineValue == null ? 2 : 1;
      continue;
    }

    // 拒绝短选项（强制使用长选项，避免歧义）
    if (token.startsWith("-")) {
      throw new CliError(`Unsupported short option: ${token}`);
    }

    // 其余 token 作为位置参数
    if (result.command != null) {
      result.args.push(token);
    }
    index += 1;
  }

  return result;
}
```

**模式要点**:
- 布尔标志与键值标志分离（booleanFlags 集合注册）
- 支持 `--flag=value` 内联赋值
- 位置参数与标志独立收集
- 拒绝短选项（`-f`），强制长选项，减少歧义

**可复用到 slash command 的设计**:
- 输入 `/research topic --depth 3 --verbose` 时：
  - `command` = `research`
  - `args` = `["topic"]`
  - `flags` = `{ depth: "3", verbose: true }`

---

## 二、技能注册与发现：myskills + skillsbase manifest

**源路径**:
- `/home/newbe36524/repos/hagicode-mono/repos/myskills/`
- `/home/newbe36524/repos/hagicode-mono/repos/skillsbase/src/lib/manifest.ts`

### 2.1 YAML Manifest 声明

`myskills/sources.yaml`:

```yaml
version: 1
skillsRoot: skills
metadataFile: .skill-source.json
managedBy: skillsbase
remoteRepository: newbe36524/myskills
staleCleanup: true
skillsCliVersion: 1.4.8
installAgent: codex
sources:
  - key: github-awesome-copilot
    label: "GitHub Awesome Copilot skills"
    kind: github-repository
    root: github/awesome-copilot
    targetPrefix: ""
    include:
      - documentation-writer
  - key: op7418-humanizer-zh
    label: "humanizer-zh repository"
    kind: github-repository
    root: op7418/humanizer-zh
    targetPrefix: ""
    include:
      - humanizer-zh
```

**模式要点**:
- 多 source 汇聚到一个 registry
- `include` 列表精确控制注册哪些 skill
- `targetPrefix` 支持名称空间隔离
- `kind: github-repository` 表明远程来源

### 2.2 SKILL.md 定义格式

`myskills/skills/documentation-writer/SKILL.md` (frontmatter):

```yaml
---
name: documentation-writer
description: 'Diátaxis Documentation Expert. ...'
---
```

**模式要点**:
- YAML frontmatter 声明 name + description
- 正文是完整的行为指令（prompt template）
- 文件名即为技能标识符

### 2.3 Manifest 加载与解析

`skillsbase/src/lib/manifest.ts:290-368` — `loadManifest()`:
- 逐行解析 `sources.yaml`（手写 YAML parser，无第三方依赖）
- 正则识别顶级 key-value、source 块、include 列表
- `validateManifest()` 强制校验必填字段
- `buildManifestEntries()` 生成扁平化的安装目标列表，带碰撞检测

**碰撞检测** (`manifest.ts:401-418`):

```typescript
const collisions = new Map<string, string[]>();
for (const entry of entries) {
  const keys = collisions.get(entry.targetName) ?? [];
  keys.push(entry.sourceKey);
  collisions.set(entry.targetName, keys);
}

const duplicateTargets = [...collisions.entries()].filter(([, keys]) => keys.length > 1);
if (duplicateTargets.length > 0) {
  throw new CliError(`Manifest target-name collision detected: ${rendered}`);
}
```

**可复用到 slash command 的设计**:
- 注册表启动时检查命令名冲突
- 多来源（内置 / 文件系统 / 远程）汇聚到统一注册表
- `addSkillToManifest()` / `removeSkillFromManifest()` — 增删改查操作

---

## 三、命令执行事件处理：codex-tool-call-adapter-display

**源路径**: `samples/codex-tool-call-adapter-display/`

### 3.1 JSONL 事件归一化

`validate-tool-events.mjs:63-104` — `normalizeEvents()`:

```javascript
function normalizeEvents(events) {
  const normalized = [];
  const fallbackMap = new Map();
  const activeTools = new Map();  // 跟踪运行中的工具
  let turnNumber = 0;

  for (const event of events) {
    if (event.type === 'turn.started') {
      turnNumber += 1;
      continue;
    }

    if (event.type === 'item.started' || event.type === 'item.updated' || event.type === 'item.completed') {
      const normalizedEvent = buildToolEvent(event.type, event.item, turnNumber, fallbackMap);
      if (!normalizedEvent) continue;

      if (normalizedEvent.status === 'running') {
        activeTools.set(normalizedEvent.toolCallId, normalizedEvent);
      } else {
        activeTools.delete(normalizedEvent.toolCallId);
      }

      normalized.push(normalizedEvent);
      continue;
    }

    // turn.failed 时回填所有运行中的工具为 failed
    if (event.type === 'turn.failed') {
      for (const [toolCallId, runningTool] of activeTools.entries()) {
        normalized.push({
          ...runningTool,
          status: 'failed',
          errorSummary: summarizeText(event.error?.message || 'turn.failed'),
        });
      }
      activeTools.clear();
    }
  }

  return normalized;
}
```

**模式要点**:
- 状态机：`activeTools` Map 跟踪 running 状态的工具
- `turn.failed` 时自动将所有 running 工具回填为 failed
- 事件归一化为统一结构：`{ toolCallId, status, argumentSummary, resultSummary, errorSummary }`

### 3.2 确定性回退 ID

`validate-tool-events.mjs:25-38` — `resolveToolCallId()`:

```javascript
function resolveToolCallId(rawId, turnNumber, toolName, fingerprint, fallbackMap) {
  if (rawId && String(rawId).trim().length > 0) {
    return rawId;  // 优先使用协议提供的 ID
  }

  const key = `${turnNumber}:${toolName}:${fingerprint}`;
  if (fallbackMap.has(key)) {
    return fallbackMap.get(key);  // 缓存一致性
  }

  // SHA256 派生确定性 ID
  const hash = crypto.createHash('sha256').update(key).digest('hex').slice(0, 12);
  const fallback = `codex-turn-${turnNumber}-${hash}`;
  fallbackMap.set(key, fallback);
  return fallback;
}
```

**模式要点**:
- 优先使用协议原始 ID
- 缺失时用 SHA256 派生确定性回退 ID
- 回退 Map 保证同一事件流内 ID 一致

### 3.3 Fixture 测试结构

5 个 JSON fixture 覆盖全部场景：

| Fixture | 场景 | 事件流 |
|---------|------|--------|
| `success.json` | 正常完成 | `item.started(running)` → `item.completed(completed)` |
| `failure.json` | 命令失败 | `item.started(running)` → `item.completed(failed)` + `turn.failed` |
| `timeout.json` | 超时 | `item.started(running)` → `turn.failed`（回填） |
| `empty-result.json` | 空结果 | `item.started(running)` → `item.completed(completed, empty)` |
| `missing-id.json` | 缺失 ID | `item.started(running)` → `item.completed(completed)` + 回退 ID |

**事件协议结构**（以 `success.json` 为例）:

```json
{
  "name": "success",
  "events": [
    {"type": "thread.started", "thread_id": "thread-success"},
    {"type": "turn.started"},
    {
      "type": "item.started",
      "item": {
        "id": "cmd-success-1",
        "type": "command_execution",
        "command": "ls -la",
        "status": "running"
      }
    },
    {
      "type": "item.completed",
      "item": {
        "id": "cmd-success-1",
        "type": "command_execution",
        "command": "ls -la",
        "aggregated_output": "total 3\n-rw-r--r-- file.txt",
        "exit_code": 0,
        "status": "completed"
      }
    },
    {"type": "turn.completed", "usage": {"input_tokens": 16, "output_tokens": 28}}
  ],
  "expected": {
    "toolCallId": "cmd-success-1",
    "statuses": ["running", "completed"],
    "terminal": "completed",
    "resultSummaryContains": "total 3"
  }
}
```

---

## 四、已有 Playground 中的 .NET CLI 模式

两个本地 C# CLI 示例也可作为对比参考：

### 4.1 GitCompatibilityTest（三模式 CLI）

`samples/GitCompatibilityTest/Program.cs`:
- `--mode <compatibility|benchmark|summarize>` 三模式分发
- 手写 `CliOptions.Parse()` 参数解析器（switch-case 风格）
- `ValidationMode` enum 替代字符串命令

### 4.2 DoubaoVoice.Cli（最小 CLI）

`samples/DoubaoDotnet/DoubaoVoice.Cli/Program.cs`:
- 位置参数 + 可选标志（`--url`, `--sample-rate`, `--model`）
- 最简单的 `ParseArguments()` 模式

---

## 五、推荐放入 Playground 的示例组合

### 示例 A：TypeScript 技能管理器（推荐提取）

从 `skillsbase` 提取核心模式，做成独立可运行的 playground 示例：

```
samples/slash-command-recognition/
├── README.md
├── command-registry.ts        # 从 cli.ts 提取的 commandMap 模式
├── parse-argv.ts              # 从 parse-argv.ts 提取的参数解析
├── manifest-loader.ts         # 从 manifest.ts 提取的注册表加载
├── fixtures/
│   ├── slash-success.json     # 从 success.json 改编
│   ├── slash-failure.json     # 从 failure.json 改编
│   └── slash-missing-id.json  # 从 missing-id.json 改编
└── validate-slash-events.mjs  # 从 validate-tool-events.mjs 改编
```

### 示例 B：.NET slash command 解析器

参考 `GitCompatibilityTest` 的 `CliOptions.Parse()` 模式，但改为 slash command 风格：

```csharp
// 从 /research topic --depth 3 解析为：
// command = "research", args = ["topic"], flags = { "depth" = "3" }
```

---

## 六、关键文件索引

| 类别 | 文件 | 模式 |
|------|------|------|
| 命令分发 | `repos/skillsbase/src/cli.ts` | commandMap dispatch |
| 参数解析 | `repos/skillsbase/src/lib/parse-argv.ts` | 标志 + 位置参数 |
| 注册表管理 | `repos/skillsbase/src/lib/manifest.ts` | YAML manifest + 碰撞检测 |
| 技能声明 | `repos/myskills/sources.yaml` | 多源汇聚 |
| 技能定义 | `repos/myskills/skills/*/SKILL.md` | frontmatter + prompt |
| 事件归一化 | `samples/codex-tool-call-adapter-display/validate-tool-events.mjs` | JSONL → 统一事件 |
| 测试 fixture | `samples/codex-tool-call-adapter-display/fixtures/*.json` | 5 场景覆盖 |
| .NET CLI A | `samples/GitCompatibilityTest/Program.cs` | 三模式 enum 分发 |
| .NET CLI B | `samples/DoubaoDotnet/DoubaoVoice.Cli/Program.cs` | 最小位置参数 |

---

## 七、限制

- `repos/` 下的 codex、code-server、CodexSharpSDK、CliWrap、seomachine 目录均为空（未 checkout）
- 以上分析基于 **已 checkout 的 `skillsbase`、`myskills` 和本地 `samples/`**
- 无法直接引用 CodexSharpSDK 的 `ThreadEventParser` 源码，只能从 `codexsharp-sdk-replacement-research.md` 间接推导
- impeccable 技能没有本地文件定义，它是 Claude Code 内置技能，仅在 system prompt 中声明
