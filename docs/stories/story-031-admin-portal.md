---
id: story-031
title: Admin portal — user management and room management
status: backlog
created: 2026-03-26
---

## What

A protected admin section at `/admin` lets trusted users (the owner group) view all users with management actions (unlock, grant/revoke admin, send password reset) and manage rooms (list, delete), without opening Azure Portal or touching the database directly. A log viewer is excluded from v1; use `az containerapp logs show` in the interim (see §12.3a of requirements.md).

## Why

As the platform grows to more games and more sessions, operational visibility (who is using the platform, which rooms are stuck, what errors occurred) becomes necessary to keep things running smoothly.

## Spec

`docs/specs/admin-portal.md`

---

## Stories in this backlog

This planning story tracks the full admin portal feature. It will be broken into the following implementation stories once the owner has answered the open questions (see Notes below). The breakdown is:

### story-031a: Admin role gating (backend + frontend)

**What:** The `Admin` Identity role is seeded at startup, an `AdminOnly` authorization policy is registered, and the frontend `/admin` route redirects non-admins to `/lobby`.

**Acceptance criteria:**
- [ ] `Admin` role is created on startup via `AdminRoleSeeder` if it does not already exist (always runs, both dev and prod)
- [ ] If the `ADMIN_SEED_EMAIL` environment variable (or equivalent app setting) is set, `AdminRoleSeeder` finds or creates a user with that email and assigns them the `Admin` role. If the variable is not set, no user is assigned automatically.
- [ ] The seed is idempotent — running it multiple times with the same email does nothing if the role is already assigned.
- [ ] `builder.Services.AddAuthorization` includes a policy `"AdminOnly"` requiring the `Admin` role
- [ ] Any request to `/api/admin/*` from a non-admin authenticated user returns `403`
- [ ] Any request to `/api/admin/*` from an unauthenticated user returns `401`
- [ ] The React route `/admin` and `/admin/*` is protected: non-admins are redirected to `/lobby` with a visible message

**Agent:** backend + frontend (parallel)

---

### story-031b: Admin user list and actions (backend)

**What:** New endpoints at `GET /api/admin/users`, `POST /api/admin/users/{userId}/send-password-reset`, `POST /api/admin/users/{userId}/unlock`, `POST /api/admin/users/{userId}/grant-admin`, and `POST /api/admin/users/{userId}/revoke-admin`.

**Note:** User hard delete is excluded from v1. Ghost rows in game tables are expected per ADR-003. Deletion is deferred to a future story.

**Acceptance criteria:**
- [ ] `GET /api/admin/users` returns paginated users with: `id`, `displayName`, `email`, `emailConfirmed`, `createdAt`, `lastSeenAt`, `loginMethods`, `isLockedOut`, `lockoutEnd`, `isAdmin`
- [ ] `isAdmin` is populated via a single join against `user_roles`/`roles` tables — no N+1 `IsInRoleAsync` calls
- [ ] Supports `search` (display name or email prefix), `page` (default 1), `pageSize` (default 25, max 100)
- [ ] `POST /api/admin/users/{userId}/send-password-reset` calls `UserManager.GeneratePasswordResetTokenAsync`, constructs the reset link, and dispatches via `IEmailSender`. Returns `204`. Returns `400` if user has no password login method (Google-only). Returns `404` if user not found.
- [ ] `POST /api/admin/users/{userId}/unlock` calls `UserManager.SetLockoutEndDateAsync(user, null)` and resets `AccessFailedCount` to 0. Returns `204`. Returns `404` if user not found.
- [ ] `POST /api/admin/users/{userId}/grant-admin` adds user to the `Admin` role via `UserManager.AddToRoleAsync`. Returns `204`. Returns `400` if user is already an Admin or if `userId == currentUserId`. Returns `404` if user not found.
- [ ] `POST /api/admin/users/{userId}/revoke-admin` removes user from the `Admin` role via `UserManager.RemoveFromRoleAsync`. Returns `204`. Returns `400` if `userId == currentUserId`. Returns `404` if user not found.
- [ ] All five endpoints require the `AdminOnly` policy

**Agent:** backend

---

### story-031c: Admin user list UI (frontend)

**What:** The `/admin/users` page shows all users in a table with actions.

**Acceptance criteria:**
- [ ] Table shows: display name, email, confirmed status, login methods (icons or text), last seen, lockout status, and an "Admin" badge for admin users
- [ ] "Send reset email" button per row — disabled and visually indicated as unavailable for Google-only users
- [ ] "Unlock" button per row — disabled unless `isLockedOut` is true
- [ ] "Grant admin" or "Revoke admin" button per row (shows "Grant admin" if the user is not an Admin, "Revoke admin" if they are). Both are disabled for the current user's own row. After the API call, the badge and button label update to reflect the new state.
- [ ] All action buttons show a success or error toast after the API call
- [ ] Pagination controls (previous / next) visible when there are multiple pages
- [ ] Search input filters by display name or email (calls API on submit, not live)
- [ ] On mobile (375px viewport), the table scrolls horizontally without clipping

**Agent:** frontend

---

### story-031d: Admin room list and delete (backend)

**What:** New endpoints at `GET /api/admin/rooms` and `DELETE /api/admin/rooms/{roomId}`.

**Acceptance criteria:**
- [ ] `GET /api/admin/rooms` returns paginated rooms with: `id`, `joinCode`, `gameId`, `gameName`, `hostId`, `hostDisplayName`, `status`, `playerCount`, `connectedCount`, `createdAt`, `updatedAt`, `expiresAt`
- [ ] Supports `status` (comma-separated filter, e.g. `waiting,in_progress`), `gameId`, `page`, `pageSize`
- [ ] `DELETE /api/admin/rooms/{roomId}` removes the room row; `room_players` and `action_log` rows are removed via ON DELETE CASCADE. Returns `204`. Returns `404` if not found.
- [ ] Both endpoints require the `AdminOnly` policy

**Note on `connectedCount`:** The `room_players` table has a `connected` column (boolean). The admin room list should surface both `playerCount` (total seats) and `connectedCount` (currently connected) so the admin can see at a glance which rooms have active connections.

**Agent:** backend

---

### story-031e: Admin room list UI (frontend)

**What:** The `/admin/rooms` page shows all rooms in a table with a delete action.

**Acceptance criteria:**
- [ ] Table shows: join code, game name, status badge (colour-coded), host display name, player count / connected count, created time
- [ ] Status badges: Waiting = neutral, InProgress = green, Finished = grey, Closed = muted
- [ ] Filter controls: status multi-select and game dropdown — applied on submit
- [ ] "Delete" button per row — opens a confirmation dialog before calling the API
- [ ] Confirmation dialog text names the room join code and game to prevent mis-deletion
- [ ] On deletion, the row is removed from the list without a full page reload
- [ ] Pagination controls when there are multiple pages
- [ ] On mobile, table scrolls horizontally without clipping

**Agent:** frontend

---

### story-031f: Admin portal navigation shell (frontend)

**What:** The admin section has its own layout with a two-tab nav (Users, Rooms) and a link back to Lobby. The Logs tab is excluded from v1.

**Acceptance criteria:**
- [ ] `/admin` redirects to `/admin/users`
- [ ] Persistent tab bar: Users, Rooms — active tab is visually distinct
- [ ] "Back to Lobby" link in the header or nav
- [ ] On mobile, the tab bar fits within 375px without overflow
- [ ] The admin shell re-uses the existing `AppShell` wrapper (same header, theme toggle, sign-out) — no bespoke chrome

**Agent:** frontend

---

## Acceptance criteria (overall portal)

- [ ] All sub-stories 031a–031f are complete
- [ ] architect review has run and all "Must fix" items are resolved
- [ ] tester has written integration tests for the two admin endpoint groups (users, rooms)
- [ ] ally has reviewed the admin UI and all "Must fix" items are resolved
- [ ] docs has confirmed no user-facing copy needs updating (admin portal is internal; no public-facing strings change)
- [ ] `ADMIN_SEED_EMAIL` behaviour is verified: setting the env var grants the Admin role on startup; omitting it leaves no user assigned (idempotent either way)

---

## Notes

**Resolved open questions:**

- OQ-ADMIN-01 (resolved 2026-03-26): "Force reset" means password reset email. Account deletion is excluded from v1 — the meaningful admin actions (unlock, grant/revoke admin, password reset) cover the stated need without the application-level cascade complexity hard delete requires.
- OQ-ADMIN-03 (resolved 2026-03-26): The first Admin role is granted via the `ADMIN_SEED_EMAIL` environment variable. If set at startup, `AdminRoleSeeder` finds or creates the user with that email and assigns them the Admin role. The seed is idempotent. No separate CLI command or SQL runbook is required.

**Sub-story ordering:**

- 031a (role gating) must be done before any other sub-story is started, because it establishes the auth policy.
- 031b and 031d (backend) can run in parallel after 031a is done.
- 031c and 031e (frontend) can run in parallel with their corresponding backend story once 031a is done, but the frontend stories cannot be fully tested until their backend counterparts are complete.
- 031f (nav shell) can be done at any point after 031a.

**ID type note:** `Room.Id` in C# is `string` (GUID as text), but the DB column is `UUID`. The admin delete endpoint should accept the GUID string and convert for the query — consistent with how `RoomEndpoints.cs` already does it.

**Connected count:** The `room_players` table has a `connected` boolean column. `GET /api/admin/rooms` should return both `playerCount` (all seats) and `connectedCount` (seats where `connected = true`) to expose live connection state.

- Branch: (link once created)
- PR: (link once opened)
