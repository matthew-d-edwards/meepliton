---
id: story-006
title: Auth screens — registration, sign-in, and password reset pages
status: done
created: 2026-03-14
---

## What

The frontend has fully functional, mobile-friendly auth screens: sign-in, registration, forgot password, and reset password.

## Why

The backend auth endpoints (stories 001–003) need corresponding UI before any user can actually sign in.

## Acceptance criteria

- [x] Sign-in page (`/sign-in`): email + password fields, "Sign in with Google" button, link to registration, link to "forgot password"
- [x] Registration page (`/register`): display name + email + password fields, "Create account" button, link back to sign-in
- [x] Forgot password page (`/forgot-password`): email field, submit shows "check your email" confirmation regardless of outcome
- [x] Reset password page (`/reset-password?token=…&email=…`): new password + confirm fields, success redirects to sign-in — implemented as `?userId=…&token=…` (not `?token=…&email=…`)
- [x] All forms show inline validation errors (empty fields, password too short, passwords don't match)
- [x] All forms show a loading state while the request is in flight
- [x] All pages work at 375px viewport with 44px minimum tap targets
- [x] All pages use the Blade Runner design system (dark surfaces, correct fonts, token-based colours)
- [x] Unauthenticated routes redirect to `/sign-in`; authenticated users visiting `/sign-in` redirect to `/lobby`

## Notes

- Frontend agent owns all pages; UX agent should review before merge
- Depends on story-001, 002, 003 API endpoints existing
- Run `/ui-design auth screens` before starting implementation to align on layout
- CSS: global class names (platform chrome), all values via tokens
- ConfirmEmailPage (`/confirm-email`) also implemented — handles the email confirmation link from the backend
- Branch: `claude/consolidate-agent-branches-SCFzb`
