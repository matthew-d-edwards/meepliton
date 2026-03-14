---
id: story-006
title: Auth screens — registration, sign-in, and password reset pages
status: refined
created: 2026-03-14
---

## What

The frontend has fully functional, mobile-friendly auth screens: sign-in, registration, forgot password, and reset password.

## Why

The backend auth endpoints (stories 001–003) need corresponding UI before any user can actually sign in.

## Acceptance criteria

- [ ] Sign-in page (`/sign-in`): email + password fields, "Sign in with Google" button, link to registration, link to "forgot password"
- [ ] Registration page (`/register`): display name + email + password fields, "Create account" button, link back to sign-in
- [ ] Forgot password page (`/forgot-password`): email field, submit shows "check your email" confirmation regardless of outcome
- [ ] Reset password page (`/reset-password?token=…&email=…`): new password + confirm fields, success redirects to sign-in
- [ ] All forms show inline validation errors (empty fields, password too short, passwords don't match)
- [ ] All forms show a loading state while the request is in flight
- [ ] All pages work at 375px viewport with 44px minimum tap targets
- [ ] All pages use the Blade Runner design system (dark surfaces, correct fonts, token-based colours)
- [ ] Unauthenticated routes redirect to `/sign-in`; authenticated users visiting `/sign-in` redirect to `/lobby`

## Notes

- Frontend agent owns all pages; UX agent should review before merge
- Depends on story-001, 002, 003 API endpoints existing
- Run `/ui-design auth screens` before starting implementation to align on layout
- CSS: global class names (platform chrome), all values via tokens
