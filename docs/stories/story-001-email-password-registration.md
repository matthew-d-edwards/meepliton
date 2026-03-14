---
id: story-001
title: User can register with email and password
status: refined
created: 2026-03-14
---

## What

A new user can create an account by providing their email address, a password, and a display name, then confirm their email before signing in.

## Why

Email/password registration is the baseline auth path for users who don't have or don't want to use Google.

## Acceptance criteria

- [ ] Given a valid email + password (≥8 chars) + display name, when the user submits the registration form, then a confirmation email is sent and the user sees a "check your email" message
- [ ] Given an email that is already registered, when the user submits, then they receive an error message (without revealing whether the account exists — show generic "if this email is not registered, you will receive a confirmation link")
- [ ] Given a user who has not confirmed their email, when they attempt to sign in, then they are told to check their email and given a resend link
- [ ] Given a valid confirmation link, when the user clicks it, then their account is confirmed and they are signed in
- [ ] Given an expired confirmation link, when the user clicks it, then they are told it has expired and offered a resend option
- [ ] `POST /api/auth/register` accepts `{ email, password, displayName }` and returns 204 on success
- [ ] Password is stored as a bcrypt hash — never plaintext

## Notes

- Spec: `docs/requirements.md` §5 (FR-AUTH-01 to FR-AUTH-04, FR-AUTH-12 to FR-AUTH-17)
- Backend agent owns the API endpoint and Identity wiring
- Frontend agent owns the registration form UI
- Email delivery via `IEmailSender` (SendGrid or SMTP — see OQ-08 in requirements)
- Owner action required: choose and configure email provider before this can be fully tested end-to-end (see `docs/owner/TODO.md`)
