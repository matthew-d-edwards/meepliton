---
id: story-014
title: Host can start the game and remove players before it begins
status: done
created: 2026-03-14
---

## What

The host can kick a player from the waiting room and start the game once enough players have joined.

## Why

These are the two host-only actions before a game begins — without them the host has no way to manage the room.

## Acceptance criteria

- [x] `POST /api/rooms/{roomId}/start` starts the game: calls `IGameModule.CreateInitialState`, persists the state, broadcasts `GameStarted` via SignalR, returns 204
- [x] Starting with fewer than `IGameModule.MinPlayers` returns 400 with a clear message
- [x] Only the host can call start — non-hosts receive 403
- [x] `DELETE /api/rooms/{roomId}/players/{userId}` removes a player from the room
- [x] Removed players receive a `PlayerRemoved` SignalR event and are redirected to the lobby on the frontend
- [x] Only the host can remove players, and cannot remove themselves — 403 otherwise
- [x] Removing a player while the game is in progress is not allowed (this story is pre-game only) — 409

## Notes

- Spec: `docs/requirements.md` §4 (game room user stories), §5 (FR-ROOM-06 to FR-ROOM-08)
- Backend agent owns both endpoints
- Frontend agent owns the "Start game" button state and the "Remove" button wiring (in `<RoomWaitingScreen>`)
- Depends on story-013 (waiting screen UI) and story-009 (lobby API for room creation)
