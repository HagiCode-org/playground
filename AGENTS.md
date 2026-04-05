# CodeRef Vault Guide

This vault follows the standard CodeRef root layout.

- `docs/` stores research notes, explanations, and walkthroughs.
- `repos/` stores Git submodules that point to source repositories.
- `samples/` stores local sample, demo, and playground code.
- `index.yaml` is the canonical metadata entry for this vault root.

Repositories under `repos/` should be maintained through Git submodules rather than copied directly into the vault root.
Sample code should live under `samples/` instead of the vault root.

The canonical `index.yaml` fields are:

- `schemaVersion`
- `type`
- `title`
- `docsRoot`
- `reposRoot`

Keep this structure stable so assistants and tools can understand the vault quickly.
