# SEO Machine Usage And Hagicode Integration Research

Date: 2026-04-11

Repository snapshot:
- `repos/seomachine`
- branch: `main`
- commit: `70231da177abe92ec26375c051561ff9acd8423a`
- commit subject: `fix: expand AI watermark removal with more unicode chars and AI-telltale phrases (#36)`

## Scope And Assumptions

This note is based only on local inspection inside this vault.

- I cloned `https://github.com/TheCraigHewitt/seomachine` into `repos/seomachine`.
- I inspected the repository structure, README files, Claude command definitions, Python modules, and WordPress integration files.
- I did not run live API calls because the repository requires GA4, GSC, DataForSEO, and WordPress credentials.
- I did not inspect a local Hagicode implementation in this vault, because no Hagicode repository is checked out under `repos/` here. Any Hagicode integration advice below is therefore an interface-level recommendation derived from `seomachine`'s local structure, not a verified Hagicode integration.

## Executive Summary

`seomachine` is best understood as a Claude Code workspace with a reusable Python SEO toolkit underneath it.

- The Claude-specific layer is large: `README.md` describes the project as a "Claude Code workspace", and the repo contains `24` command files under `.claude/commands/`, `11` agent files under `.claude/agents/`, and `26` skill entries under `.claude/skills/`.
- The reusable automation layer is real: Python modules under `data_sources/modules/`, root-level research scripts such as `research_serp_analysis.py`, and a WordPress publisher with an argparse CLI in `data_sources/modules/wordpress_publisher.py`.
- The project can be combined with Hagicode, but not by "just calling the slash commands". To become Claude-agnostic, the `.claude/*.md` workflow layer needs to be translated into stable Hagicode tasks or wrappers.
- The fastest path is not to port everything at once. First wrap the Python and publishing layer, then re-express the Claude command prompts as Hagicode workflows.

## What The Repository Actually Contains

### Claude-first workflow layer

Evidence from `repos/seomachine/README.md`, `repos/seomachine/CLAUDE.md`, and `.claude/`:

- The main README says the project is "built on Claude Code" and instructs users to launch it with `claude-code .`.
- The operational interface is a set of slash-command specs in `.claude/commands/`, including `research.md`, `write.md`, `rewrite.md`, `optimize.md`, `performance-review.md`, and `publish-draft.md`.
- The repo also defines specialized agent prompt files in `.claude/agents/`.
- Skill packaging is mostly directory-based, but one entry is a standalone file: `.claude/skills/growth-lead-SKILL.md`. If another orchestration system expects a uniform `dir/SKILL.md` shape, this is a normalization task.

### Reusable automation layer

Evidence from `repos/seomachine/data_sources/modules/` and root scripts:

- `data_sources/modules/dataforseo.py` contains a real API client for SERP, rankings, competitor analysis, keyword ideas, and questions.
- `data_sources/modules/data_aggregator.py` combines GA4, GSC, and DataForSEO into one report surface.
- `data_sources/modules/wordpress_publisher.py` is a concrete publisher that parses markdown, converts to HTML, creates draft posts, and writes Yoast metadata.
- `research_serp_analysis.py`, `research_quick_wins.py`, `research_trending.py`, and related scripts are entry points that can be called directly with Python.
- `data_sources/modules/content_scrubber.py` exposes reusable functions `scrub_content()` and `scrub_file()`.

Practical interpretation:

- If you want to stay inside Claude Code, the repo is already usable as designed.
- If you want automation or Claude independence, the Python layer is the part worth integrating first.

## How To Install And Use It Efficiently

### Recommended local setup

From the inspected files, the minimum practical setup is:

1. Clone the repo into `repos/seomachine`.
2. Create a Python virtual environment manually.
3. Install `pip install -r data_sources/requirements.txt`.
4. Fill the context templates under `context/`.
5. Prepare `.env` carefully before using analytics or publishing.

Why I recommend a virtual environment:

- The repo ships only one dependency file: `data_sources/requirements.txt`.
- I did not find `pyproject.toml`, `setup.py`, `poetry.lock`, `Pipfile`, or another lockfile-based Python project definition.
- That means environment isolation is the safest default.

### Best way to learn the content setup

The easiest way to understand the required context is not to start from empty templates.

- Read `examples/castos/README.md`.
- Compare `examples/castos/*.md` with the template files under `context/`.
- Copy the structure, then replace Castos-specific details with your own brand voice, product features, internal links, and writing samples.

This is the lowest-friction way to get useful outputs quickly, because the README repeatedly emphasizes that output quality depends on context quality.

### Manual usage path inside Claude Code

The repo's intended path is:

1. `claude-code .`
2. `/research [topic]`
3. `/write [topic]`
4. `/optimize [file]`
5. `/publish-draft [file]`

This flow is well-documented in `README.md`, `QUICK-START.md`, and `CLAUDE.md`.

### Scripted usage path without Claude

The repo already exposes enough lower-level pieces to automate parts of the workflow:

- Research script: `python3 research_serp_analysis.py "keyword phrase"`
- Content scoring: `python3 data_sources/modules/content_scorer.py <draft_file_path>`
- WordPress publishing: `python3 data_sources/modules/wordpress_publisher.py <draft_file> --type post`
- Content scrubbing: import `scrub_file()` from `data_sources/modules/content_scrubber.py`

This means a non-Claude flow is feasible, but today it is partial, not end-to-end.

## Installation And Usage Traps Found During Inspection

These are the most important issues to account for before automating anything.

### 1. `.env` loading is inconsistent

This is the largest setup risk.

- `CLAUDE.md` says credentials live in `data_sources/config/.env`.
- `data_sources/modules/data_aggregator.py`, `google_analytics.py`, `google_search_console.py`, and `dataforseo.py` explicitly load `data_sources/config/.env`.
- But several root scripts call plain `load_dotenv()` with no path, including `research_serp_analysis.py`, `research_quick_wins.py`, `research_trending.py`, and `research_priorities_comprehensive.py`.
- `data_sources/modules/wordpress_publisher.py` loads a repo-root `.env` in its CLI entry point.

Practical tip:

- For reliable local use, keep the same values in both `data_sources/config/.env` and repo-root `.env`, or create one as a symlink to the other.
- If you automate this with Hagicode, standardize the environment contract first.

### 2. WordPress environment variable names are inconsistent

There is documentation drift here.

- `README.md` shows `WP_URL`, `WP_USERNAME`, and `WP_APP_PASSWORD`.
- `data_sources/modules/wordpress_publisher.py` actually requires `WORDPRESS_URL`, `WORDPRESS_USERNAME`, and `WORDPRESS_APP_PASSWORD`.

Practical tip:

- Use the `WORDPRESS_*` names, because that is what the actual publisher code reads.
- If you build a Hagicode wrapper, map any legacy `WP_*` names to the `WORDPRESS_*` names centrally.

### 3. WordPress installation docs disagree on whether you need one integration file or two

- `wordpress/README.md` says to choose exactly one option: the MU-plugin or the `functions.php` snippet.
- `README.md` says to install the MU-plugin and add the functions snippet.

Practical interpretation:

- Treat `wordpress/README.md` as the more precise file for WordPress setup.
- For automation and maintainability, the MU-plugin-only path is the cleaner default.

### 4. `research_priorities_comprehensive.py` is not ready for non-interactive automation

This script has two separate problems:

- It uses `input()` to pause for confirmation and to ask whether competitor-gap analysis should be skipped.
- In the current implementation, it imports `quick_wins_main` but does not call it, and several later sections print placeholder success messages instead of executing real module logic.

Practical interpretation:

- Do not treat this script as a trustworthy non-interactive batch entry point yet.
- If Hagicode integration is the goal, this file should be patched or replaced early.

### 5. The content scrubber is reusable, but not yet a real CLI

- `content_scrubber.py` provides reusable functions.
- Its `__main__` block is only a self-test using hard-coded sample text, not a general file CLI.

Practical tip:

- In automation, import `scrub_file()` directly or add a thin wrapper command.
- Do not assume `python data_sources/modules/content_scrubber.py some-file.md` will work, because the file does not currently parse arguments.

## Can It Be Combined With Hagicode?

Yes, but only if you treat `seomachine` as two layers and integrate them differently.

### What should move into Hagicode first

These parts are good candidates for immediate Hagicode orchestration:

- Python research scripts that already accept arguments.
- Python analysis modules that can be imported directly.
- WordPress publishing through `WordPressPublisher`.
- Content scrubbing through `scrub_file()`.
- Context-file generation, synchronization, and validation.

Why this is the right first step:

- These pieces are concrete code, not prompt-only workflow descriptions.
- They can be wrapped as deterministic steps with stable inputs and outputs.
- They reduce manual work immediately without first solving the entire Claude prompt stack.

### What is still Claude-specific

These parts are not portable as-is:

- `.claude/commands/*.md`
- `.claude/agents/*.md`
- automatic agent chaining described in `write.md`
- the repo's main operator experience based on slash commands

Why this matters:

- These files describe behavior, but they are not a runtime by themselves.
- Hagicode would need its own orchestration layer to decide when to research, write, optimize, scrub, and publish.

### A realistic Claude-agnostic migration path

Recommended order:

1. Normalize configuration.
   Standardize one `.env` location and one WordPress variable naming scheme.

2. Add thin CLI wrappers around the reusable modules.
   Good initial commands would be `research-serp`, `score-content`, `scrub-content`, and `publish-wordpress`.

3. Convert the `.claude/commands/*.md` files into Hagicode task templates.
   Treat the current Markdown commands as prompt specifications, not as executable assets.

4. Define machine-readable outputs.
   Research and optimization steps should emit JSON or stable markdown sections so later steps can chain without human editing.

5. Patch the current interactive and placeholder scripts.
   `research_priorities_comprehensive.py` is the clearest early target.

My judgement:

- If the goal is "Claude-independent but still automated", the idea is viable.
- If the goal is "drop seomachine into Hagicode unchanged", the answer is no. The Claude prompt layer is too central.

## Recommended Immediate Follow-up Actions

If this vault is meant to support a real Hagicode integration spike, the next actions should be:

1. Add a small wrapper layer in this repo or in Hagicode that exposes:
   - SERP research
   - content scoring
   - content scrubbing
   - WordPress publishing

2. Patch `research_priorities_comprehensive.py` to remove `input()` and replace placeholder logging with real execution.

3. Unify environment handling so every script reads the same dotenv path.

4. Add a proper CLI to `content_scrubber.py`.

5. Decide whether Hagicode will:
   - call Python entry points directly, or
   - import Python modules and orchestrate them in-process.

The first option is faster for an integration spike. The second is cleaner if long-term observability and typed contracts matter.

## Limitations

- No live API credentials were available in this investigation, so I did not verify GA4, GSC, DataForSEO, or WordPress network behavior.
- No Hagicode repository was present under this vault's `repos/`, so Hagicode compatibility was not tested against a concrete local implementation.
- I did not rewrite any upstream repository files; this deliverable is an inspection note only.
