---
id: story-022
title: Liar's Dice game module is added to the platform
status: refined
created: 2026-03-14
updated: 2026-03-14
---

## What

Liar's Dice — a dice-based bluffing game for 2–6 players — is built as the second game module, proving the game system is general-purpose and not shaped around Skyline's tile-placement model.

## Why

One game could be a special case. Liar's Dice is structurally different (dice rolling, hidden information, elimination mechanics, per-round rule changes) and will validate that the contracts, scaffolding, Aspire orchestration, and per-game EF Core migrations work for a genuinely different game type.

## Design decisions

- **Game:** Liar's Dice (dice-based bluffing)
- **Theme:** Clean, modern pirate aesthetic — maritime navigation charts, weathered brass, dark ocean colours. Not cheesy, no cartoon skulls. Theme is scoped to the in-game room UI only; the lobby and platform header keep the Skyline (Blade Runner) theme.
- **Theme delivery:** `data-game-theme="pirates"` attribute on the room wrapper element, with its own CSS token overrides in the game module's stylesheet.
- **Purpose:** Prove Aspire orchestration, per-game EF Core migrations, and dynamic game loading with a game that has hidden information, elimination, and round-varying rules.

## Acceptance criteria

- [ ] Given the game is scaffolded with `scripts/new-game.ps1`, no manual changes to any platform file (`Program.cs`, `PlatformDbContext`, hub, CI workflow) are required
- [ ] The game has its own `LiarsDiceDbContext` with isolated `__EFMigrationsHistory_liarsdice` table
- [ ] Given the CI pipeline runs, the Liar's Dice migration step completes without changing any platform files
- [ ] Given a user opens the lobby, Liar's Dice appears in the game catalogue automatically (Scrutor discovery)
- [ ] Given 2–6 players are in a room, the host can start the game; each player sees their own dice only (hidden information)
- [ ] Given it is a player's turn, they can place a bid (quantity + face) or call Liar; invalid bids are rejected with a message
- [ ] Given a player calls Liar, all dice are revealed; the correct loser loses one die; eliminated players transition to spectator
- [ ] Given a player has one die, they may trigger a Palifico round in which 1s are not wild
- [ ] Given the last player standing wins, the game transitions to `finished` phase and broadcasts a `GameOverEffect`
- [ ] Given a player is eliminated, they remain in the room as a spectator and see the full game state
- [ ] The game room renders with the pirate theme (`data-game-theme="pirates"` on the room wrapper)
- [ ] The frontend includes a custom SVG dice component that renders faces 1–6
- [ ] Given Liar is called, a reveal animation plays before the result is shown
- [ ] The game module is mobile-friendly at 375px viewport; all interactive elements have minimum 44px touch targets

## Notes

- Spec: `docs/specs/story-022-liars-dice.md`
- Branch: `claude/story-022-liars-dice-spec-GAnxk`
- Agents: backend + frontend implement in parallel after `/story-review`; devops adds CI migration step; ally reviews theming and accessibility
- Owner TODO item resolved: second game is Liar's Dice (was open question 2026-03-14)
