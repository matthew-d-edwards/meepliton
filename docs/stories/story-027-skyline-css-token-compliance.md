---
id: story-027
title: Fix Skyline game CSS to use design system tokens
status: backlog
created: 2026-03-14
---

## What

Skyline's CSS module is updated to reference only tokens defined in `tokens.css`, so the game board, hand tiles, and scoreboard render with the correct Blade Runner colour palette.

## Why

`skyline/styles.module.css` references `--color-border`, `--color-surface`, `--color-primary`, `--text-lg`, `--text-sm` and similar properties that do not exist in the platform design system. All `var()` calls for these tokens silently resolve to nothing — the board renders with no background colours, no borders, and no text size variation.

## Acceptance criteria

- [ ] All `var(--color-*)` references replaced with correct platform tokens: `--edge-subtle` / `--edge-strong` for borders; `--surface-float` / `--surface-raised` for surfaces; `--neon-cyan` for primary interactive colour
- [ ] All `var(--text-*)` size references replaced with platform type scale tokens or `font-size` values from `tokens.css`
- [ ] The Skyline board renders with correct colours and borders in dark theme without any browser dev tools warnings about undefined custom properties
- [ ] No hard-coded hex or rgb values introduced — all colours via tokens
- [ ] Visual appearance reviewed by UX agent before merge

## Notes

- Frontend agent owns this; UX agent reviews
- Identified by UX gap analysis 2026-03-14 (GAP-017)
- File: `apps/frontend/src/games/skyline/styles.module.css`
- Small fix; no `/ui-design` run needed — just a token mapping exercise
