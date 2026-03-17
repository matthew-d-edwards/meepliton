---
id: story-002
title: User can sign in with email and password
status: done
created: 2026-03-14
---

## What

A registered and confirmed user can sign in with their email and password, and is locked out for 15 minutes after 5 consecutive failed attempts.

## Why

Email/password sign-in is the baseline return path for users not using Google OAuth.

## Acceptance criteria

- [x] Given a valid email + correct password, when the user submits, then they receive an HttpOnly JWT cookie and are redirected to the lobby
- [x] Given a valid email + wrong password, when the user submits, then they receive a generic "incorrect email or password" message (no hint about which field is wrong)
- [x] Given 5 consecutive failed attempts on one account, when the 5th attempt fails, then the account is locked for 15 minutes and subsequent attempts return a "too many attempts" message with the unlock time
- [x] Given a locked account, when the lock expires, then sign-in works normally again
- [x] Given an unconfirmed email account, when the user attempts to sign in, then they are told to confirm their email and offered a resend link
- [x] `POST /api/auth/login` accepts `{ email, password }` and sets the JWT cookie on success
- [x] The JWT is stored in an HttpOnly, SameSite=Strict cookie — never exposed to JavaScript

## Notes

- Spec: `docs/requirements.md` §5 (FR-AUTH-05 to FR-AUTH-10)
- Backend agent owns the endpoint and lockout logic (ASP.NET Core Identity `SignInManager`)
- Frontend agent owns the sign-in form UI
- Depends on story-001 (registration) for test accounts
