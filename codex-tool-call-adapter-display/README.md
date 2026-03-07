# Codex Tool Calling Adapter Playground

This folder contains deterministic fixtures and a lightweight validator for the
OpenSpec change `codex-tool-calling-adapter-display`.

## Quick Start

```bash
node repos/playground/codex-tool-call-adapter-display/validate-tool-events.mjs
```

The script reads all `fixtures/*.json`, normalizes the event stream using the
same lifecycle semantics as backend adapter code, and prints assertion results.

## Fixtures

- `success.json`: running -> completed
- `failure.json`: running -> failed with command output error
- `timeout.json`: running -> failed via `turn.failed` backfill
- `empty-result.json`: completed with empty result payload
- `missing-id.json`: deterministic fallback `toolCallId` generation

## Expected Outputs

- status vocabulary is always `running/completed/failed`
- each scenario has deterministic status sequence
- terminal event contains summary fields for UI rendering

See `snapshots/frontend-timeline-sample.md` and `INTEGRATION_CHECKLIST.md` for
manual verification and end-to-end alignment.
