---
id: story-013
title: Host sees a waiting screen with join code and connected players
status: done
created: 2026-03-14
---

## What

When a host creates a room, they land on a waiting screen that prominently shows the join code, the shareable URL and QR code, and a live list of connected players.

## Why

The host needs to share the room and see who has joined before they can start the game.

## Acceptance criteria

- [ ] After creating a room the host is taken to `/room/{roomId}` in waiting state
- [x] The join code is displayed prominently (large font, easy to read aloud)
- [ ] The join URL and QR code are displayed (links to story-012)
- [x] A player list shows everyone who has joined, with their display name and avatar
- [x] Each player shows a connected/disconnected indicator (green dot / grey dot)
- [x] The "Start game" button is enabled only when the minimum player count for the game is met
- [x] The host sees a "Remove" button next to each non-host player
- [x] Non-host players on the same screen see the waiting state but without the Start / Remove controls
- [x] The waiting screen is part of `<RoomWaitingScreen>` in `packages/ui/src/` (platform chrome)
- [x] Works at 375px with all player names visible (truncate with ellipsis if necessary)

## Notes

- Spec: `docs/requirements.md` §4 (game room user stories), §5 (FR-ROOM-01)
- UX + frontend agents own the `<RoomWaitingScreen>` platform component
- Depends on story-012 (QR code / join URL) and story-014 (host controls)
- Real-time player list updates come via SignalR `PlayerJoined` / `PlayerLeft` / `PlayerConnected` / `PlayerDisconnected` events
- Run `/ui-design room waiting screen` before building
