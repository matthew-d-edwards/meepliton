---
id: story-005
title: User can link Google and email/password to one account
status: refined
created: 2026-03-14
---

## What

A user who registered with one auth method (Google or email/password) can add the other method to their account so they can sign in either way.

## Why

Users may start with Google and later want a password for situations where Google isn't available, or vice versa.

## Acceptance criteria

- [ ] Given a user signed in via Google only, when they visit account settings and add a password, then they can subsequently sign in with email + password as well
- [ ] Given a user signed in via email/password only, when they visit account settings and click "Link Google account", then after completing Google consent their account is linked and they can sign in with either method
- [ ] `GET /api/auth/me` includes a `loginMethods` array listing `"google"` and/or `"password"` so the UI knows which options to show
- [ ] `POST /api/auth/add-password` accepts `{ newPassword }` — only available when account has no password yet
- [ ] `GET /api/auth/link-google` initiates the Google OAuth link flow for an already-signed-in user
- [ ] Linking an already-used Google account (belonging to a different user) returns an error
- [ ] Attempting to add a password when one already exists returns 400

## Notes

- Spec: `docs/requirements.md` §4 (account linking user stories), §11 (auth API endpoints)
- Backend agent owns the endpoints; frontend agent owns the account settings UI section
- Depends on story-001 (email registration) and story-004 (Google OAuth) both being done first
