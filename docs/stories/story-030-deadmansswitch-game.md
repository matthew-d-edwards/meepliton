---
id: story-030
title: Dead Man's Switch — bluffing game module (heist / bomb-defusal theme)
status: backlog
created: 2026-03-22
---

## What

Players can create a room, select Dead Man's Switch, and play a complete game of the Skull-inspired disc-bluffing game with 3–6 players.

## Why

Adds a third game module to the platform, proving the module system handles multi-phase turn structures (place → bid → reveal) and per-player hidden state (face-down disc stacks), and delivers a fast, tense party game the group can play in under 30 minutes.

## Acceptance criteria

- [ ] Given a room with 3–6 players, when the host starts the game, then each player receives 3 rose tokens and 1 skull token on their mat and the phase is `Placing`.
- [ ] Given the `Placing` phase and it is a player's turn, when they submit `PlaceDisc`, then a face-down disc is added to the top of their stack and the turn advances to the next player.
- [ ] Given the `Placing` phase and a player has already placed at least one disc, when they submit `StartBid` with an opening number, then the phase transitions to `Bidding` with that player as the current bidder.
- [ ] Given the `Bidding` phase, when the current player raises the bid, then all players receive the updated bid and the turn advances; when a player passes, then they are marked as passed and the turn advances past them; when all but one player have passed, then that player becomes the Challenger and the phase transitions to `Revealing`.
- [ ] Given the `Revealing` phase, when the Challenger flips discs (their own first, top-to-bottom, then freely from any other player's stack), and no skull is encountered, and the count reaches the bid number, then the Challenger gains 1 point, the player with 2 points wins the game, and a `GameOverEffect` is emitted.
- [ ] Given the `Revealing` phase, when the Challenger flips a disc that is their own skull, then the Challenger permanently loses one disc of their choice, the next round's first player is the Challenger's choice, and if the Challenger now has 0 discs they are eliminated.
- [ ] Given the `Revealing` phase, when the Challenger flips a disc that is an opponent's skull, then the Challenger permanently loses one disc chosen randomly by that opponent, the next round's first player is the skull's owner, and if the Challenger now has 0 discs they are eliminated.
- [ ] Given a player reaches 0 discs, they are eliminated; if only one player remains active, that player wins and a `GameOverEffect` is emitted.

## Notes

- Spec: `docs/specs/deadmansswitch.md`
- Branch: (link once created)
- PR: (link once opened)
