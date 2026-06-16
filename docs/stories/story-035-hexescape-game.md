---
id: story-035
title: Add Hex Escape co-op game module
status: in-review
created: 2026-06-15
updated: 2026-06-16
---

## What

Players can create a Hex Escape room, select a level, place and rotate pipe tiles collaboratively on a hex grid, and win by connecting all survivors to the exit before the zombie threat counter reaches the level threshold.

## Why

Every current game module is competitive. Hex Escape is the first co-op title, enabling groups to play together against a shared system threat and supporting solo sessions for a single player.

## Acceptance criteria

See `docs/specs/hexescape.md` for the full Given/When/Then criteria (AC-1 through AC-16).

## Implementation status

Backend, frontend, and tests are complete on the `add-hexescape-game` branch:

- `src/games/Meepliton.Games.HexEscape/` — `HexEscapeModule`, `HexEscapeLevels` (tutorial-01, medium-01, hard-01), models. No `DbContext` or migrations.
- `apps/frontend/src/games/hexescape/` — `Game.tsx`, `HexBoard.tsx`, `index.tsx`, `registry.ts` entry.
- `src/Meepliton.Tests/Games/HexEscapeModuleTests.cs` — 62 xUnit tests.
- Platform change: `POST /rooms` now populates `room.GameOptions` from `req.Options` (AD-9 resolved).
- Platform addition: `IGameModule.SetupOptions` generic mechanism (ADR-012) and `GameSetupOption.cs` in `Meepliton.Contracts`.

**CI gate:** Status will move to `done` only after CI passes on the PR. Do not mark done before that.

## Notes

- Spec: `docs/specs/hexescape.md`
- Platform prerequisite (AD-9): resolved — `POST /rooms` now transports `req.Options` into `room.GameOptions`.
- Branch: `add-hexescape-game`
- PR: (link once opened)
