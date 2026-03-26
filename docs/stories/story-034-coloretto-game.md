---
id: story-034
title: Add Coloretto game module
status: backlog
created: 2026-03-26
---

## What

Players can play Coloretto on Meepliton — a 2–5 player push-your-luck set-collection game where each turn you either draw a card onto a row (building it up for others or yourself) or take an entire row and collect its cards, then score your best 3 colour groups positively and the rest negatively.

## Why

Coloretto's elegant draw-or-take mechanic maps directly to simple server actions with interesting strategic depth, and the colour-group scoring is visually satisfying to display in a browser. It also provides a model for games without player elimination.

## Acceptance criteria

- [ ] Given a 4-player game, when the game starts, then there are 5 rows and the deck contains cards for 6 active colours + 3 jokers + 1 end card
- [ ] Given a player draws a card, when they place it on a row that already has 3 cards, then the action is rejected
- [ ] Given a player takes a row, when the row is empty (0 cards), then the action succeeds (empty row take is valid)
- [ ] Given a player has taken a row this round, when they try to draw or take again, then the action is rejected
- [ ] Given all players have taken a row, when the round ends, then all rows reset and HasTakenThisRound resets for all players
- [ ] Given the end-game card is drawn, when the current round finishes (all players take), then the game ends and scoring runs
- [ ] Given a player's collection is scored, when they have 4+ colours, then only their top 3 score positively and the rest score negatively
- [ ] Given a player holds joker cards, when final scoring runs, then jokers are assigned to the colour group that maximises that player's score
- [ ] Given the scoring scale, then 1 card=1pt, 2=3, 3=6, 4=10, 5=15, 6=21, 7=28 (both positive and negative)
- [ ] Given state is projected, when a client receives it, then deckSize (integer) is included but the deck card list is not

## Notes

- Spec: `docs/specs/coloretto.md`
- Branch: (link once created)
- PR: (link once opened)
