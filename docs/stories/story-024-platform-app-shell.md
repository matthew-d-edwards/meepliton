---
id: story-024
title: Platform AppShell — shared header wraps every page
status: in-progress
created: 2026-03-14
---

## What

Every page in the app is wrapped by a shared `<AppShell>` component that renders the platform header (logo, navigation, theme toggle, sign-out), so no page needs to roll its own chrome.

## Why

Without a shared header, each page duplicates chrome inconsistently, the theme toggle has nowhere consistent to live, and the room page has no header at all — the platform branding disappears mid-session.

## Acceptance criteria

- [x] A `<AppShell>` platform chrome component exists in `packages/ui/src/`
- [x] `<AppShell>` renders a sticky `.meepliton-header` containing: the Meepliton logo (`.meepliton-logo` / `.font-display .neon-gold`), a theme toggle slot, and a sign-out icon button (when authenticated)
- [x] `AppShell` is used by all routes: sign-in, register, lobby, and room — the header is consistently present across the full session
- [x] The sign-out button calls the auth context `logout()` method and redirects to `/login`
- [ ] The theme toggle is wired to the theme hook from story-011
- [x] On mobile (375px) the header collapses to logo + icon buttons only; no text labels in the header
- [x] The header uses `.meepliton-header` + `backdrop-filter` containment and correct padding per tokens
- [x] All colours, fonts, and spacing use tokens — no hard-coded values

## Notes

- UX agent owns the header design; frontend agent implements
- Prerequisite for story-011 (theme toggle needs a home) and story-007 (profile link in header)
- Run `/ui-design app shell header` before building
- Identified by UX gap analysis 2026-03-14 (GAP-011)

## Implementation notes

- `AppShell` is intentionally router-agnostic — no `react-router-dom` dependency so it works in
  Storybook or tests. Logo link and post-sign-out navigation are provided by the caller via
  `logoLinkAs` and `onSignOut` props.
- `themeToggle` prop is a structural slot. Story-011 should pass `<ThemeToggle>` here from `App.tsx`.
- The auth context method is named `logout` (not `signOut`). Story-011 can rename it consistently
  if desired — this story does not rename existing auth API.
- `LobbyPage` inline `<header className="lobby-header">` has been removed — `AppShell` handles chrome.
