---
id: story-001
title: User can register with email and password
status: done
created: 2026-03-14
---

## What

A new user can create an account using their email address, a password, and a display name. A confirmation email is sent immediately, and the account is inactive until the email is confirmed.

## Why

Email/password registration is the fallback authentication path for users who do not use Google. It unlocks the full platform for everyone in the group.

## Acceptance criteria

- [x] `POST /api/auth/register` accepts `{ email, password, displayName }` and returns 201
- [x] Password is validated: minimum 8 characters, at least one uppercase letter, one digit
- [x] Password is stored as a hash (ASP.NET Core Identity PBKDF2 — never raw)
- [x] A confirmation email is sent immediately after successful registration via `IEmailSender<ApplicationUser>`
- [x] The confirmation email contains a link to `{Frontend:BaseUrl}/confirm-email?userId={id}&token={encodedToken}` with the token Base64Url-encoded
- [x] If the email address is already registered, the endpoint returns a generic validation error — it does not reveal that the address is in use
- [x] When `SENDGRID_API_KEY` is set in configuration, emails are delivered via SendGrid
- [x] When `SENDGRID_API_KEY` is absent (local dev / CI), a `LoggingEmailSender` logs the link at Information level — no credentials required
- [x] `POST /api/auth/confirm-email` accepts `{ userId, token }` and activates the account (returns 204)

## Notes

- Branch: `claude/story-001-registration-GAnxk`
- Spec: no spec written (story was clear enough to implement directly)
- `Frontend:BaseUrl` defaults to `https://meepliton.com` when not configured; override to `http://localhost:5173` in local dev via environment variable or user secrets
- Requirements §11 specifies `POST /api/auth/register → 201`; the story brief said 204 — 201 was used as it matches the authoritative requirements document
- `confirm-email` token is Base64Url-encoded UTF-8; the endpoint decodes it before passing to Identity, matching the encoding applied on registration
