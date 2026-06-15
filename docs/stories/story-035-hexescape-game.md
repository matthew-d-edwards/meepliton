---
id: story-035
title: Add Hex Escape co-op game module
status: refined
created: 2026-06-15
---

## What

Players can create a Hex Escape room, select a level, place and rotate pipe tiles collaboratively on a hex grid, and win by connecting all survivors to the exit before the zombie threat counter reaches the level threshold.

## Why

Every current game module is competitive. Hex Escape is the first co-op title, enabling groups to play together against a shared system threat and supporting solo sessions for a single player.

## Acceptance criteria

See `docs/specs/hexescape.md` for the full Given/When/Then criteria (AC-1 through AC-9).

## Notes

- Spec: `docs/specs/hexescape.md`
- Platform prerequisite: `POST /rooms` must populate `room.GameOptions` from `req.Options` (AD-9 in spec) — backend agent must land this before level-selector is testable; architect sign-off required.
- Branch: `add-hexescape-game`
- PR: (link once opened)
