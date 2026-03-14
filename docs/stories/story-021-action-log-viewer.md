---
id: story-021
title: Host can view the action log during a game (debug tool)
status: backlog
created: 2026-03-14
---

## What

The host can open a collapsible panel showing the raw action log for the current room — useful for debugging game modules during development.

## Why

Game authors need visibility into what actions have been submitted to diagnose unexpected state or rule bugs without reading database rows.

## Acceptance criteria

- [ ] A "Debug" toggle (host-only, hidden from other players) opens an action log panel
- [ ] The panel shows a scrollable list of all actions in the room's `action_log` table: timestamp, player name, and the raw action JSON
- [ ] The panel is read-only — no actions can be replayed or deleted from here
- [ ] `GET /api/rooms/{roomId}/action-log` returns the log (host-only, 403 for non-hosts)
- [ ] The panel is part of platform chrome (not per-game) and works for any game module

## Notes

- Spec: `docs/requirements.md` Phase 2 roadmap ("Action log viewer in room (debug tool for game authors)")
- Status `backlog` — Phase 2; depends on stories 009, 016 (rooms + real-time)
- Backend agent: new endpoint; frontend agent: collapsible panel component
