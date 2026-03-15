---
id: story-011
title: User can toggle dark and light theme with preference remembered
status: done
created: 2026-03-14
completed: 2026-03-14
---

## What

The user can switch between dark and light themes from anywhere in the app, and their choice is remembered across sessions.

## Why

Some users prefer light mode (especially in bright environments), and system-preference detection ensures a good first experience.

## Acceptance criteria

- [x] The app defaults to the user's OS colour scheme preference on first visit (`prefers-color-scheme`)
- [x] A theme toggle control is present in the global navigation/header on all pages
- [x] Toggling switches the theme immediately without a page reload
- [x] The preference is persisted in `localStorage` and applied on subsequent visits before any flash of the wrong theme
- [x] Dark theme uses `data-theme="dark"` on `<html>`; light theme uses `data-theme="light"` — all colours come from CSS custom properties, no hard-coded hex in components
- [x] Both themes satisfy WCAG AA contrast ratios for text on backgrounds

## Implementation notes

- FOUC prevention: inline `<script>` added to `<head>` in `apps/frontend/index.html` before any stylesheets — reads `localStorage` and `prefers-color-scheme`, sets `data-theme` on `<html>` synchronously before React hydrates.
- Light theme tokens added to `[data-theme="light"]` in `packages/ui/src/styles/tokens.css` — "neon on wet concrete" palette: light grey surfaces, dark text, muted neons, gold accent.
- `useTheme` hook in `packages/ui/src/theme/useTheme.ts` — reads `data-theme` attribute for initial state (no flash), toggles by writing attribute + localStorage.
- `ThemeToggle` component in `packages/ui/src/components/ThemeToggle.tsx` — icon button with sun/moon SVG, uses `.theme-toggle` global class from tokens.css, aria-label for accessibility.
- Both exported from `packages/ui/src/index.ts`. Barrel re-exports in `apps/frontend/src/platform/theme/`.
- Toggle wired into `LobbyPage`, `LoginPage`, and `RoomPage` headers until AppShell (story-024) provides a unified header slot.

## Notes

- Frontend agent owns the toggle control and theme logic
- UX agent owns the `tokens.css` light-theme overrides
- No backend involvement — purely client-side
- The design system's Blade Runner aesthetic must hold in light mode: neons on wet concrete, not neon on white (see UX agent)
- **FOUC prevention is critical**: `data-theme` must be set on `<html>` via an inline `<script>` in `index.html` before React hydrates — reading `localStorage` inside a `useEffect` is too late and will cause a flash of the wrong theme
- The toggle control lives in `<AppShell>` (story-024) — when story-024 ships, per-page ThemeToggle placements can be replaced with the AppShell `themeToggle` prop
- `User.theme` field already exists in `AuthContext.tsx` but is not yet read or applied — deferred to a follow-up
