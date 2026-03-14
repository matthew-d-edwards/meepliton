---
id: story-018
title: Players can clearly see whose turn it is
status: refined
created: 2026-03-14
---

## What

At any point during a game, every player can see at a glance whose turn it is — including knowing when it is their own turn.

## Why

Without a turn indicator players don't know when to act, which breaks the basic game loop.

## Acceptance criteria

- [ ] The game room chrome (platform level, not game level) shows the current player's name and avatar in a persistent "current turn" indicator
- [ ] When it is the local player's turn, the indicator reads "Your turn" and is visually highlighted (e.g. `--neon-cyan` glow)
- [ ] When it is another player's turn, the indicator shows that player's name
- [ ] The indicator updates immediately when `StateUpdated` is received
- [ ] The indicator is part of `<PlayerPresence>` or a new platform chrome component — it must not be duplicated per game module
- [ ] On mobile (375px) the indicator is visible without scrolling

## Notes

- Spec: `docs/requirements.md` §4 (game room user stories — "clearly whose turn it is")
- `currentPlayerId` is a convention in game state (games are expected to expose it) — the platform reads it from the opaque state blob via a known key
- UX + frontend agents own the component; architect should confirm how `currentPlayerId` is surfaced from the JSONB blob (may need a thin typed wrapper or a game contract addition)
- Owner decision: should `currentPlayerId` be a first-class field in the `rooms` table (copied on each state update), or extracted from the JSONB on the client? Blocks finalising this story. (See `docs/owner/TODO.md`)
