---
id: story-009
title: Lobby API — list rooms, create room, join by code
status: in-progress
created: 2026-03-14
---

## What

The backend exposes endpoints for listing a user's active rooms, browsing available games, creating a new room, and joining a room by its 6-character code.

## Why

These are the core lobby operations — without them the frontend can't show anything useful after sign-in.

## Acceptance criteria

- [x] `GET /api/lobby` returns `{ rooms: [...], games: [...] }` — rooms the user is part of, and all registered game modules
- [x] Each room entry includes: `roomId`, `gameId`, `gameName`, `status` (waiting/playing/finished), `playerCount`, `joinCode`
- [x] Each game entry includes: `gameId`, `name`, `description`, `minPlayers`, `maxPlayers`
- [x] `POST /api/rooms` accepts `{ gameId, options? }` — creates a room, sets the caller as host, returns `{ roomId, joinCode }`
- [x] Join codes are 6 characters, uppercase A–Z excluding O and I, digits 2–9 (no 0, 1, O, I to avoid ambiguity)
- [x] `POST /api/rooms/join` accepts `{ code }` — adds the user to the room if it is in `waiting` status; returns `{ roomId }`
- [x] Joining a room that is already in `playing` or `finished` status returns 409 with a clear message
- [x] Joining a room you are already in returns 200 (idempotent)
- [x] All endpoints require authentication (`RequireAuthorization()`)

## Notes

- Spec: `docs/requirements.md` §5 (FR-LOBBY-01 to FR-LOBBY-05), §11 (lobby/room API endpoints)
- Backend agent owns all three endpoints
- Games list is derived from Scrutor-discovered `IGameModule` registrations — no database table needed for games themselves
- Depends on story-001/002 (auth) so the user identity is available
