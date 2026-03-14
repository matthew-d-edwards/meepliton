---
id: story-007
title: User can update their display name and avatar
status: backlog
created: 2026-03-14
---

## What

A signed-in user can view and update their display name and choose or upload an avatar from their account settings page.

## Why

Display names and avatars appear in game rooms, so users need a way to personalise how they appear to others.

## Acceptance criteria

- [ ] `GET /api/account/me` returns `{ displayName, avatarUrl, email, loginMethods }`
- [ ] `PUT /api/account/me` accepts `{ displayName, avatarUrl }` and updates the profile
- [ ] Display name must be 1–32 characters; returns 400 with a message if invalid
- [ ] Profile page (`/account`) shows current display name and avatar with an edit form
- [ ] Changes are reflected immediately in the UI after saving (no page reload)
- [ ] Google-authenticated users see their Google avatar pre-filled but can override it

## Notes

- Spec: `docs/requirements.md` §4 (profile user stories), §11 (account endpoints)
- Backend agent owns `GET/PUT /api/account/me`; frontend agent owns the settings page
- Status is `backlog` because avatar upload storage (where do files go?) needs a decision before this is fully refined
- Owner decision needed: self-hosted upload vs Gravatar vs URL-only for v1 (see `docs/owner/TODO.md`)
