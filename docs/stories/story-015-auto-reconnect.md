---
id: story-015
title: Player automatically returns to their seat after disconnecting
status: refined
created: 2026-03-14
---

## What

A player who loses their connection (tab closed, network drop, page refresh) returns to their seat and receives the current game state when they reconnect.

## Why

Accidental disconnections are common on mobile — without reconnect, any disconnect ends the game for that player.

## Acceptance criteria

- [ ] When a player calls `JoinRoom` via SignalR and they already have a seat in the room, they are placed back in their existing seat (not a new one)
- [ ] On reconnect, the full current game state is pushed to the reconnecting client via `StateUpdated`
- [ ] Other players in the room receive a `PlayerConnected` event when the player reconnects
- [ ] Other players receive a `PlayerDisconnected` event when a player's connection drops
- [ ] Disconnection does not remove the player from the room — their seat is held until the room expires
- [ ] The UI shows a reconnecting state (spinner or message) while the SignalR connection is re-establishing
- [ ] On successful reconnect the game resumes exactly where it left off — no state loss

## Notes

- Spec: `docs/requirements.md` §4 (joining user stories), §5 (FR-SYNC-05 to FR-SYNC-06), §11 (SignalR events)
- Backend agent: `JoinRoom` hub method must detect existing seat and push full state
- Frontend agent: SignalR reconnect handling, reconnecting UI state
- Phase 2 roadmap explicitly calls this out as a polish item, but it is a Phase 1 requirement per FR
