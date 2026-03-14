---
id: story-019
title: Skyline game is playable end-to-end in a browser
status: done
created: 2026-03-14
---

## What

Two or more players can create a Skyline room, wait for each other, start the game, take turns placing tiles, and see a winner declared — all in the browser, in real time.

## Why

Skyline is the first game module and the integration test for the entire platform. If Skyline works end-to-end, the platform works.

## Acceptance criteria

- [ ] A host can create a Skyline room from the lobby and share the join code
- [ ] A second player can join via join code, URL, or QR code
- [ ] The host starts the game and both players see the Skyline board rendered by `SkylineModule`
- [x] Players take turns placing tiles; the board updates in real time for both players
- [x] Out-of-turn actions are rejected with an `ActionRejected` toast
- [x] When the board is full, the game calculates row/column scores, declares a winner, and shows a game-over state
- [ ] The Skyline board is mobile-friendly at 375px viewport
- [ ] The Skyline CI migration step runs successfully in GitHub Actions

## Notes

- This is a validation story, not a build story — it confirms stories 001–018 hang together
- Tester agent should write end-to-end scenario tests covering the full flow
- If any acceptance criterion fails, raise a bug story rather than blocking this story
- Spec: `docs/requirements.md` §10 (game module system), Phase 1 roadmap (Skyline)
- Branch: likely part of the initial scaffold already — validate rather than build
