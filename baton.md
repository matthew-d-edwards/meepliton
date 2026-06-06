---
project: Meepliton
sync_mode: bidirectional
sync_conflict_strategy: llm-merge
sync_dir: docs/baton
import_require_frontmatter: false
---

# How this repo uses Baton

This repo uses Baton to track its own development. `baton.md` declares the bound
project and sync defaults so neither Baton nor an agent has to be told them each
session. It is human-authored config — edit it in a PR, not via the API.

## Conventions

- Log a decision after every committed change to `src/` or `tests/`.
- Update related docs in the same pass when behavior changes.
- Score 9–10 only when you have verified the change end-to-end.
