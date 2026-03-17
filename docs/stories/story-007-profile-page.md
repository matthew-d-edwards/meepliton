---
id: story-007
title: User can update their display name and avatar
status: refined
created: 2026-03-14
---

## What

A signed-in user can view and update their display name and avatar URL from their account settings page.

## Why

Display names and avatars appear in game rooms, so users need a way to personalise how they appear to others.

## Acceptance criteria

### Endpoint shape

- [ ] **AC-1** `GET /api/auth/me` returns `{ id, displayName, avatarUrl, theme }`. `loginMethods` is not included — that belongs to `GET /api/auth/me/login-methods` (account-linking story).
- [ ] **AC-2** `PUT /api/auth/me` accepts `{ displayName?, avatarUrl? }` and responds 204 on success.

### Display name validation

- [ ] **AC-3** Display name must be 1–32 characters. Returns 400 with a descriptive message if the rule is violated. This same 1–32 character rule is enforced on `POST /api/auth/register` — the validation logic must be shared or consistently applied in both places.

### Avatar URL validation

- [ ] **AC-4** `avatarUrl` on `PUT /api/auth/me` must be an absolute HTTPS URL or `null`. Any other value (empty string, HTTP URL, relative path) returns 400. Sending `null` clears the user's override and falls back to the derived default.

### Profile page behaviour

- [ ] **AC-5** The profile page (`/account`) shows the current display name and avatar with an edit form.
- [ ] **AC-6** While `GET /api/auth/me` is in-flight, inputs are disabled (or a spinner is shown) and the form is not interactive.
- [ ] **AC-7** If `GET /api/auth/me` fails with a network error, an error message is displayed on the page. The user can retry.
- [ ] **AC-8** If `GET /api/auth/me` returns 401, the frontend redirects to `/login`.
- [ ] **AC-9** The save action is NOT optimistic. The UI updates display name and avatar only after a successful 204 response from `PUT /api/auth/me`. No other pages in the same session are required to update.

### Gravatar default (email users)

- [ ] **AC-10** For email/password users who have not set a custom `avatarUrl`, the platform derives the Gravatar URL from their normalized (lowercased) email MD5 hash. This is the initial default and is displayed on the profile page. The user can override it by providing a valid HTTPS URL via `PUT /api/auth/me`.

### Google avatar handling

- [ ] **AC-11** When a user first signs in with Google and has no `avatarUrl` override set, the Google profile photo is displayed.
- [ ] **AC-12** Once a user sets a custom `avatarUrl` override, subsequent Google sign-ins do NOT overwrite it. The user-set override is preserved.

## Notes

- Spec: `docs/requirements.md` §4 (profile user stories), §11 (account endpoints)
- Backend agent owns `GET/PUT /api/auth/me`; frontend agent owns the `/account` settings page
- Avatar strategy decided: Gravatar for email users (derive from email hash); URL-only override for v1 — no file upload

---

## Story review

**Reviewed by:** analyst (adversarial) + tester
**Date:** 2026-03-17
**Challenges raised:** 10
**Resolved:** 10
**Criteria added:** 4
**Verdict:** Ready for implementation

### Key edge cases to implement

- `avatarUrl` must be validated as an absolute HTTPS URL or `null` on `PUT /api/auth/me`
- Google re-login must not overwrite a user-set avatar override
- Profile page must handle 401 (redirect to `/login`) and network error (display message)
- Display name 1–32 character rule must be enforced on both `PUT /api/auth/me` and `POST /api/auth/register`

### Test complexity note

- Endpoint tests: xUnit integration (needs real auth cookie)
- Validator logic: xUnit unit test
- Frontend form states: React Testing Library component test
- No SignalR concerns — profile is REST-only
