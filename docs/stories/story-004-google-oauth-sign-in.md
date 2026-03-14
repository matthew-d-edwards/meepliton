---
id: story-004
title: User can sign in with Google
status: refined
created: 2026-03-14
---

## What

A user can sign in (or create an account) by authenticating with their Google account — no password required.

## Why

Google OAuth reduces friction for users who don't want to manage another password, and pre-fills their display name and avatar.

## Acceptance criteria

- [ ] Given an unauthenticated user, when they click "Sign in with Google", then they are redirected to Google's consent screen
- [ ] Given a successful Google consent, when Google redirects back, then an account is created (first time) or the existing account is found, a JWT cookie is set, and the user lands on the lobby
- [ ] Given a first-time Google sign-in, then `display_name` and `avatar_url` are pre-filled from the Google profile
- [ ] Google-authenticated users are not required to confirm their email (Google has already verified it)
- [ ] `GET /api/auth/google` initiates the OAuth flow
- [ ] `GET /api/auth/google/callback` handles the callback, sets the cookie, and redirects to the frontend
- [ ] Signing in with a Google account whose email matches an existing email/password account links them (does not create a duplicate account)

## Notes

- Spec: `docs/requirements.md` §5 (FR-AUTH-06 to FR-AUTH-09), §11 (auth endpoints)
- Backend agent owns `.AddGoogle()` config in `Program.cs` and callback handler
- OQ-09 resolved: accept Google name automatically, allow override in profile settings
- Owner action required: Google OAuth client credentials (Client ID + Secret) must be configured in Azure — see `docs/owner/TODO.md`
