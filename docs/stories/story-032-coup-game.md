---
id: story-032
title: Add Coup game module
status: backlog
created: 2026-03-26
---

## What

Players can play Coup on Meepliton — a 2–6 player hidden-role bluffing game where players take actions claiming characters they may or may not hold, and any player can challenge or block.

## Why

Introduces the most complex interaction pattern in the platform: multi-step turn resolution with asynchronous responses from multiple non-active players (challenge, block, block-challenge, influence-loss choice). Validating this pattern unblocks future games with similar mechanics.

## Acceptance criteria

- [ ] Given a player has ≥ 10 coins at the start of their turn, when they try to take any action other than Coup, then the action is rejected
- [ ] Given a player declares Tax (claiming Duke), when any other player challenges, then the challenge resolves correctly (reveal card → draw replacement if true; lose influence if false)
- [ ] Given a player attempts Foreign Aid, when another player blocks (claims Duke), then the action fails if the block is unchallenged
- [ ] Given a player blocks an assassination (claims Contessa) and is challenged, when the blocker does not hold Contessa, then the blocker loses influence and the assassination resolves
- [ ] Given a player has 0 face-down influence cards, when the last is revealed, then that player is eliminated and becomes a spectator
- [ ] Given only 1 active player remains, when the last elimination occurs, then GameOverEffect is emitted and that player is declared winner
- [ ] Given a player's face-down cards are projected, when another player receives state, then those card names are hidden (null)
- [ ] Given a player uses Exchange (Ambassador), when they choose 2 cards to keep, then the remaining cards return to the deck

## Notes

- Spec: `docs/specs/coup.md`
- Branch: (link once created)
- PR: (link once opened)
