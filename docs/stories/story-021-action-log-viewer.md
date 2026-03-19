---
id: story-021
title: Host can view the action log during a game (debug tool)
status: done
created: 2026-03-14
completed: 2026-03-19
---

## What

The host can open a collapsible panel showing the raw action log for the current room — useful for debugging game modules during development.

## Why

Game authors need visibility into what actions have been submitted to diagnose unexpected state or rule bugs without reading database rows.

## Acceptance criteria

- [x] A "Debug" toggle (host-only, hidden from other players) opens an action log panel
- [x] The panel shows a scrollable list of all actions in the room's `action_log` table: timestamp, player name, and the raw action JSON
- [x] The panel is read-only — no actions can be replayed or deleted from here
- [x] `GET /api/rooms/{roomId}/action-log` returns the log (host-only, 403 for non-hosts)
- [x] The panel is part of platform chrome (not per-game) and works for any game module

## Notes

- Backend: `GET /api/rooms/{roomId}/action-log` in `src/Meepliton.Api/Endpoints/RoomEndpoints.cs` — host-only (403), joins with users table for display names
- `action_log` table, `ActionLog` model, and `GameDispatcher` logging were pre-existing from initial migration
- Frontend: `debugOpen` + `actionLog` + `actionLogError` state in `RoomPage.tsx`; host-only toggle with `aria-expanded`/`aria-controls`; fetch fires on each open (close/reopen to refresh)
- CSS: `.action-log-debug*` in `apps/frontend/src/platform/room/room.css` using design tokens
- Architect review: PASS — no must-fix items; error handling and should-fix items addressed
- Future: add polling/auto-refresh while panel is open; add `stateVersion` to response
