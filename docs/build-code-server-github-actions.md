# Building distributable code-server packages with GitHub Actions

Scope
- Goal: produce distributable artifacts usable on Linux, macOS and Windows via GitHub Actions.
- Target repo inspected: repos/code-server (https://github.com/coder/code-server)

What was inspected (evidence)
- .github/workflows/release.yaml — workflow that builds releases across linux/macos and packages them (uses npm scripts, nfpm, and uploads draft releases).
  - Path: .github/workflows/release.yaml
- package.json — scripts: `build`, `build:vscode`, `release`, `package`.
  - Path: package.json
- ci/build/build-release.sh — creates `./release` by bundling outputs and preparing package.json.
  - Path: ci/build/build-release.sh
- ci/build/build-packages.sh — creates tar.gz and (on linux) uses `nfpm` to build deb/rpm into `release-packages`.
  - Path: ci/build/build-packages.sh
- .gitignore/.dockerignore/other files reference `release`, `release-packages`, and `release-standalone`.

Key findings
- The repository already contains a complete release workflow: .github/workflows/release.yaml performs matrix builds for linux and macOS, runs `npm ci`, `npm run build`, `npm run build:vscode`, then `npm run release` and `npm run package`, producing `release` and `release-packages`.
- Packaging uses nfpm (downloaded in the workflow) to create distro packages (deb/rpm) for Linux; tar.gz archives are generated for all platforms. The workflow uploads draft releases.
- The repo's packaging model is "release directory + platform packages" (tar.gz, deb, rpm, macos packages) rather than a single self-contained native executable file.
- package.json `bin` points to a JS entry (out/node/entry.js). The build/packaging scripts bundle Node/VSCodium artifacts into a release directory; they may include a Node binary for some targets when KEEP_MODULES=1.

Implications for "single executable" requirement
- If by "single executable" the intent is "single distributable artifact per OS" (e.g., tar.gz / .pkg / .zip / .exe installer), the repo already supports that via the existing workflow. Reuse or adapt .github/workflows/release.yaml to produce and upload artifacts on push/tag.
- If the intent is a single native binary (one file that contains Node runtime and app code): the current project does not produce that out-of-the-box. Achieving a single native executable would require adding a bundling step using tools such as `pkg`, `nexe`, or building a native launcher that embeds Node-plus-assets. This is non-trivial and has risks:
  - Native modules (argon2, other prebuilt modules) may be incompatible with bundlers.
  - Platform-specific dependencies (glibc, macOS frameworks, code signing/notarization) complicate cross-platform single-binary builds.
  - Tests and native module rebuild steps must be adapted; the repo currently builds native modules per-target in CI.

Recommended approach (practical, low-risk)
1. Reuse repository's release workflow
   - Use `.github/workflows/release.yaml` as the basis. Trigger on `workflow_dispatch` or on tag push to produce draft releases.
   - Matrix across OS/arch as in release.yaml; run the same steps: `npm ci`, `npm run build`, `npm run build:vscode`, `npm run release`, `npm run package`.
   - Upload `release-packages/*` and `package.tar.gz` as artifacts or attach them to GitHub Releases (the workflow already does this).

2. For Windows artifacts
   - The workflow currently produces tar.gz; add an artifact step to create zip or installer formats appropriate for Windows. Consider using `nsis` or `inno` if a native installer is required.

3. If a single-file native executable is required (advanced)
   - Prototype locally with `pkg` or `nexe` on each target platform. Validate compatibility with native modules (argon2) and any handling of external binaries.
   - If successful, add a separate job in Actions that runs `pkg`/`nexe` for linux, macos, windows and uploads artifacts. Mark this experimental and keep the existing packaging as primary.

Risks and notes
- Cross-compilation: building macOS artifacts on Linux is limited; release.yaml uses macOS runners for mac builds.
- Native modules: need to be built for each target arch/os; the repo already handles that by building on matching runners (`npm_config_build_from_source` set to true).
- Code signing / notarization: required for macOS and recommended for Windows binaries; not covered by default CI.
- Reproducibility: keep `npm ci` and shrinkwrap (`npm-shrinkwrap.json`) steps as in current scripts to get deterministic builds.

Recommended next actions (short)
- Re-run/trigger the existing `.github/workflows/release.yaml` on a test tag to observe produced artifacts.
- If single-file native executables are a hard requirement, create a small PoC repo that tries `pkg` with code-server's release output (or a minimal subset) and test native modules.

Files inspected (relative paths)
- .github/workflows/release.yaml
- package.json
- ci/build/build-release.sh
- ci/build/build-packages.sh
- ci/build/* (packaging helpers)

Limitations
- This research used the checked-out HEAD (shallow clone). If a deeper history or tags are needed to reproduce exact release behavior, a full clone or fetching tags may be required.
- No changes were made to the code-server repository; recommendations are based on existing scripts and workflows found in the codebase.

---

Quick Chinese summary
- 该仓库已包含 GitHub Actions release 流程（.github/workflows/release.yaml），能在不同平台上构建并产出 `release-packages`（tar/ deb/ rpm 等）。
- 想要“单文件原生可执行程序”需要额外引入打包工具（如 pkg/nexe），但存在原生模块和平台签名等复杂性，风险较高。建议优先复用现有 release.yaml 来产出每个平台的可分发包。

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
