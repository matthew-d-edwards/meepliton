---
id: story-008
title: User stays signed in across browser sessions
status: refined
created: 2026-03-14
---

## What

A signed-in user who closes and reopens the browser is still signed in without having to log in again.

## Why

Requiring a sign-in every browser session is friction for a small friend group who use the app regularly.

## Acceptance criteria

- [ ] Given a signed-in user who closes the browser and reopens it, when they navigate to meepliton.com, then they land directly on the lobby without seeing the sign-in page
- [ ] The JWT cookie has a sliding expiry of 30 days and is refreshed on each authenticated request
- [ ] Given a user who explicitly clicks "Sign out", then their cookie is cleared and they are redirected to the sign-in page
- [ ] `POST /api/auth/logout` clears the auth cookie and returns 204
- [ ] A signed-out user visiting any protected route is redirected to `/sign-in`

## Notes

- Spec: `docs/requirements.md` §4 (persistent session user story), §5 (FR-AUTH-10)
- Backend agent owns cookie lifetime and logout endpoint
- Frontend agent owns the sign-out button (in nav/header) and the protected route redirect
- Depends on story-002 (sign-in) being done first
