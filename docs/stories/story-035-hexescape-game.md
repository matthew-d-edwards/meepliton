---
id: story-035
title: Add Hex Escape co-op game module (Outbreak — v2)
status: refined
created: 2026-06-15
updated: 2026-06-16
---

## What

Players can create a Hex Escape room, select a level, draw tiles from a shared deck, place and rotate pipe tiles collaboratively on a hex grid, move their characters toward the exit, and win by getting all surviving characters to the exit before zombies eliminate everyone.

## Why

Every current game module is competitive. Hex Escape is the first co-op title. v1 shipped a threat counter. v2 (Outbreak) replaces it with zombie tokens that move on the grid and eliminate characters, creating genuine spatial tension and making every tile placement matter.

## v2 supersedes v1

The v1 implementation on branch `add-hexescape-game` is superseded. v2 rewrites `HexEscapeModule.cs`, `Models/HexEscapeModels.cs`, `HexEscapeLevels.cs`, `types.ts`, `Game.tsx`, and `HexEscapeModuleTests.cs` in place on branch `claude/hex-pipe-zombie-coop-mtlx2m`. The hex geometry, tile model, connection rule, `HexBoard` rendering, `SetupOptions`/level-selector mechanism, and the platform fixes from v1 (AD-9, ADR-012, ADR-013) all carry forward unchanged.

## Acceptance criteria

See `docs/specs/hexescape.md` for the full Given/When/Then criteria (AC-v2-1 through AC-v2-29).

## Notes

- Spec: `docs/specs/hexescape.md`
- Branch: `claude/hex-pipe-zombie-coop-mtlx2m`
- v1 spec and implementation preserved in git history on branch `add-hexescape-game`
- PR: (link once opened)
