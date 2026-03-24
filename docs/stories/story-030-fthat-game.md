---
id: story-030
title: Add F'That card game module
status: in-progress
created: 2026-03-19
---

## What

Players can create a room, select F'That, and play a full game of the No Thanks card mechanics with comedic theming — passing cursed cards around, collecting chips, and scoring chains — in real-time with 3–7 players. Chip counts are private (each player sees only their own exact count); all players see the remaining deck count at all times.

## Why

Adds a fast, light card game (15–20 min) that plays well at 3–7 players, gives the platform its first chip/card archetype, and is the first module to exercise `HasStateProjection: true` for per-player state masking.

## Spec

`docs/specs/fthat.md`

## Acceptance criteria

- [ ] **AC-1 — Setup:** Given a host starts an F'That game with 3–7 players, when `CreateInitialState` is called, then `deck` contains exactly 23 unique integers from [3..35], `faceUpCard` is a valid integer from [3..35] not in `deck`, `chipsOnCard` is 0, each player has `startingChips` chips (default 11, clamped to [7, 15]), `currentPlayerIndex` is 0, and `phase` is `Playing`.

- [ ] **AC-2 — Pass (chips available):** Given it is player A's turn and A has at least 1 chip, when A dispatches `{ type: "Pass" }`, then A loses 1 chip, `chipsOnCard` increases by 1, and the turn advances clockwise to the next player.

- [ ] **AC-3 — Pass (no chips):** Given it is player A's turn and A has 0 chips, when A dispatches `{ type: "Pass" }`, then the action is rejected with "You have no chips — you must take the card." and state is unchanged.

- [ ] **AC-4 — Take (mid-game):** Given it is player A's turn and `deck` is non-empty, when A dispatches `{ type: "Take" }`, then the face-up card and all chips on it move to A's collection/supply, the first card in `deck` becomes the new `faceUpCard` with 0 chips, `deck` shrinks by one, and the turn remains with player A.

- [ ] **AC-5 — Take (last card):** Given it is player A's turn and `deck` is empty (last card), when A dispatches `{ type: "Take" }`, then `phase` becomes `GameOver`, scores are computed using chain-minimum scoring, the player(s) with the lowest `total` are listed in `winners` in ascending seat order, and a `GameOverEffect` is emitted with the lowest-seat-index winner's ID.

- [ ] **AC-6 — Chain scoring:** Given a player holds cards [7, 8, 9, 20] (insertion order) and 3 remaining chips, when end-game scoring runs, then `cardScore` is 27, `chips` is 3, and `total` is 24 (cards 8 and 9 do not contribute). The scoring function sorts cards internally; the stored list is not mutated.

- [ ] **AC-7 — Wrong turn:** Given any player dispatches any action when it is not their turn, then the action is rejected with "It is not your turn." and state is unchanged.

- [ ] **AC-8 — Private chip projection:** Given player A's game view is generated, when `ProjectStateForPlayer(state, playerA.id)` is called, then player A's own view entry has `chips` = exact count and `chipsHidden` = false; every other player's view entry has `chips` = -1 and `chipsHidden` = true.

- [ ] **AC-9 — Deck count visible:** Given a game is in progress, when any player's projected state is generated, then `FThatView.deckCount` equals the number of cards remaining in `deck`, and the `deck` contents (card values and order) are not present in the projected state.

## Notes

- Branch: (link once created)
- PR: (link once opened)
