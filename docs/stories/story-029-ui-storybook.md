---
id: story-029
title: Storybook for @meepliton/ui platform chrome components
status: backlog
created: 2026-03-14
---

## What

A Storybook instance is added to `packages/ui/` so every platform chrome component can be developed, reviewed, and visually tested in isolation.

## Why

As the design system matures, reviewing component changes requires spinning up the full app. Storybook gives the UX and frontend agents (and human reviewers) a fast, isolated view of every component in every state — dark/light theme, empty/loaded/error, host/non-host, mobile/desktop.

## Acceptance criteria

- [ ] Storybook is configured in `packages/ui/` and can be run with a single command (`npm run storybook` or similar)
- [ ] Stories exist for all current platform chrome components: `<RoomWaitingScreen>`, `<PlayerPresence>`, `<JoinCodeDisplay>`, `<ActionRejectedToast>`, and `<AppShell>` (from story-024)
- [ ] Each component has at minimum: a default story, a mobile (375px) viewport story, and stories for key state variants (e.g. host vs non-host, empty vs populated, error state)
- [ ] Both dark and light themes are toggleable within Storybook via the `data-theme` attribute
- [ ] Storybook build is added to CI as a non-blocking job (build check only, no deploy required for Phase 3)

## Notes

- Phase 3 item — do not start until story-024 and the core platform chrome stories (010, 011, 012, 013) are done
- UX agent owns story writing; frontend agent owns Storybook configuration
- Identified by UX gap analysis 2026-03-14 (GAP-026)
