# CodexSharpSDK 替代 HagiCode.Libs Codex 对接调研

## 结论先行

`ManagedCode.CodexSharpSDK` 可以替代我们在 `repos/Hagicode.Libs` 里 **Codex CLI 进程驱动、CLI 参数映射、JSONL 事件解析、线程级串行化** 这一层的大部分自研实现，但它 **不能直接替代** 我们在 `hagicode-core` 里已经围绕 `ICliProvider<CodexOptions>` 构建出来的会话绑定、流式 chunk 归一化、工具调用展示和错误恢复逻辑。

因此更合适的落地方式不是“整包 NuGet 引入然后全量替换”，而是：

1. 先把 `CodexSharpSDK` 作为源码参考引入本 vault。
2. 在真实生产仓库里优先做 **源码嵌入 + 兼容适配层**。
3. 第一阶段只替换 `Hagicode.Libs` 内部的 Codex 底层执行实现，保持 `ICliProvider<CodexOptions>` 和 `hagicode-core` 现有调用面不变。

本次调研引入的参考源码位置：`repos/CodexSharpSDK`。
当前仓库头提交：`a3188fe`。
许可证：MIT。

## 我们当前自研实现的边界

当前 HagiCode 里的 Codex 对接主要分成两层：

### 1. `repos/Hagicode.Libs`

核心文件：

- `repos/Hagicode.Libs/src/HagiCode.Libs.Providers/Codex/CodexProvider.cs`
- `repos/Hagicode.Libs/src/HagiCode.Libs.Providers/Codex/CodexExecTransport.cs`
- `repos/Hagicode.Libs/src/HagiCode.Libs.Providers/Codex/CodexOptions.cs`

这一层负责：

- 定位 `codex` 可执行文件
- 构建 `codex exec --experimental-json` 参数
- 通过 stdin 发送 prompt，读取 stdout JSONL
- 将每一行包装成 `CliMessage(type, JsonElement)`
- 识别 `thread.started` / `turn.completed` / `turn.failed` / `error`
- 提供 `PingAsync()` 和基础重入保护

### 2. `repos/hagicode-core`

核心文件：

- `repos/hagicode-core/src/PCode.ClaudeHelper/AI/Providers/CodexCliProvider.cs`

这一层负责：

- 将 `CliMessage` 转成统一的 `AIStreamingChunk`
- 把 Codex 的 item/tool 事件映射到我们自己的 tool call 展示协议
- session thread binding 的 SQLite 持久化
- 权限模式、allowed tools、提示词拼接
- retry / cleanup / 兼容性指纹

所以真正“可被 SDK 替换”的部分主要在 `Hagicode.Libs`，不是 `hagicode-core` 全层。

## CodexSharpSDK 提供了什么

`repos/CodexSharpSDK` 的核心项目是 `CodexSharpSDK/`，关键点如下：

- `Client/CodexClient.cs`
  - 提供 `StartThread()` / `ResumeThread()` 生命周期
- `Client/CodexThread.cs`
  - 提供 `RunAsync()` / `RunStreamedAsync()` / typed output
  - 内置每个 thread 的 `SemaphoreSlim` 串行化
- `Execution/CodexExec.cs`
  - 负责构建 `codex exec --json` 参数、环境变量、进程执行
- `Internal/ThreadEventParser.cs`
  - 将 JSONL 事件解析为强类型 `ThreadEvent` / `ThreadItem`
- `Models/Events.cs` + `Models/Items.cs`
  - 把 `agent_message`、`command_execution`、`mcp_tool_call`、`collab_tool_call`、`web_search`、`todo_list` 等都建模了
- `Internal/CodexCliMetadataReader.cs`
  - 提供版本、默认模型、模型缓存、升级建议读取
- `Extensions.AI` / `Extensions.AgentFramework`
  - 额外适配层，不是第一阶段替换必需品

它的本质不是 HTTP SDK，而是一个更完整的 `.NET codex exec --json` 封装。

## 与我们当前实现的对比

| 维度 | 我们当前实现 | CodexSharpSDK | 结论 |
| --- | --- | --- | --- |
| CLI 调用 | `CodexProvider` + `CodexExecTransport` | `CodexExec` + `DefaultCodexProcessRunner` | SDK 更完整，可替代 |
| 参数映射 | 自己维护 `BuildCommandArguments()` | `CodexExec.BuildCommandArgs()` | SDK 覆盖更全 |
| 环境变量 | `BuildEnvironmentVariables()` | `BuildEnvironment()` | 基本等价 |
| 线程恢复 | `ThreadId` + 外部会话绑定 | `CodexThread.Id` + `ResumeThread()` | SDK 更贴近线程模型 |
| 事件解析 | 主要透传原始 JSON，只做少量终态识别 | `ThreadEventParser` 强类型解析 | SDK 更强，但兼容性要评估 |
| 工具事件 | 上层自己解析原始 item | SDK 已有 typed item model | 有助于减少上层字符串判断 |
| 输出 schema | 当前通过 `ConfigOverrides` / 原始 CLI 能做，但没有统一 typed API | SDK 内建 structured output | SDK 明显更好 |
| CLI metadata | 当前零散或缺失 | SDK 自带 metadata/update 读取 | 可以直接复用 |
| 与现有 `ICliProvider<CodexOptions>` 兼容 | 原生兼容 | 不兼容，需要适配 | 不能直接替换 |
| 未知协议容忍度 | 原始 JSON 透传，较保守 | `ThreadEventParser` 默认对未知类型抛异常 | 这是迁移风险点 |

## 哪些部分适合替换

### 适合直接替换的部分

1. `CodexExecTransport` 的进程管理
2. `CodexProvider.BuildCommandArguments()` 的大部分参数拼装
3. `codex` 可执行文件发现逻辑
4. 结构化输出 schema 文件/参数管理
5. `thread.started` 之后的 thread id 维护
6. 对 `command_execution`、`mcp_tool_call`、`collab_tool_call`、`todo_list` 的 typed 建模

### 不适合第一阶段直接替换的部分

1. `ICliProvider<CodexOptions>` 这个公共接口
2. `CliMessage` 这个当前上层大量依赖的原始消息形态
3. `hagicode-core` 中围绕 `CliMessage` 搭建的流式 chunk 归一化
4. session binding 的持久化策略
5. retry / owned process cleanup / permission contract 这些 HagiCode 特有逻辑

## 最大风险

### 1. 未知协议事件的处理策略更激进

我们当前实现对 Codex JSONL 基本是“读取并透传”；只要 `type` 在，很多新字段不会拦住上层。

`CodexSharpSDK` 当前 `ThreadEventParser` 对未知 event / item 类型会直接抛异常。这意味着一旦 OpenAI 给 CLI 增了新事件，我们的生产链路可能会从“新字段暂时忽略”变成“直接失败”。

如果要源码嵌入，建议第一时间把这一点改成：

- 未知 event -> `UnknownThreadEvent`
- 未知 item -> `UnknownThreadItem`
- 保留原始 `JsonElement` / `JsonNode`

不改这一点，不建议直接上生产替换。

### 2. SDK 的抽象层比我们更高

`CodexSharpSDK` 以 `CodexClient` / `CodexThread` 为主 API，而我们当前生产系统的稳定接口是 `ICliProvider<CodexOptions> -> IAsyncEnumerable<CliMessage>`。

如果直接把 `CodexThread` 推进 `hagicode-core`，变更面会从 `Hagicode.Libs` 扩散到 `hagicode-core` 流式协议层，首轮替换成本偏高。

### 3. 额外扩展包暂时没有必要

`Extensions.AI` 和 `Extensions.AgentFramework` 对未来统一接入 `Microsoft.Extensions.AI` 可能有价值，但当前 HagiCode 现有的 provider 体系并不依赖它们。第一阶段引入只会增大 blast radius。

## 推荐的源码嵌入方式

推荐顺序如下。

### 方案 A：保守替换，优先推荐

在真实生产仓库中，以源码方式只嵌入 `CodexSharpSDK` 的核心项目代码，并写一个 HagiCode 适配层：

- 保留 `HagiCode.Libs.Providers.Codex.CodexOptions`
- 保留 `ICliProvider<CodexOptions>`
- 新建一个内部适配器，例如：
  - `HagiCode.Libs.Providers.Codex.Internal.CodexSharpBackedRunner`
  - 或 `HagiCode.Libs.Providers.Codex.CodexSharpProvider`
- 由该适配器完成：
  - `CodexOptions` -> `CodexExecArgs` / `ThreadOptions` 映射
  - `ThreadEvent` -> `CliMessage` 回写，保持对上层兼容

优点：

- `hagicode-core` 基本不用动
- 可以逐步替换底层实现
- 失败时可快速切回当前 provider

缺点：

- 初期会保留一层“typed event 再转 raw message”的折返

### 方案 B：结构性替换，第二阶段再做

在 `Hagicode.Libs` 直接暴露 `CodexThread` / `ThreadEvent` 风格 API，再让 `hagicode-core` 改成消费强类型事件。

优点：

- 长期维护成本更低
- 工具调用、todo、web_search、collab tool 能减少大量字符串判断

缺点：

- 改动面大
- 需要同步改 `hagicode-core`、测试、可能还包括前端的事件适配语义

### 具体嵌入建议

如果按源码嵌入做，我建议：

1. 只嵌入 `CodexSharpSDK/CodexSharpSDK` 核心项目。
2. 不嵌入 `Extensions.AI` / `Extensions.AgentFramework`。
3. 保留 upstream LICENSE 与 commit 记录。
4. 最好以“vendored project”而不是“散拷贝若干 `.cs` 文件”的方式引入。
5. 通过单独目录维护，例如：
   - `repos/Hagicode.Libs/src/ThirdParty/ManagedCode.CodexSharpSDK/`
   - 或 `repos/Hagicode.Libs/src/HagiCode.Libs.Vendored.CodexSharpSDK/`
6. 写一个同步说明文件，记录 upstream 仓库、commit、裁剪差异。

这样做的重点是：**让 upstream 同步成为可持续动作，而不是一次性复制粘贴。**

## 建议的最小试点范围

第一阶段试点建议只做下面这些：

1. 保持 `CodexOptions` 和 `ICliProvider<CodexOptions>` 不变。
2. 用嵌入的 `CodexExec` 取代当前 `CodexExecTransport` + 一部分 `BuildCommandArguments()`。
3. 把 `ThreadEventParser` 先作为内部解析器引入，但加上 unknown event/item 容错。
4. 继续向上输出 `CliMessage`，不动 `hagicode-core`。
5. 在 `HagiCode.Libs` 里补一组对照测试：
   - 当前 provider fixture
   - CodexSharpSDK-backed fixture
   - 确保 `thread.started`、`item.updated`、`item.completed`、`turn.completed`、`turn.failed`、`error` 的输出契约一致

如果这一步跑通，再考虑第二阶段把 `hagicode-core` 改成直接消费 typed event。

## 最终建议

我的建议是：

- **采纳 `CodexSharpSDK`，但不要直接整包替换。**
- **优先采用源码嵌入，而不是 NuGet 依赖。**
- **第一阶段只替换 `Hagicode.Libs` 里的 Codex 底层执行层，并保持上层接口稳定。**
- **在真正接入前，先修改其 unknown event/item 策略，否则协议漂移风险过高。**

换句话说，这个项目很适合成为我们 Codex 对接的“新底盘”，但暂时不适合被当成“现成总成”直接塞进 HagiCode 生产链路。
