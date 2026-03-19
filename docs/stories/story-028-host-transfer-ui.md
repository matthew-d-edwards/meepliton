---
id: story-028
title: Host can transfer host status to another player
status: done
created: 2026-03-14
completed: 2026-03-19
---

## What

The host of a room can hand off host status to any other connected player — from the waiting screen.

## Why

If the original host needs to leave mid-session, there is currently no way to pass control. The `POST /api/rooms/{roomId}/transfer-host` endpoint had no backend implementation or UI entry point.

## Acceptance criteria

- [x] On the waiting screen, the host sees a "Make host" (★) action next to each non-host player (alongside the existing "Remove" button)
- [x] The button is hidden on the host's own player row
- [x] Triggering the action calls `POST /api/rooms/{roomId}/transfer-host` with the target player's ID
- [x] On success, the old host loses host controls and the new host gains them in real time via `HostTransferred` SignalR event
- [x] Self-transfer rejected by backend (400) and button not shown in UI
- [x] The control is only visible to the current host; non-host players do not see it

## Notes

- Backend: `POST /api/rooms/{roomId}/transfer-host` in `src/Meepliton.Api/Endpoints/RoomEndpoints.cs` — guards: host-only (403), self-transfer (400), waiting-only (409), non-member target (404)
- Frontend: `onTransferHost` prop + `currentUserId` on `RoomWaitingScreen`; `transferHost()` + `HostTransferred` handler in `RoomPage.tsx`
- CSS: `.player-presence__transfer` in `packages/ui/src/styles/tokens.css`
- Accessibility: ★ button has `aria-label="Make {name} the host"`, `:focus-visible` ring applied, `.sr-only` utility added for presence dot
- Architect review: PASS
- Ally review: PASS — presence dot fixed (aria-hidden + sr-only), focus rings added
