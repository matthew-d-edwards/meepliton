---
id: story-020
title: Connected and disconnected players are visually distinguished
status: done
created: 2026-03-14
completed: 2026-03-19
---

## What

In both the waiting screen and during a game, each player's avatar or name shows whether they are currently connected or disconnected.

## Why

Players need to know if a teammate has dropped — it explains why nothing is happening and whether to wait or act.

## Acceptance criteria

- [x] Each player entry shows a status dot: green = connected, grey = disconnected
- [x] The dot updates in real time when `PlayerConnected` / `PlayerDisconnected` SignalR events arrive
- [x] Disconnected players are not removed from the list — their seat is shown as empty/inactive
- [x] The `<PlayerPresence>` platform component in `packages/ui/src/` renders the dot
- [x] Works in both the waiting screen and the in-game player list

## Notes

- Spec: `docs/requirements.md` §4 (joining user stories — "see which players are currently connected"), Phase 2 roadmap
- Backend: `PlayerConnected` / `PlayerDisconnected` events already sent from `GameHub.cs`
- `PlayerInfo.connected: boolean` in `packages/contracts/src/GameModule.ts`
- `RoomPage.tsx` listens to hub events and updates player connected state
- `RoomWaitingScreen` renders `PlayerPresence` which applies `.connected` / `.disconnected` CSS classes
- CSS for `.player-presence`, `.player-presence__dot`, `.room-waiting` added to `packages/ui/src/styles/tokens.css`
- In-game player lists are game-specific; `GameContext.players` provides `connected` field for games to consume
