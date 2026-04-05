# 实验场

本目录仅用于本地实验、快速原型验证与非权威样例。

## 边界

- CodeBuddy、Codex、Copilot、Hermes、Kimi、OpenCode 等 aiAgent provider、console、测试与配置样例，已统一迁入 `repos/Hagicode.Libs`
- 若你在寻找 aiAgent 相关实现，请直接查看 `repos/Hagicode.Libs`
- 本 vault 只保留与权威实现不重复的实验内容，演练代码统一放在 `samples/`

## 当前保留的示例

- `samples/IFlowDotnet`：.NET console demo
- `samples/codex-tool-call-adapter-display`：工具调用事件展示校验
- `samples/docker-local-https-example`：本地 Docker HTTPS 示例
- `samples/DoubaoDotnet`、`samples/DoubaoGo`、`samples/GitCompatibilityTest`：保留为目录内实验，请按各自目录说明运行

## 说明

- 如需查看当前已注册的 playground 快捷入口，可在 monorepo 根目录运行 `npm run playground`
- 调整 `samples/` 结构后，需要同步更新主仓库的 `scripts/playground.mjs`
