---
id: story-030
title: Dead Man's Switch — bluffing game module (heist / bomb-defusal theme)
status: refined
created: 2026-03-22
---

## What

Players can create a room, select Dead Man's Switch, and play a complete game of the Skull-inspired disc-bluffing game with 3–6 players.

## Why

Adds a third game module to the platform, proving the module system handles multi-phase turn structures (place → bid → reveal) and per-player hidden state (face-down disc stacks), and delivers a fast, tense party game the group can play in under 30 minutes.

## Acceptance criteria

### Setup
- [ ] Given a room with 3–6 players, when the host sends `StartGame`, then each player's state has `rosesOwned: 3`, `skullOwned: true`, `stack: []`, `stackCount: 0`, `pointsWon: 0`, `active: true`, and the phase is `Placing` with `currentPlayerIndex: 0` (seat 0 leads round 1).
- [ ] Given a room with fewer than 3 or more than 6 players, when the host sends `StartGame`, then the action is rejected with a clear reason and the game does not begin.

### Placing phase
- [ ] Given the `Placing` phase and it is player A's turn, when player A sends `PlaceDisc`, then a disc is added face-down to the top of player A's stack, `stackCount` increments, `totalDiscsOnTable` increments, and the turn advances to the next active player.
- [ ] Given the `Placing` phase and it is not player A's turn, when player A sends `PlaceDisc`, then the action is rejected and the state is unchanged.
- [ ] Given the `Placing` phase and player A has `stackCount == 0` this round, when player A sends `StartBid`, then the action is rejected.
- [ ] Given the `Placing` phase and player A has `stackCount >= 1`, when player A sends `StartBid` with a target count between 1 and `totalDiscsOnTable` inclusive, then the phase transitions to `Bidding` with that target count as `currentBid` and player A in the bidding rotation.
- [ ] Given the `Placing` phase and player A sends `StartBid` with a target count of 0 or greater than `totalDiscsOnTable`, then the action is rejected.

### Bidding phase
- [ ] Given the `Bidding` phase and it is player B's turn and `currentBid` is N, when player B sends `RaiseBid` with `newBid` of N+1 or higher (up to `totalDiscsOnTable`), then the bid updates and the turn advances to the next non-passed active player.
- [ ] Given the `Bidding` phase and player B sends `RaiseBid` with `newBid` less than or equal to `currentBid`, then the action is rejected.
- [ ] Given the `Bidding` phase and player B sends `Pass`, then player B has `passed: true` and the turn advances to the next non-passed active player.
- [ ] Given the `Bidding` phase and only one player has not passed, then that player becomes the Challenger automatically and the phase transitions to `Revealing` without a player action.
- [ ] Given the `Bidding` phase and a player raises `newBid` to equal `totalDiscsOnTable`, then that player immediately becomes the Challenger and the phase transitions to `Revealing`.

### Revealing phase
- [ ] Given the `Revealing` phase and the Challenger still has unflipped discs in their own stack, when the Challenger sends `FlipDisc` targeting their own ID, then the top unflipped disc is flipped, `lastFlip` is set, and the flipped count increments.
- [ ] Given the `Revealing` phase and the Challenger still has unflipped discs in their own stack, when the Challenger sends `FlipDisc` targeting any opponent's stack, then the action is rejected with "You must flip your own devices first."
- [ ] Given the `Revealing` phase and the Challenger flips the Nth non-skull disc where N equals `currentBid`, then the Challenger's `pointsWon` increments and the phase transitions to `RoundOver`.
- [ ] Given a Challenger's `pointsWon` reaches 2 on a successful reveal, then the phase transitions to `Finished`, `winner` is set, and a `GameOverEffect` is emitted.

### Revealing — failure (own skull)
- [ ] Given the Challenger flips their own skull disc, then the phase transitions to `DiscardChoice` and only the Challenger may send `DiscardDisc`.
- [ ] Given the `DiscardChoice` phase and both disc types are available, when the Challenger sends `DiscardDisc` with `discType: 'Rose'` or `discType: 'Skull'`, then that type decrements permanently, the Challenger is set as next round's first player, and the phase transitions to `RoundOver`.
- [ ] Given the `DiscardChoice` phase and the Challenger owns only one disc type, then the server auto-discards that type immediately and transitions to `RoundOver` without waiting for `DiscardDisc`.

### Revealing — failure (opponent skull)
- [ ] Given the Challenger flips an opponent's skull (owner = player C), then the server immediately picks one of the Challenger's remaining discs at random to permanently remove, sets player C as next round's first player, and the phase transitions to `RoundOver` — no player action required.

### Elimination and last-player win
- [ ] Given a player's total remaining discs reaches 0, then `active` is set to `false` and that player spectates with full state visibility.
- [ ] Given only one player remains `active`, then the phase transitions to `Finished`, `winner` is set to that player's ID, and a `GameOverEffect` is emitted.

### Round reset
- [ ] Given the phase is `RoundOver`, when any active player sends `StartNextRound`, then all active players have stacks cleared, the phase returns to `Placing`, and `currentPlayerIndex` is set to `nextRoundFirstPlayerIndex`.

### Projection
- [ ] Given any player receives projected state during `Placing` or `Bidding`, then opponent `stack` fields are `[]` and `stackCount` reflects the accurate disc count.
- [ ] Given a disc in an opponent's stack has `flipped: true` during `Revealing`, then all players' projected state includes that disc entry with `type` visible.
- [ ] Given the phase is `RoundOver` or `Finished`, then all players receive full unredacted state.

## Notes

- Spec: `docs/specs/deadmansswitch.md`
- Branch: (link once created)
- PR: (link once opened)
