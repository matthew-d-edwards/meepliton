---
id: story-033
title: Add Love Letter game module
status: backlog
created: 2026-03-26
---

## What

Players can play Love Letter on Meepliton — a 2–4 player micro card game where each player holds 1 card, draws 1 per turn, and plays 1, trying to be the last standing or hold the highest card when the deck runs out, winning Tokens of Affection across multiple rounds.

## Why

Love Letter has the smallest possible state surface of any target game, making it a fast win for validating multi-round play, private Priest reveals (per-player projected information), and async-friendly sequential turns.

## Acceptance criteria

- [ ] Given a player holds Countess + King (or Prince), when they attempt to play King or Prince, then the action is rejected with a Countess-forced-discard error
- [ ] Given a player plays Guard and guesses correctly, when the target holds that character, then the target is eliminated
- [ ] Given a player plays Priest, when state is projected, then only the Priest player sees the revealed card (PriestReveal null for all others)
- [ ] Given a player is protected by Handmaid, when another player tries to target them, then the action is rejected
- [ ] Given a player plays or discards the Princess for any reason, then that player is eliminated immediately
- [ ] Given the deck is empty after a player draws, when hands are compared, then the player with the highest card wins the round; tie-break by highest discard total
- [ ] Given a player reaches the token threshold (2p=7, 3p=5, 4p=4), when the round ends, then they are declared game winner
- [ ] Given a new round starts, when state is initialised, then 1 card is set aside face-down; in 2-player, 3 additional cards are set aside face-up

## Notes

- Spec: `docs/specs/love-letter.md`
- Branch: (link once created)
- PR: (link once opened)
