---
id: story-020
title: Connected and disconnected players are visually distinguished
status: backlog
created: 2026-03-14
---

## What

In both the waiting screen and during a game, each player's avatar or name shows whether they are currently connected or disconnected.

## Why

Players need to know if a teammate has dropped — it explains why nothing is happening and whether to wait or act.

## Acceptance criteria

- [ ] Each player entry shows a status dot: green = connected, grey = disconnected
- [ ] The dot updates in real time when `PlayerConnected` / `PlayerDisconnected` SignalR events arrive
- [ ] Disconnected players are not removed from the list — their seat is shown as empty/inactive
- [ ] The `<PlayerPresence>` platform component in `packages/ui/src/` renders the dot
- [ ] Works in both the waiting screen and the in-game player list

## Notes

- Spec: `docs/requirements.md` §4 (joining user stories — "see which players are currently connected"), Phase 2 roadmap
- Status `backlog` — Phase 2 feature; depends on story-013 (waiting screen) and story-015 (reconnect)
- Frontend + UX agents own `<PlayerPresence>`
