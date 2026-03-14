---
id: story-028
title: Host can transfer host status to another player
status: backlog
created: 2026-03-14
---

## What

The host of a room can hand off host status to any other connected player — from the waiting screen or (if designed) during a game.

## Why

If the original host needs to leave mid-session, there is currently no way to pass control. The `POST /api/rooms/{roomId}/transfer-host` endpoint exists in the requirements but has no UI entry point.

## Acceptance criteria

- [ ] On the waiting screen, the host sees a "Make host" or "Transfer host" action next to each non-host player (alongside the existing "Remove" button)
- [ ] Triggering the action calls `POST /api/rooms/{roomId}/transfer-host` with the target player's ID
- [ ] On success, the old host loses host controls (Start game, Remove, Transfer) and the new host gains them in real time via SignalR
- [ ] If the transfer fails, an inline or toast error is shown
- [ ] The control is only visible to the current host; non-host players do not see it

## Notes

- Backend: `POST /api/rooms/{roomId}/transfer-host` endpoint must be verified to exist (see `docs/requirements.md` §11.1); if not, backend agent must implement it first
- Frontend + UX agents own the UI
- Identified as a gap by UX gap analysis 2026-03-14 (GAP-027) — no story previously existed
- Depends on story-013 (room waiting screen) and story-014 (host controls) being done first
