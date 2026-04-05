# Frontend Timeline Verification Samples

Use the normalized outputs from `validate-tool-events.mjs` to compare Web timeline rendering.

## success
- Timeline order:
  1. `cmd-success-1` -> `running` badge
  2. `cmd-success-1` -> `completed` badge
- Summaries:
  - argument summary contains `"command":"ls -la"`
  - result summary contains `total 3`

## failure
- Timeline order:
  1. `cmd-fail-1` -> `running`
  2. `cmd-fail-1` -> `failed`
- Summaries:
  - error summary contains `No such file`

## timeout
- Timeline order:
  1. `cmd-timeout-1` -> `running`
  2. `cmd-timeout-1` -> `failed` (from `turn.failed` backfill)
- Summaries:
  - error summary contains `timeout`

## empty-result
- Timeline order:
  1. `cmd-empty-1` -> `running`
  2. `cmd-empty-1` -> `completed`
- Summaries:
  - result summary is empty string (UI should show fallback copy)

## missing-id
- Timeline order:
  1. `codex-turn-*` fallback id -> `running`
  2. same fallback id -> `completed`
- Summaries:
  - result summary contains `/workspace`
