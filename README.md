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

## Git 跨系统基准验证

`samples/GitCompatibilityTest` 现在同时承担 Git 兼容性 smoke、跨操作系统 benchmark，以及 SQLite EF Core / linq2db 基线验证三个角色。

### Workflow

- `.github/workflows/git-cross-os-benchmark.yml`
  - `push` / `pull_request`：当 sample、workflow 或本 README 变化时自动触发。
  - `workflow_dispatch`：维护者可手动触发完整的 `ubuntu-latest`、`windows-latest`、`macos-14` 矩阵。
- `.github/workflows/test-arm-macos.yml`
  - 保留为窄口径 smoke workflow，只验证 `--mode compatibility` 在 `macos-14` 上可运行。

### 本地命令

```bash
cd samples/GitCompatibilityTest

dotnet run -- --mode compatibility --repository ../.. --output ./artifacts/local-compat
dotnet run -- --mode benchmark --output ./artifacts/local-benchmark --warmup 2 --iterations 8
dotnet run -- --mode benchmark --output ./artifacts/local-benchmark --warmup 2 --iterations 8 --refresh-readme --readme ../../README.md
```

### 场景名

- `repository-open`
- `status-scan`
- `branch-lookup`
- `head-commit-lookup`
- `sqlite-ef-query`
- `sqlite-linq2db-query`

### 产物文件名

- `compatibility-report.json`
- `compatibility-report.md`
- `benchmark-results.json`
- `benchmark-results.csv`
- `benchmark-summary.md`
- `latest-manual-run-summary.md`
- `compatibility.log`
- `benchmark.log`

### 结果解读

- 先看 `compatibility-report.md` 是否全部通过；兼容性失败时当前 runner 的 benchmark 会被跳过。
- 比较跨平台结果时，优先看 `benchmark-summary.md` 或 `latest-manual-run-summary.md` 中位数，再结合 CPU、逻辑核数、总内存判断是否是硬件差异。
- 现在可以直接对比 Git 场景和 `sqlite-ef-query`、`sqlite-linq2db-query` 两条 SQLite 基线，用来判断异常更像 Git 栈问题还是 SQLite 栈问题。
- 首轮交付只建立采集口径，不对 `repository-open`、`status-scan`、`branch-lookup`、`head-commit-lookup`、`sqlite-ef-query`、`sqlite-linq2db-query` 设硬阈值。

### 边界说明

- benchmark 使用程序现场创建的 fixture 仓库，不依赖 hagiplayground 当前工作树的脏状态。
- 手动 workflow 执行完成后会刷新工作区中的 `README.md` 固定摘要区，并把更新后的 README 作为 artifact 上传；不会自动提交回当前分支。
- `test-arm-macos.yml` 和 `git-cross-os-benchmark.yml` 共用同一个 CLI 入口，只是责任范围不同。

## 最近一次手动执行摘要
<!-- git-cross-os-benchmark-summary:start -->
_Generated at 2026-04-23 05:53:08 UTC from `/tmp/hagiplayground-git-benchmark-local`._

| Runner | Hardware | Status | repository-open median (ms) | status-scan median (ms) | branch-lookup median (ms) | head-commit-lookup median (ms) | sqlite-ef-query median (ms) | sqlite-linq2db-query median (ms) |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Fedora Linux 43 (KDE Plasma Desktop Edition) / X64 / .NET 10.0.5 | 13th Gen Intel(R) Core(TM) i5-13500H; 16 logical cores; 31.1 GiB RAM; machine=fedora | PASS | 0.0394 | 0.1076 | 0.0080 | 0.0133 | 0.2883 | 0.1146 |

Artifacts remain per runner in the workflow downloads. Compare medians alongside hardware metadata, then inspect JSON/CSV for raw samples and failure details.
<!-- git-cross-os-benchmark-summary:end -->



