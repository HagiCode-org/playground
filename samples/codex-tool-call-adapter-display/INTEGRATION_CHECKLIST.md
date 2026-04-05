# Codex Tool Calling Adapter Integration Checklist

## 1) Prepare fixtures
- Verify fixture files exist in `fixtures/`:
  - `success.json`
  - `failure.json`
  - `timeout.json`
  - `empty-result.json`
  - `missing-id.json`

## 2) Validate adapter normalization
- Run:
  - `node samples/codex-tool-call-adapter-display/validate-tool-events.mjs`
- Expected:
  - All scenarios print `✓`
  - Terminal status only appears once per tool call
  - Missing id scenario generates `codex-turn-` prefix id

## 3) Backend chain smoke check (hagicode-core)
- Send Codex stream with tool events and observe output contract:
  - `LifecycleStatus` uses only `running/completed/failed`
  - `argumentSummary/resultSummary/errorSummary` populated as applicable
  - payload truncation metadata appears when payload is oversized

## 4) Frontend timeline check (web)
- Open Session Detail and compare against `snapshots/frontend-timeline-sample.md`.
- Confirm:
  - running -> completed/failed transitions stay on same `toolCallId`
  - status badge + summary text updates incrementally
  - ordering remains stable for mixed legacy and unified messages

## 5) Regression pass
- Re-run existing Claude session rendering tests / manual checks:
  - legacy ToolUse + ToolResult still render
  - no runtime error when lifecycle metadata is absent
