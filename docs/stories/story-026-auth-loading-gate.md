---
id: story-026
title: App shows neutral loading screen during initial auth check to prevent login flash
status: backlog
created: 2026-03-14
---

## What

While the app is resolving whether the user is authenticated (the initial `/api/auth/me` call), it shows a neutral loading screen instead of immediately redirecting to the sign-in page — eliminating the flash of the sign-in page on every hard refresh for authenticated users.

## Why

Currently, `AuthContext` sets `user = null` while loading, which causes `AppRoutes` to redirect authenticated users to `/sign-in` for the few hundred milliseconds the session check takes. This looks broken and erodes trust.

## Acceptance criteria

- [ ] `AuthContext` exposes a `loading: boolean` state that is `true` until the initial `/api/auth/me` response is received
- [ ] `AppRoutes` (or the auth guard) renders a neutral loading screen — not a redirect — while `loading === true`
- [ ] The loading screen uses design system tokens (dark surface, Meepliton logo or spinner) — not a blank white/unstyled screen
- [ ] Once `loading` becomes `false`, normal routing resumes: unauthenticated users go to `/sign-in`, authenticated users go to their destination
- [ ] Hard refreshing on `/lobby` or `/room/:id` while authenticated never shows the sign-in page before the lobby/room loads

## Notes

- Frontend agent owns this
- `AuthContext.tsx` already has a `loading` field — it just needs to be consumed correctly in `AppRoutes`
- Identified by UX gap analysis 2026-03-14 (GAP-020)
- Small change; no `/ui-design` run needed — just use the design system spinner/loading surface tokens
