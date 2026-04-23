# GitCompatibilityTest

`GitCompatibilityTest` 是 hagiplayground 里用于验证 `LibGit2Sharp`、`SQLite + EF Core`、`SQLite + linq2db` 跨平台兼容性和读取路径性能的统一 harness。

## 前置条件

- `.NET SDK 8.0.x`
- 能联网恢复 NuGet 包
- 兼容性模式需要一个有效 Git 仓库路径

## CLI 入口

```bash
dotnet run -- --mode compatibility --repository <git-repo> --output <artifacts-dir>
dotnet run -- --mode benchmark --output <artifacts-dir> --warmup 2 --iterations 8
dotnet run -- --mode benchmark --output <artifacts-dir> --warmup 2 --iterations 8 --refresh-readme --readme ../../README.md
dotnet run -- --mode summarize --results-root <workflow-download-root> --output <summary-dir> --readme ../../README.md
```

## 模式说明

- `compatibility`：验证库加载、仓库打开、状态扫描、分支读取、HEAD commit 读取，以及 `SQLite + EF Core`、`SQLite + linq2db` 的基本读操作。
- `benchmark`：在受控 fixture 仓库和受控 SQLite fixture 上执行 `repository-open`、`status-scan`、`branch-lookup`、`head-commit-lookup`、`sqlite-ef-query`、`sqlite-linq2db-query` 六个场景，并输出结构化结果。
- `summarize`：聚合多个 `benchmark-results.json`，生成统一 Markdown 摘要，并可刷新 hagiplayground 根 `README.md` 的固定摘要区。

## 关键参数

- `--repository`：兼容性模式要检查的真实仓库路径；workflow 默认传入仓库根目录。
- `--output`：结果输出目录；如果不传，会自动创建 `artifacts/<mode>-<timestamp>/`。
- `--warmup`：每个 benchmark 场景的预热次数，默认 `2`。
- `--iterations`：每个 benchmark 场景的计时次数，默认 `8`。
- `--refresh-readme`：benchmark 完成后，用当前单机结果刷新根 `README.md` 的固定摘要区。
- `--readme`：要刷新或复制的 README 路径；与 `--refresh-readme` 或 `--mode summarize` 搭配。
- `--results-root`：聚合模式下查找 `benchmark-results.json` 的根目录。

## 输出文件

### compatibility

- `compatibility-report.json`：平台信息、硬件信息、检查结果和异常细节。
- `compatibility-report.md`：适合直接贴进 GitHub Step Summary 的 Markdown 版本。
- `compatibility.log`：建议由 workflow shell 重定向保存。

### benchmark

- `benchmark-results.json`：包含平台元数据、硬件信息、Git fixture、SQLite fixture、StatusOptions、所有样本和失败诊断。
- `benchmark-results.csv`：按样本展开的 CSV，额外带上 CPU、逻辑核数和总内存，便于做跨平台比较或二次分析。
- `benchmark-summary.md`：单 runner 摘要，包含阶段状态、统计值和产物文件名。
- `benchmark.log`：建议由 workflow shell 重定向保存。

### summarize

- `latest-manual-run-summary.md`：聚合多个 runner 结果后的统一 Markdown 摘要。
- `README.md`：如果传了 `--readme`，输出目录会保留一份更新后的 README 副本。

## 场景口径

- `repository-open`：只衡量 `new Repository(path)` 的打开路径。
- `status-scan`：使用与 `hagicode-core` 读取路径一致的 `StatusOptions` 运行 `RetrieveStatus`。
- `branch-lookup`：读取 `Repository.Head.FriendlyName`。
- `head-commit-lookup`：读取 `Repository.Head.Tip` 的提交元数据。
- `sqlite-ef-query`：打开 SQLite EF Core `DbContext` 并读取最新匹配行。
- `sqlite-linq2db-query`：打开 SQLite linq2db `DataConnection` 并读取最新匹配行。

## 本地执行建议

1. 先跑兼容性模式，确认当前机器能正常加载 `LibGit2Sharp`。
2. 再跑 benchmark 模式，检查 Git 和 SQLite 六个场景是否都有样本。
3. 如果要刷新根 README，用 `--refresh-readme --readme ../../README.md`。
4. 如果要复盘多 runner 下载产物，用 `--mode summarize --results-root <download-root>` 聚合。

## 边界

- 首轮结果只用于跨平台比较，不设性能阈值门禁。
- benchmark fixture 是程序现场创建的受控仓库，不依赖 hagiplayground 当前工作树状态。
- 每次结果都携带 CPU、逻辑核数和总内存，避免把硬件差异误判为 Git 或 SQLite 差异。
- workflow 的手动执行会更新工作区里的 `README.md` 并作为 artifact 上传，不会自动推回仓库分支。
