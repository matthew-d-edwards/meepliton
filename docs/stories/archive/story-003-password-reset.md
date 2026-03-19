---
id: story-003
title: User can reset a forgotten password
status: done
created: 2026-03-14
---

## What

A user who has forgotten their password can request a reset link by email, and use that link to set a new password.

## Why

Without password reset, locked-out users have no self-service recovery path — they'd need manual intervention.

## Acceptance criteria

- [ ] Given any email address, when the user submits "forgot password", then the API always returns 204 regardless of whether the email exists (prevents user enumeration)
- [ ] Given a registered email, then a reset link is sent within 60 seconds
- [ ] Given a valid, unexpired reset link, when the user submits a new password (≥8 chars), then their password is updated and they are redirected to sign in
- [ ] Given an expired reset link (>1 hour), when the user submits, then they receive an "expired link" message and are offered a new request
- [ ] Given a reset link that has already been used, when the user visits it again, then they receive an "invalid link" message
- [ ] `POST /api/auth/forgot-password` accepts `{ email }` — always 204
- [ ] `POST /api/auth/reset-password` accepts `{ userId, token, newPassword }` — 204 on success, 400 on invalid/expired

## Notes

- Spec: `docs/requirements.md` §5 (FR-AUTH-11), §11 (API endpoints)
- Backend agent owns the endpoints (Identity `UserManager.GeneratePasswordResetTokenAsync`)
- Frontend agent owns the "forgot password" and "reset password" form pages
- Depends on transactional email being configured (see OQ-08 and `docs/owner/TODO.md`)
