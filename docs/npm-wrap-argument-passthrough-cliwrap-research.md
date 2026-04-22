# 如何实现 npm 的 wrap 又能支持参数的调用

## 调研范围

- 调研对象：`repos/CliWrap`
- 调研目标：确认 `CliWrap` 是否适合实现一个“包装 npm 并支持参数透传”的 .NET 调用层
- 输出类型：结构化调研笔记，偏 Explanation / How-to

## 本次默认假设

- 这是一次非交互调研，未向需求方补充确认。
- “npm 的 wrap”这里按一个常见需求理解：在 .NET 里封装 `npm` 调用，并把外部传入的参数继续透传给 `npm` 或 `npm run <script>`。
- 与 `npm` 自身语义相关的部分不是来自 `CliWrap` 仓库证据，因此本文只把它当作实现假设，不当作仓库事实。

## Vault 与仓库处理记录

- Vault 根目录、`docs/`、`repos/` 已存在，`index.yaml` 字段完整，可直接使用。
- `repos/` 下原本没有 `CliWrap`，本次已补齐 checkout：`repos/CliWrap`
- 本次检查到的 `CliWrap` HEAD：`a2ab9e82cc7679e8b880cd098084cafd0d50f0b9`

## 结论摘要

`CliWrap` 适合用来做 npm wrapper，关键原因有四点：

1. 它对外暴露的是不可变 `Command`，适合分层封装命令模板。
2. 它支持数组和 builder 两种安全组参方式，默认逐项转义，避免手写字符串参数。
3. 最终执行层直接走 `ProcessStartInfo.FileName + Arguments`，不会额外套一层 shell。
4. 在 Windows 上，如果目标文件写成短名，执行层会额外探测 `.exe`、`.cmd`、`.bat`，这对 `npm` / `npm.cmd` 这类入口尤其有用。

如果你的目标是实现“可复用的 npm 调用封装，同时支持参数透传”，最稳妥的方案是：

- 用 `Cli.Wrap("npm")` 或显式 `Cli.Wrap("npm.cmd")` 创建基础命令
- 用 `WithArguments(args => ...)` 分段添加固定参数与透传参数
- 不要优先使用 `WithArguments(string)` 拼整串命令

## 关键证据

### 1. `Command` 本身只保存目标文件和最终参数字符串

`Command` 是不可变对象，`WithArguments(...)` 不会原地修改，而是返回一个新的 `Command`。无论你传数组还是 builder，最后都会变成一个 `string Arguments` 保存下来。

证据：

- `repos/CliWrap/CliWrap/Command.cs:13-24`
- `repos/CliWrap/CliWrap/Command.cs:43-47`
- `repos/CliWrap/CliWrap/Command.cs:119-140`
- `repos/CliWrap/Readme.md:155-156`

这意味着封装 npm wrapper 时，可以先构造一个基础命令，再按场景叠加参数，而不用担心命令对象被意外共享修改。

### 2. 安全组参的核心在 `ArgumentsBuilder`

数组重载和 builder 重载最终都依赖 `ArgumentsBuilder`。`Add(...)` 的行为很直接：

- 参数之间用空格拼接
- 默认对每个参数做转义
- `IFormattable` 使用 invariant culture 输出

证据：

- `repos/CliWrap/CliWrap/Builders/ArgumentsBuilder.cs:21-29`
- `repos/CliWrap/CliWrap/Builders/ArgumentsBuilder.cs:40-45`
- `repos/CliWrap/CliWrap/Builders/ArgumentsBuilder.cs:57-61`
- `repos/CliWrap/CliWrap/Builders/ArgumentsBuilder.cs:81-82`
- `repos/CliWrap/CliWrap/Builders/ArgumentsBuilder.cs:137-140`

转义逻辑明确处理了这些情况：

- 参数里有空格
- 参数里有双引号
- 参数尾部或中间有反斜杠

证据：

- `repos/CliWrap/CliWrap/Builders/ArgumentsBuilder.cs:154-208`

这正是“支持参数调用”的核心。只要把每个用户参数当作一个独立项加入 builder，`CliWrap` 就会负责把它变成最终可执行的命令行字符串。

### 3. 官方 README 明确建议优先用数组或 builder，不建议直接传整串字符串

README 中给出的优先级非常明确：

- 推荐 `WithArguments(["commit", "-m", "my commit"])`
- 推荐 `WithArguments(args => args.Add(...))`
- 不推荐 `WithArguments("commit -m \"my commit\"")`

证据：

- `repos/CliWrap/Readme.md:158-224`
- `repos/CliWrap/CliWrap/Command.cs:95-100`

README 还特别说明：

- 直接字符串方式要求调用方自己完成转义
- 格式错误会导致 bug 和安全问题
- 真有特殊场景时，再考虑用 `ArgumentsBuilder.Escape(...)` 手动转义

这对 npm wrapper 很重要，因为“参数透传”通常正是最容易因为空格、引号、路径而出错的地方。

### 4. 执行层不借助 shell，而是直接设置 `ProcessStartInfo`

执行时，`CliWrap` 把最终参数字符串直接放进 `ProcessStartInfo.Arguments`，同时 `UseShellExecute = false`。

证据：

- `repos/CliWrap/CliWrap/Command.Execution.cs:74-91`

这说明参数边界的控制权主要掌握在 `ArgumentsBuilder` 上，而不是依赖外部 shell 二次解析。对 wrapper 而言，这种模型更可控。

### 5. Windows 上会为短文件名探测 `.cmd` / `.bat`

如果当前平台是 Windows，且目标文件既不是全路径也没有扩展名，`CliWrap` 会尝试在探测目录下补 `.exe`、`.cmd`、`.bat`。

证据：

- `repos/CliWrap/CliWrap/Command.Execution.cs:17-21`
- `repos/CliWrap/CliWrap/Command.Execution.cs:30-38`
- `repos/CliWrap/CliWrap/Command.Execution.cs:65-71`
- `repos/CliWrap/CliWrap.Tests/PathResolutionSpecs.cs:28-50`

这意味着：

- 在 Windows 上，`Cli.Wrap("npm")` 理论上可以命中 `npm.cmd`
- 在非 Windows 上，则直接按 `npm` 可执行文件解析

这一点非常适合“跨平台 npm wrapper”。

### 6. 测试直接验证了数组和 builder 的参数格式化结果

配置测试里已经验证：

- 数组参数 `["-a", "foo bar"]` 最终会变成 `-a "foo bar"`
- builder 可以正确处理空格、嵌套引号、数值和批量追加

证据：

- `repos/CliWrap/CliWrap.Tests/ConfigurationSpecs.cs:60-96`

这是本次调研里最直接的“本地证据”，因为它精确描述了参数透传时最容易出问题的边界。

## 与“npm wrap + 参数透传”相关的实现建议

下面的建议是基于上面的仓库事实做出的落地推导，不是 `CliWrap` 仓库直接提供的 npm API。

### 场景 A：包装普通 npm 子命令

如果你只是要包装 `npm install`、`npm exec` 之类的子命令，可以把固定部分和用户传入部分分开追加：

```csharp
using CliWrap;
using CliWrap.Buffered;

public static Task<BufferedCommandResult> RunNpmAsync(
    string subCommand,
    IReadOnlyList<string> forwardedArgs,
    CancellationToken cancellationToken = default)
{
    var command = Cli.Wrap("npm")
        .WithArguments(args =>
        {
            args.Add(subCommand);
            args.Add(forwardedArgs);
        });

    return command.ExecuteBufferedAsync(cancellationToken);
}
```

这个写法的重点不是“少写字符串”，而是把每个参数作为独立 token 交给 `ArgumentsBuilder`。

### 场景 B：包装 `npm run <script>` 并透传额外参数

如果你的业务要模拟常见的“脚本名 + 额外参数”的包装方式，可以把固定前缀、脚本名和透传参数拆开构造：

```csharp
using CliWrap;
using CliWrap.Buffered;

public static Task<BufferedCommandResult> RunNpmScriptAsync(
    string scriptName,
    IReadOnlyList<string> forwardedArgs,
    CancellationToken cancellationToken = default)
{
    var command = Cli.Wrap("npm")
        .WithArguments(args =>
        {
            args.Add("run");
            args.Add(scriptName);

            if (forwardedArgs.Count > 0)
            {
                args.Add("--");
                args.Add(forwardedArgs);
            }
        });

    return command.ExecuteBufferedAsync(cancellationToken);
}
```

这里保留 `--` 只是一个实现假设，用于表达“固定命令”和“后续透传参数”之间的边界。`CliWrap` 只负责安全组参，不负责解释 `npm` 如何消费这些参数。

### 场景 C：需要复用参数模式

README 提到 builder 重载适合抽可复用扩展方法，因此可以把 npm 的组参规则封装成一个内部 helper，而不是在业务代码里到处手写 `"run xxx -- yyy"`。

参考证据：

- `repos/CliWrap/Readme.md:175-206`

一个最小可复用封装可以是：

```csharp
using CliWrap.Builders;

public static class NpmArgumentsBuilderExtensions
{
    public static ArgumentsBuilder AddNpmRunScript(
        this ArgumentsBuilder args,
        string scriptName,
        IReadOnlyList<string> forwardedArgs)
    {
        args.Add("run");
        args.Add(scriptName);

        if (forwardedArgs.Count > 0)
        {
            args.Add("--");
            args.Add(forwardedArgs);
        }

        return args;
    }
}
```

然后业务层只保留：

```csharp
var command = Cli.Wrap("npm")
    .WithArguments(args => args.AddNpmRunScript(scriptName, forwardedArgs));
```

## 模块边界与执行流

从仓库结构看，这条链路非常清晰：

1. `Cli.Wrap("npm")` 创建基础 `Command`
2. `WithArguments(...)` 把参数构造成最终字符串
3. `CreateStartInfo()` 把目标文件和参数塞进 `ProcessStartInfo`
4. `ExecuteAsync()` 或 `ExecuteBufferedAsync()` 启动进程并处理输出

关键模块：

- 入口：`repos/CliWrap/CliWrap/Cli.cs`
- 配置对象：`repos/CliWrap/CliWrap/Command.cs`
- 参数构造：`repos/CliWrap/CliWrap/Builders/ArgumentsBuilder.cs`
- 进程执行：`repos/CliWrap/CliWrap/Command.Execution.cs`
- 缓冲输出：`repos/CliWrap/CliWrap/Buffered/BufferedCommandExtensions.cs`

依赖关系：

- `Command` 依赖 `ArgumentsBuilder` 生成 `Arguments`
- `Command.Execution` 依赖 `System.Diagnostics.ProcessStartInfo`
- `BufferedCommandExtensions` 在 `Command` 之上附加 stdout/stderr 缓冲

## 风险与边界

### 1. 最大风险不是启动 npm，而是把参数提前拼成字符串

如果你把用户输入先拼成一个完整字符串，再传给 `WithArguments(string)`，就绕过了 `CliWrap` 最重要的安全能力。空格、引号、反斜杠都可能在这里出问题。

### 2. `CliWrap` 只能保证“参数构造正确”，不能保证“npm 业务语义正确”

`CliWrap` 只负责把参数安全地交给子进程。`npm` 如何解释这些参数，不在该仓库控制范围内。

### 3. Windows 与非 Windows 的目标文件解析存在差异

当前实现只在 Windows 上主动补 `.cmd` / `.bat`。如果你的部署环境非常固定，也可以直接传 `npm.cmd` 或绝对路径，减少解析歧义。

### 4. 构建验证在当前环境里需要绕过强签名

直接运行测试时，`CliWrap` 在 `net10.0` 目标下因强签名触发了 OpenSSL digest 错误。使用下面的命令后，相关测试可通过：

```bash
dotnet test repos/CliWrap/CliWrap.Tests/CliWrap.Tests.csproj \
  --filter "FullyQualifiedName~ConfigurationSpecs|FullyQualifiedName~PathResolutionSpecs" \
  -p:SignAssembly=false \
  -p:PublicSign=false
```

本地结果：

- 17 个测试通过
- 1 个测试跳过
- 跳过项是 Windows 专属脚本短名解析测试

这说明当前环境可以验证参数格式化与短名可执行文件解析，但不能直接把“Windows 下 `npm.cmd` 一定可运行”当作本机实测结论。

## 建议的后续动作

1. 在你的业务代码里先实现一个最小 `NpmRunner`，只暴露结构化参数接口，不暴露整串命令字符串接口。
2. 针对你的真实场景补 3 类测试：带空格参数、带引号参数、路径参数。
3. 如果你的目标明确是 `npm run <script>`，把“固定参数”和“透传参数”封装成 builder 扩展方法，避免散落在多处调用点。
4. 如果需要跨平台严谨性，在 CI 里分别验证 Linux 和 Windows，对 `Cli.Wrap("npm")` 与 `Cli.Wrap("npm.cmd")` 做对照。
