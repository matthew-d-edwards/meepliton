---
id: story-024
title: Platform AppShell — shared header wraps every page
status: backlog
created: 2026-03-14
---

## What

Every page in the app is wrapped by a shared `<AppShell>` component that renders the platform header (logo, navigation, theme toggle, sign-out), so no page needs to roll its own chrome.

## Why

Without a shared header, each page duplicates chrome inconsistently, the theme toggle has nowhere consistent to live, and the room page has no header at all — the platform branding disappears mid-session.

## Acceptance criteria

- [ ] A `<AppShell>` platform chrome component exists in `packages/ui/src/`
- [ ] `<AppShell>` renders a sticky `.meepliton-header` containing: the Meepliton logo (`.meepliton-logo` / `.font-display .neon-gold`), a theme toggle icon button, and a sign-out icon button (when authenticated)
- [ ] `AppShell` is used by all routes: sign-in, register, lobby, and room — the header is consistently present across the full session
- [ ] The sign-out button calls the auth context `signOut()` method and redirects to `/sign-in`
- [ ] The theme toggle is wired to the theme hook from story-011
- [ ] On mobile (375px) the header collapses to logo + icon buttons only; no text labels in the header
- [ ] The header uses `.container` for max-width containment and correct padding
- [ ] All colours, fonts, and spacing use tokens — no hard-coded values

## Notes

- UX agent owns the header design; frontend agent implements
- Prerequisite for story-011 (theme toggle needs a home) and story-007 (profile link in header)
- Run `/ui-design app shell header` before building
- Identified by UX gap analysis 2026-03-14 (GAP-011)
