---
id: story-010
title: Lobby page — view active rooms and start or join a game
status: refined
created: 2026-03-14
---

## What

After signing in, the user lands on a lobby page that shows their active rooms and lets them create a new room or join one by code.

## Why

The lobby is the product's front door — it needs to exist before the app is usable end-to-end.

## Acceptance criteria

- [ ] After sign-in the user is redirected to `/lobby`
- [ ] The lobby shows a list of the user's active rooms, each with the game name, status badge (waiting / playing / finished), and a "Rejoin" button
- [ ] An empty state is shown when the user has no rooms ("No active games — start one below")
- [ ] A "Join a room" input accepts a 6-character code and navigates to `/room/{roomId}` on success
- [ ] A "New game" section lists all registered games with name and description; clicking one creates a room and navigates to the room waiting screen
- [ ] Invalid join codes show an inline error; the input accepts only alphanumeric characters
- [ ] The page works at 375px; the join input and game cards have ≥44px tap targets
- [ ] The page uses the Blade Runner design system throughout

## Notes

- Frontend agent owns the page; UX agent should review before merge
- Backend: depends on story-009 (lobby API)
- Run `/ui-design lobby page` before starting to align on layout
- Route: `apps/frontend/src/platform/lobby/LobbyPage.tsx`
