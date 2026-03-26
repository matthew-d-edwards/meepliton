---
id: story-031
title: Admin portal — user management, room management, and log viewer
status: backlog
created: 2026-03-26
---

## What

A protected admin section at `/admin` lets trusted users (the owner group) view all users, manage rooms, and read recent application logs from a browser without opening Azure Portal or touching the database directly.

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

**What:** New endpoints at `GET /api/admin/users`, `POST /api/admin/users/{userId}/send-password-reset`, `POST /api/admin/users/{userId}/unlock`, `DELETE /api/admin/users/{userId}`, `POST /api/admin/users/{userId}/grant-admin`, and `POST /api/admin/users/{userId}/revoke-admin`.

**Acceptance criteria:**
- [ ] `GET /api/admin/users` returns paginated users with: `id`, `displayName`, `email`, `emailConfirmed`, `createdAt`, `lastSeenAt`, `loginMethods`, `isLockedOut`, `lockoutEnd`, `isAdmin`
- [ ] Supports `search` (display name or email prefix), `page` (default 1), `pageSize` (default 25, max 100)
- [ ] `POST /api/admin/users/{userId}/send-password-reset` calls `UserManager.GeneratePasswordResetTokenAsync`, constructs the reset link, and dispatches via `IEmailSender`. Returns `204`. Returns `400` if user has no password login method (Google-only). Returns `404` if user not found.
- [ ] `POST /api/admin/users/{userId}/unlock` calls `UserManager.SetLockoutEndDateAsync(user, null)` and resets `AccessFailedCount` to 0. Returns `204`. Returns `404` if user not found.
- [ ] `DELETE /api/admin/users/{userId}` hard-deletes the user. Returns `400` if `userId == currentUserId`. Returns `404` if user not found. Deletion cascade: (1) delete all rooms where the user is the host (cascade removes their `room_players` and `action_log` rows); (2) delete the user's `room_players` rows in non-hosted rooms; (3) delete the Identity user record.
- [ ] `POST /api/admin/users/{userId}/grant-admin` adds user to the `Admin` role via `UserManager.AddToRoleAsync`. Returns `204`. Returns `400` if user is already an Admin or if `userId == currentUserId`. Returns `404` if user not found.
- [ ] `POST /api/admin/users/{userId}/revoke-admin` removes user from the `Admin` role via `UserManager.RemoveFromRoleAsync`. Returns `204`. Returns `400` if `userId == currentUserId`. Returns `404` if user not found.
- [ ] All six endpoints require the `AdminOnly` policy

**Agent:** backend

---

### story-031c: Admin user list UI (frontend)

**What:** The `/admin/users` page shows all users in a table with actions.

**Acceptance criteria:**
- [ ] Table shows: display name, email, confirmed status, login methods (icons or text), last seen, lockout status, and an "Admin" badge for admin users
- [ ] "Send reset email" button per row — disabled and visually indicated as unavailable for Google-only users
- [ ] "Unlock" button per row — disabled unless `isLockedOut` is true
- [ ] "Delete user" button per row — opens a confirmation dialog naming the user's display name and email before calling `DELETE /api/admin/users/{userId}`. Disabled for the current user's own row.
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

### story-031f: In-process ring buffer log provider (backend)

**What:** A custom `ILoggerProvider` that retains the last 1000 log entries in memory, accessible via `GET /api/admin/logs`.

**Acceptance criteria:**
- [ ] `RingBufferLoggerProvider` implements `ILoggerProvider` and holds a `ConcurrentQueue<LogEntry>` capped at 1000 entries (oldest entry dequeued when limit is exceeded)
- [ ] `RingBufferLogger` implements `ILogger`; each `Log` call enqueues a `LogEntry` with: `timestamp`, `level`, `message`, `category`, `eventId` (nullable), `exception` (nullable, as string)
- [ ] The provider is registered as a singleton in `Program.cs` via `builder.Logging.AddProvider(...)` before `builder.Build()`
- [ ] The singleton is also registered in DI as `RingBufferLoggerProvider` so `AdminEndpoints` can inject it
- [ ] `GET /api/admin/logs` reads the current buffer and returns entries; supports `level` (minimum severity, default `Information`), `limit` (default 200, max 1000), `category` (prefix filter)
- [ ] Entries returned newest-first
- [ ] Endpoint requires `AdminOnly` policy
- [ ] Entries are lost on process restart — documented behaviour, not a bug

**Agent:** backend

---

### story-031g: Admin log viewer UI (frontend)

**What:** The `/admin/logs` page shows a scrollable, colour-coded log stream.

**Acceptance criteria:**
- [ ] Scrollable list of log entries showing: timestamp (local time), level badge, category, message
- [ ] Level colour coding: Debug = muted, Information = neutral, Warning = amber, Error = red, Critical = red + bold
- [ ] Exception text (if present) is shown in a collapsed `<details>` element below the message
- [ ] Filter controls: minimum level selector, category text input — applied on "Refresh" click
- [ ] "Refresh" button fetches the latest entries from the API
- [ ] Last-fetched timestamp is shown near the Refresh button
- [ ] On mobile, long messages wrap; the row is not clipped

**Agent:** frontend

---

### story-031h: Admin portal navigation shell (frontend)

**What:** The admin section has its own layout with a three-tab nav (Users, Rooms, Logs) and a breadcrumb back to Lobby.

**Acceptance criteria:**
- [ ] `/admin` redirects to `/admin/users`
- [ ] Persistent tab bar: Users, Rooms, Logs — active tab is visually distinct
- [ ] "Back to Lobby" link in the header or nav
- [ ] On mobile, the tab bar fits within 375px without overflow
- [ ] The admin shell re-uses the existing `AppShell` wrapper (same header, theme toggle, sign-out) — no bespoke chrome

**Agent:** frontend

---

## Acceptance criteria (overall portal)

- [ ] All sub-stories 031a–031h are complete
- [ ] architect review has run and all "Must fix" items are resolved
- [ ] tester has written integration tests for the three admin endpoint groups (users, rooms, logs)
- [ ] ally has reviewed the admin UI and all "Must fix" items are resolved
- [ ] docs has confirmed no user-facing copy needs updating (admin portal is internal; no public-facing strings change)
- [ ] `ADMIN_SEED_EMAIL` behaviour is verified: setting the env var grants the Admin role on startup; omitting it leaves no user assigned (idempotent either way)

---

## Notes

**Resolved open questions:**

- OQ-ADMIN-01 (resolved 2026-03-26): "Force reset" means password reset email — the spec's original interpretation is correct. Account deletion is separately supported via `DELETE /api/admin/users/{userId}` (see story-031b).
- OQ-ADMIN-03 (resolved 2026-03-26): The first Admin role is granted via the `ADMIN_SEED_EMAIL` environment variable. If set at startup, `AdminRoleSeeder` finds or creates the user with that email and assigns them the Admin role. The seed is idempotent. No separate CLI command or SQL runbook is required.

**Sub-story ordering:**

- 031a (role gating) must be done before any other sub-story is started, because it establishes the auth policy.
- 031b, 031d, 031f (backend) can run in parallel after 031a is done.
- 031c, 031e, 031g (frontend) can run in parallel with their corresponding backend story once 031a is done, but the frontend stories cannot be fully tested until their backend counterparts are complete.
- 031h (nav shell) can be done at any point after 031a.

**ID type note:** `Room.Id` in C# is `string` (GUID as text), but the DB column is `UUID`. The admin delete endpoint should accept the GUID string and convert for the query — consistent with how `RoomEndpoints.cs` already does it.

**Connected count:** The `room_players` table has a `connected` boolean column. `GET /api/admin/rooms` should return both `playerCount` (all seats) and `connectedCount` (seats where `connected = true`) to expose live connection state.

- Branch: (link once created)
- PR: (link once opened)
