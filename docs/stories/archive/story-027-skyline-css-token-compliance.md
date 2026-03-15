---
id: story-027
title: Fix Skyline game CSS to use design system tokens
status: done
created: 2026-03-14
---

## What

Skyline's CSS module is updated to reference only tokens defined in `tokens.css`, so the game board, hand tiles, and scoreboard render with the correct Blade Runner colour palette.

## Why

`skyline/styles.module.css` references `--color-border`, `--color-surface`, `--color-primary`, `--text-lg`, `--text-sm` and similar properties that do not exist in the platform design system. All `var()` calls for these tokens silently resolve to nothing — the board renders with no background colours, no borders, and no text size variation.

## Acceptance criteria

- [x] All `var(--color-*)` references replaced with correct platform tokens: `--edge-subtle` / `--edge-strong` for borders; `--surface-float` / `--surface-raised` for surfaces; `--neon-cyan` for primary interactive colour
- [x] All `var(--text-*)` size references replaced with platform type scale tokens or `font-size` values from `tokens.css`
- [x] The Skyline board renders with correct colours and borders in dark theme without any browser dev tools warnings about undefined custom properties
- [x] No hard-coded hex or rgb values introduced — all colours via tokens
- [ ] Visual appearance reviewed by UX agent before merge

## Token mapping applied

| Old (broken) token | New (platform) token | Rationale |
|---|---|---|
| `--color-border` | `--edge-subtle` | Neutral cell border |
| `--color-surface` | `--surface-float` | Default cell / tile background |
| `--color-surface-hover` | `--surface-raised` | Hovered empty cell |
| `--color-surface-raised` | `--surface-overlay` | Occupied cell (more elevated) |
| `--color-primary` | `--neon-cyan` | Primary interactive colour per design system |
| `--color-on-primary` | `--surface-base` | Text on filled cyan background |
| `--text-lg` | `1.25rem` | No platform font-size token; explicit value |
| `--text-sm` | `0.875rem` | No platform font-size token; explicit value |

## Notes

- Frontend agent owns this; UX agent reviews
- Identified by UX gap analysis 2026-03-14 (GAP-017)
- File: `apps/frontend/src/games/skyline/styles.module.css`
- Small fix; no `/ui-design` run needed — just a token mapping exercise
