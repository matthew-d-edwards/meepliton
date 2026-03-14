---
id: story-022
title: A second game module is added to validate the game system
status: backlog
created: 2026-03-14
---

## What

A second game (different in type from Skyline — ideally map-based or simultaneous-action) is built and added to the platform to prove the module system works for genuinely different games.

## Why

One game could be a special case. A second, structurally different game proves the contracts and scaffolding are general-purpose.

## Acceptance criteria

- [ ] The game is scaffolded with `scripts/new-game.ps1` — no manual changes to the platform core
- [ ] The game has its own `DbContext` and EF Core migrations with an isolated history table
- [ ] The CI pipeline runs the new game's migration step without changes to any platform files
- [ ] The game appears in the lobby game catalogue automatically (Scrutor discovery)
- [ ] The game is playable end-to-end by 2+ players: create room → wait → start → play → finish
- [ ] The game module is mobile-friendly at 375px

## Notes

- Spec: `docs/requirements.md` Phase 2 roadmap ("Second game module — validates module system for a genuinely different game type")
- Status `backlog` — Phase 2; the game concept needs to be chosen (owner decision)
- Owner decision needed: what should the second game be? It should be structurally different from Skyline (tile placement). (See `docs/owner/TODO.md`)
- Agents: analyst designs it, architect reviews contracts, backend + frontend implement, tester validates, devops adds CI step
