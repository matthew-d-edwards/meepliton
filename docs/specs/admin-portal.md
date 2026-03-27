## Feature: Admin Portal

### Summary

A protected admin section at `/admin` giving trusted users (the owner group) a single place to inspect and operate the running platform: view all registered users with management actions, and manage rooms. The portal is gated behind an ASP.NET Core Identity role (`Admin`) so it cannot be reached by regular players and is never reachable by unauthenticated requests.

This is an internal operations tool, not a player-facing feature. The scope is deliberately narrow: the platform is a small friends group, so the portal does not need bulk actions, reporting dashboards, or audit logging.

**Spec revision history:**
- Round 1 (2026-03-26): Initial analyst draft
- Round 2 (2026-03-26): Architect review — objections raised
- Round 3 (2026-03-26): Final consolidated spec — objections resolved

---

### Architect objection responses (Round 3)

**Objection 1 (Critical) — Drop user delete from v1:** Accepted. The stated admin need (unblocking friends, granting access, cleaning up rooms) is fully covered by unlock, grant/revoke admin, and password reset. Hard-deleting users requires application-level cascade logic across hosted rooms, room_players, action_log, and Identity tables — meaningful complexity for a feature that has no immediate use case on a small platform. Ghost rows in game-module tables are already expected per ADR-003. User deletion is deferred to a future story.

**Objection 2 (Moderate) — Drop the Logs tab from v1:** Accepted. The original request included "operational visibility" as a motivation, and this analyst initially contested the objection. On reflection, the ring buffer is the only viable in-process approach, and it is genuinely lossy: entries disappear on every container recycle (Azure Container Apps on the Consumption plan recycles frequently on scale-to-zero). The result would be a log viewer that is empty after every cold start — worse than the Azure CLI alternative. The v1 Logs tab is dropped. Operational visibility for v1 is met by `az containerapp logs show --name meepliton-api --resource-group ... --follow` (documented in requirements.md §12). A v2 Logs tab backed by a persistent sink (Azure Monitor, Application Insights, or a managed Serilog sink) is noted in the roadmap.

**Objection 3 (Minor) — AdminSeeder pattern:** Accepted. The seeder follows the existing `DevSeeder` style: a registered hosted service, reads `ADMIN_SEED_EMAIL` env var, runs in all environments. If the env var is not set, or if the email is not found in the DB, the seeder logs a warning and skips — it never creates users. Idempotent.

---

### v1 scope

| Area | Included | Excluded |
|---|---|---|
| Auth | AdminSeeder (all envs), `AdminOnly` policy, JWT role claims at login | Role changes without relogin |
| Users tab | Paginated + searchable list; unlock, grant admin, revoke admin, send password reset | Hard delete, bulk actions, audit log |
| Rooms tab | Paginated list with filters; delete room with cascade | Game-state-level actions |
| Logs tab | Dropped from v1 | Ring buffer, Application Insights |
| Frontend | `/admin` route with role guard, nav shell (Users + Rooms tabs) | Logs tab UI |

---

### User stories

- As an admin, I want to see all registered users so I can answer "who is on the platform" quickly.
- As an admin, I want to send a forced password reset email to a user so I can unblock a locked-out friend without touching the database.
- As an admin, I want to unlock a locked-out account so I can restore access without a password reset.
- As an admin, I want to grant or revoke the Admin role so I can manage who has operational access.
- As an admin, I want to see all rooms (active, in-progress, finished) so I can understand what is happening right now.
- As an admin, I want to delete any room so I can clean up stale or broken sessions.
- As a developer, I want the first admin role to be assigned via an environment variable so there is no chicken-and-egg problem on first deployment.

---

### Acceptance criteria

#### Role gating

- [ ] A new `Admin` Identity role exists; the `AdminSeeder` ensures it is created on startup if it does not exist already.
- [ ] `AdminSeeder` runs in all environments (dev and production).
- [ ] If `ADMIN_SEED_EMAIL` is set and a user with that email exists in the DB, the seeder assigns them the `Admin` role. If the user is already in the role, the seeder does nothing (idempotent).
- [ ] If `ADMIN_SEED_EMAIL` is set but no user with that email exists, the seeder logs a warning and does not create a new user.
- [ ] If `ADMIN_SEED_EMAIL` is not set, the seeder only ensures the `Admin` role row exists; no user is assigned.
- [ ] `Admin` role claims are embedded in the JWT at login time — one extra `GetRolesAsync` call per login. Role changes require the user to log out and back in.
- [ ] All admin API endpoints return `403 Forbidden` for authenticated non-admin users.
- [ ] All admin API endpoints return `401 Unauthorized` for unauthenticated requests.
- [ ] The React route `/admin` and `/admin/*` redirects non-admins to `/lobby` with a visible toast message.

#### User management

- [ ] `GET /api/admin/users` returns a paginated list of all users; each entry includes: `id`, `displayName`, `email`, `emailConfirmed`, `createdAt`, `lastSeenAt`, `loginMethods` (`google`, `password`, or both), `isLockedOut`, `lockoutEnd`, `isAdmin`.
- [ ] `isAdmin` is populated via a single join query against `user_roles`/`roles` tables — no N+1 `IsInRoleAsync` calls.
- [ ] `GET /api/admin/users` supports query parameters: `search` (matches display name or email prefix, case-insensitive), `page` (1-based, default 1), `pageSize` (default 25, max 100).
- [ ] `POST /api/admin/users/{userId}/send-password-reset` generates a password reset token and sends the reset email via the existing `IEmailSender`. Returns `204` on success. Returns `400` if the user has no password login method (Google-only accounts). Returns `404` if the user does not exist.
- [ ] `POST /api/admin/users/{userId}/unlock` clears an active lockout (`UserManager.SetLockoutEndDateAsync(user, null)`) and resets the access failed count to 0. Returns `204`. Returns `404` if the user does not exist.
- [ ] `POST /api/admin/users/{userId}/grant-admin` adds the user to the `Admin` role via `UserManager.AddToRoleAsync`. Returns `204`. Returns `400` if the user is already an Admin or if `userId == currentUserId`. Returns `404` if the user does not exist.
- [ ] `POST /api/admin/users/{userId}/revoke-admin` removes the user from the `Admin` role via `UserManager.RemoveFromRoleAsync`. Returns `204`. Returns `400` if `userId == currentUserId`. Returns `404` if the user does not exist.
- [ ] The frontend admin user list shows: display name, email, confirmed status (icon), login methods (icon or text), last seen (relative time), lockout status, and an "Admin" badge for admin users.
- [ ] Each user row has action buttons: "Send reset email" (disabled for Google-only users), "Unlock" (disabled unless `isLockedOut` is true), "Grant admin" or "Revoke admin" (toggled by current state; both disabled for the current user's own row).
- [ ] All action buttons show a success or error toast after the API call.
- [ ] The "Grant admin" / "Revoke admin" button and badge update optimistically after a successful API response — no full page reload needed.
- [ ] Pagination controls (previous / next) are visible when there are multiple pages.
- [ ] Search input filters by display name or email — calls API on submit, not on every keystroke.
- [ ] On mobile (375px viewport), the user table scrolls horizontally without clipping.

#### Room management

- [ ] `GET /api/admin/rooms` returns a paginated list of all rooms; each entry includes: `id`, `joinCode`, `gameId`, `gameName`, `hostId`, `hostDisplayName`, `status`, `playerCount`, `connectedCount`, `createdAt`, `updatedAt`, `expiresAt`. (`connectedCount` is the count of `room_players` rows where `connected = true`.)
- [ ] `GET /api/admin/rooms` supports: `status` (comma-separated filter, e.g. `waiting,in_progress`), `gameId`, `page`, `pageSize` (same defaults as users).
- [ ] `DELETE /api/admin/rooms/{roomId}` hard-deletes the room and all `room_players` and `action_log` rows via ON DELETE CASCADE. Returns `204`. Returns `404` if not found.
- [ ] The frontend admin rooms list shows: join code, game name, status badge (colour-coded), host display name, player count / connected count, created time.
- [ ] Status badge colours: Waiting = neutral, InProgress = green, Finished = grey, Closed = muted.
- [ ] Filter controls: status multi-select and game dropdown — applied on submit.
- [ ] "Delete" button per row opens a confirmation dialog naming the room join code and game before calling the API.
- [ ] On successful delete, the row is removed from the list without a full page reload.
- [ ] Pagination controls when there are multiple pages.
- [ ] On mobile, the rooms table scrolls horizontally without clipping.

#### Frontend routing and nav shell

- [ ] `/admin` redirects to `/admin/users`.
- [ ] Persistent tab bar with two tabs: Users, Rooms. Active tab is visually distinct.
- [ ] "Back to Lobby" link in the header or nav.
- [ ] On mobile (375px), the tab bar fits within the viewport without overflow.
- [ ] The admin shell re-uses the existing `AppShell` wrapper (same header, theme toggle, sign-out) — no bespoke platform chrome.

---

### API changes

All new endpoints live under `/api/admin` and require the `AdminOnly` authorization policy.

```
GET    /api/admin/users                               → AdminUserListDto (paginated)
POST   /api/admin/users/{userId}/send-password-reset  → 204
POST   /api/admin/users/{userId}/unlock               → 204
POST   /api/admin/users/{userId}/grant-admin          → 204
POST   /api/admin/users/{userId}/revoke-admin         → 204

GET    /api/admin/rooms                               → AdminRoomListDto (paginated)
DELETE /api/admin/rooms/{roomId}                      → 204
```

New authorization policy registered in `Program.cs`:

```
options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
```

New endpoint file: `src/Meepliton.Api/Endpoints/AdminEndpoints.cs`

JWT role claims are added at login in `TokenService` (or equivalent auth handler) via one `GetRolesAsync` call. Role changes take effect after the user's next login.

---

### Data model changes

No new tables or migrations required.

The `roles` and `user_roles` tables are already created by Identity migrations (noted in requirements §9.3 as "created by migrations, unused in v1"). The `Admin` role row is created at startup by `AdminSeeder`. Role assignments are managed via `UserManager.AddToRoleAsync` / `RemoveFromRoleAsync`.

`LastSeenAt` already exists on `ApplicationUser` — no migration needed.

**Room ID type note:** `Room.Id` in C# is `string` (GUID as text); the PostgreSQL column is `UUID`. Admin endpoints accept the GUID string; EF Core handles the conversion — consistent with existing `RoomEndpoints.cs`.

**`isAdmin` query:** Populated via a single join against `user_roles` and `roles`, not via N+1 `IsInRoleAsync` calls.

---

### New service: AdminSeeder

Follows `DevSeeder` style: a registered hosted service (or `IHostedService`), runs on startup in all environments.

Behaviour:
1. Ensure the `Admin` role exists in the `roles` table. If not, create it.
2. If `ADMIN_SEED_EMAIL` is set in configuration:
   a. Find the user by email.
   b. If not found: log a warning at `Warning` level, skip.
   c. If found and already in `Admin` role: do nothing (idempotent).
   d. If found and not in role: add to role via `UserManager.AddToRoleAsync`.
3. If `ADMIN_SEED_EMAIL` is not set: step 2 is skipped entirely.

The seeder never creates user accounts — it only assigns roles to existing users.

---

### ADR-011: Admin access via Identity roles + JWT claims

**Status:** Accepted
**Date:** 2026-03-26

#### Context

The admin portal requires a way to gate access to a small set of trusted users. The platform already uses ASP.NET Core Identity for user management and JWTs for API authentication. The question is how and when the `Admin` role is surfaced in the token.

#### Decision

Embed role claims in the JWT at login time via a single `GetRolesAsync` call. An `AdminOnly` authorization policy (`RequireRole("Admin")`) gates all `/api/admin/*` endpoints and the frontend `/admin` route.

Role changes (grant/revoke admin) take effect after the affected user's next login. "Relogin to activate admin" is acceptable given the platform's scale and usage pattern.

#### Consequences

- One extra DB call per login (negligible cost).
- An admin whose role is revoked retains access until their current JWT expires. Acceptable: the platform is a private friends group with short session windows. If immediate revocation is needed in future, add a token blacklist or shorten JWT TTL.
- No middleware plumbing for dynamic role claims is required.

---

### Out of scope (v1)

- User hard delete (deferred — see note in objection 1 response above).
- Account anonymisation or soft delete.
- Log viewer (deferred to v2 — use `az containerapp logs show` in the interim).
- Application Insights integration (separate decision — see requirements §17).
- Bulk admin actions (delete all rooms older than N days, etc.).
- Admin audit log (recording who performed which admin action).
- Real-time log streaming.
- Game state reset per-player.

---

### Open questions

All open questions from Round 1 are resolved. No blocking questions remain.

| # | Question | Resolution |
|---|---|---|
| OQ-ADMIN-01 | "Force reset" — password reset or account deletion? | Password reset email. Account deletion dropped from v1. |
| OQ-ADMIN-02 | Admin portal available in all environments or production only? | All environments. |
| OQ-ADMIN-03 | How does the first admin get their role? | `ADMIN_SEED_EMAIL` env var at startup. Idempotent. |
| OQ-ADMIN-04 | How are role claims surfaced in JWT? | `GetRolesAsync` at login, embedded in JWT. Relogin to activate. |
| OQ-ADMIN-05 | How is `isAdmin` populated in the user list without N+1? | Single join against `user_roles`/`roles` tables. |
| OQ-ADMIN-06 | Logs tab — ring buffer or drop from v1? | Dropped from v1. Ring buffer is lossy on container recycle. |

---

### Story

`docs/stories/story-031-admin-portal.md`
