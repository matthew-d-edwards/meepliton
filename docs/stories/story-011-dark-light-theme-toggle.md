---
id: story-011
title: User can toggle dark and light theme with preference remembered
status: refined
created: 2026-03-14
---

## What

The user can switch between dark and light themes from anywhere in the app, and their choice is remembered across sessions.

## Why

Some users prefer light mode (especially in bright environments), and system-preference detection ensures a good first experience.

## Acceptance criteria

- [ ] The app defaults to the user's OS colour scheme preference on first visit (`prefers-color-scheme`)
- [ ] A theme toggle control is present in the global navigation/header on all pages
- [ ] Toggling switches the theme immediately without a page reload
- [ ] The preference is persisted in `localStorage` and applied on subsequent visits before any flash of the wrong theme
- [ ] Dark theme uses `data-theme="dark"` on `<html>`; light theme uses `data-theme="light"` — all colours come from CSS custom properties, no hard-coded hex in components
- [ ] Both themes satisfy WCAG AA contrast ratios for text on backgrounds

## Notes

- Frontend agent owns the toggle control and theme logic
- UX agent owns the `tokens.css` light-theme overrides
- No backend involvement — purely client-side
- The design system's Blade Runner aesthetic must hold in light mode: neons on wet concrete, not neon on white (see UX agent)
