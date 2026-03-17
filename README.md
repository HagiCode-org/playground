# 实验场

本仓库用于本地实验、快速原型验证和代码测试。

## 用途

- 快速原型验证
- 实验性功能测试
- 不影响主项目的代码实验

## 从主仓库快捷启动

如果你已经位于 monorepo 根目录，可以先用下面的命令查看当前支持的 playground 快捷方式：

```bash
npm run playground
```

常见示例：

```bash
# 运行 Codex 工具调用校验脚本
npm run example:tool-call-display

# 运行 CodeBuddy fixture 示例
npm run example:codebuddy-dotnet -- --scenario startup-smoke

# 运行 Hermes ACP .NET 实验
npm run example:hermes-acp-dotnet
```

当前根级快捷方式覆盖的首批示例包括：

- `example:codebuddy-dotnet`
- `example:codex-dotnet`
- `example:copilot-dotnet`
- `example:hermes-acp-dotnet`
- `example:iflow-dotnet`
- `example:opencode-dotnet`
- `example:tool-call-display`
- `example:docker-https`

这些根级命令只负责把你路由到正确的 `repos/playground` 子目录并执行对应命令，不会自动安装依赖、配置认证，或代替各示例自己的前置条件说明。

如果后续新增 playground 示例，需要同步在主仓库的 `scripts/playground.mjs` 中显式注册，`npm run playground` 才会展示对应入口。
