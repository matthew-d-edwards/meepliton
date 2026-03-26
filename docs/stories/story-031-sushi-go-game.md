---
id: story-031
title: Add Sushi Go game module
status: backlog
created: 2026-03-26
---

## What

Players can play Sushi Go! on Meepliton — a 2–5 player card-drafting game where everyone simultaneously selects cards from a hand, then passes the hand left, for three rounds.

## Why

Validates the platform's simultaneous-action pattern (all players submit independently; server auto-advances when all have picked), and produces a visually rich, satisfying UI with strong replay value for the friend group.

## Acceptance criteria

- [ ] Given 2–5 players in a room, when the host starts Sushi Go, then each player receives the correct hand size (2p=10, 3p=9, 4p=8, 5p=7)
- [ ] Given a player has Chopsticks in their tableau, when they use them, then two cards are taken and Chopsticks return to the passing cycle
- [ ] Given all players have picked, when the server resolves the turn, then hands pass left and picks are revealed simultaneously
- [ ] Given a player has a Wasabi with no nigiri, when the round is scored, then Wasabi scores 0 pts
- [ ] Given a player places a nigiri on a Wasabi, when the round is scored, then the nigiri scores 3× its base value
- [ ] Given 3 or more rounds have completed, when pudding is scored, then the player with the most gets +6 and fewest gets −6 (no −6 penalty in 2-player)
- [ ] Given all 3 rounds are complete, when final scores are tallied, then the player with the highest total is declared winner
- [ ] Given a player is not the active turn-holder, when state is projected, then other players' hands are hidden (only hand size visible)

## Notes

- Spec: `docs/specs/sushi-go.md`
- Branch: (link once created)
- PR: (link once opened)
