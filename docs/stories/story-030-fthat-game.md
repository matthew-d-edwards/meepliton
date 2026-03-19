---
id: story-030
title: Add F'That card game module
status: backlog
created: 2026-03-19
---

## What

Players can create a room, select F'That, and play a full game of the No Thanks card mechanics with comedic theming — passing cursed cards around, collecting chips, and scoring chains — in real-time with 3–7 players.

## Why

Adds a fast, light card game to the platform that plays well with 3–7 people and validates the `ReducerGameModule` pattern for a purely turn-based hand/chip game with no hidden information.

## Acceptance criteria

- [ ] Given a host creates a room with F'That selected and at least 3 players have joined, when the host starts the game, then a deck of 24 cards (33 cards numbered 3–35 with exactly 9 removed at random) is dealt face-down with one card face-up, each player starts with 11 chips, and the first player's turn begins.
- [ ] Given it is a player's turn, when they choose to Pass, then one chip is deducted from their supply and added to the face-up card, and the turn advances to the next player clockwise; if the player has zero chips, the Pass action is rejected with "You have no chips — you must take the card."
- [ ] Given it is a player's turn, when they choose to Take, then the face-up card and all chips on it are added to that player's collection, the next card from the deck is revealed, the chip count on the new card resets to zero, and the turn advances to the player who just took the card.
- [ ] Given two or more of a player's cards form a consecutive sequence (e.g. 7, 8, 9), when scores are calculated, then only the lowest card in each consecutive run counts toward that player's score (7 in this example; 8 and 9 are ignored).
- [ ] Given the last card in the deck is taken, when the game ends, then each player's score is computed as (sum of lowest cards in each consecutive run) minus (remaining chips); the player with the lowest score is declared the winner and a `GameOverEffect` is emitted with the winner's player ID.
- [ ] Given two or more players are tied for the lowest score at game end, then all tied players are listed as co-winners in the final state and the `GameOverEffect` carries the first tied player's ID by seat order.
- [ ] Given a player dispatches any action when it is not their turn, then the action is rejected with "It is not your turn." and the game state is unchanged.

## Notes

- Spec: `docs/specs/fthat.md`
- Branch: (link once created)
- PR: (link once opened)
