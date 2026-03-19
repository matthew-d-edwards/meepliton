---
id: story-025
title: Room page shows loading spinner, error messages, and "Room not found" screen
status: done
created: 2026-03-14
---

## What

The room page handles all non-happy-path states gracefully: a styled loading state while connecting, a "Room not found" screen for bad join codes or expired rooms, and a friendly error for unknown game IDs.

## Why

Currently the room page renders bare `<p>Loading…</p>` and silently does nothing on a failed join — users are left on a broken URL with no feedback. A production-quality app must handle these states visibly.

## Acceptance criteria

- [ ] While `room` or `user` is not yet available, a styled loading screen is shown (spinner or skeleton, using design system tokens — not a bare `<p>`)
- [ ] When `POST /api/rooms/join` fails (invalid or expired code), the user is navigated to a `/room-not-found` route or shown an inline "Room not found" message — the broken URL is not left silently blank
- [ ] A "Room not found" page exists with a clear message, the code that was tried, and a "Back to lobby" button
- [ ] When a room's `gameId` has no registered frontend module (`Unknown game: {id}`), an error screen is shown with a human-readable message and a "Back to lobby" link — not a bare `<p>`
- [ ] While a game module is dynamically loading (`Loading game…` in `GameLoader`), a styled loading indicator is shown
- [ ] All error and loading states use `.meepliton-header` / `.container` chrome — they do not render on a raw unstyled surface
- [ ] All colours, fonts, and spacing use design system tokens

## Notes

- Frontend agent owns this
- Identified by UX gap analysis 2026-03-14 (GAP-013, GAP-014)
- Relevant files: `apps/frontend/src/platform/room/RoomPage.tsx` (`GameLoader`, `RoomLoadingScreen`, and `UnknownGameScreen` are defined inline in this file)
