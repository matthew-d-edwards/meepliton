## Feature: Admin Portal

### Summary

A protected admin section at `/admin` giving trusted users (the owner group) a single place to inspect and operate the running platform: view all registered users, manage rooms and games, and surface structured application logs for troubleshooting. The portal is gated behind an ASP.NET Core Identity role (`Admin`) so it cannot be reached by regular players and is never reachable by unauthenticated requests.

This is an internal operations tool, not a player-facing feature. The scope is deliberately narrow: the platform is a small friends group, so the portal does not need bulk actions, reporting dashboards, or audit logging beyond what the existing `action_log` already provides.

---

### Clarifications and design decisions recorded here

#### What does "force reset a user" mean?

The request was ambiguous. Three plausible interpretations existed:

1. **Password reset** — invalidate the user's current password and send a reset email (what "reset" normally means in Identity).
2. **Game state reset** — clear a player's in-progress room state, effectively removing them from a room.
3. **Account reset** — delete or anonymise an account.

**Decision for this spec:** "Force reset" means **sending an admin-triggered password reset email** — i.e., the admin calls `UserManager.GeneratePasswordResetTokenAsync` and dispatches the reset email on behalf of the user. This is the safest interpretation because:
- It uses existing Identity machinery with no new schema.
- It is reversible (user still exists, they just get a new password link).
- It is the meaning most operators reach for when a user is locked out or reports login trouble.
- Account deletion is now separately supported in this spec via `DELETE /api/admin/users/{userId}` (see acceptance criteria and data model sections).
- Game state reset is already possible via the room delete endpoint; exposing it in admin is covered separately in the game management section.

---

### User stories

- As an admin, I want to see all registered users so I can answer "who is on the platform" quickly.
- As an admin, I want to send a forced password reset email to a user so I can unblock a locked-out friend without touching the database manually.
- As an admin, I want to see all rooms (active, in-progress, finished) so I can understand what is happening on the platform right now.
- As an admin, I want to delete any room so I can clean up stale or broken sessions.
- As an admin, I want to see recent structured application logs so I can diagnose errors without opening Azure Portal every time.
- As a developer, I want the admin role to be assignable via a seed script or CLI command so the first admin can be granted access without a chicken-and-egg problem.

---

### Acceptance criteria

#### Role gating

- [ ] A new `Admin` Identity role exists; seeds on first startup if it does not exist already.
- [ ] All admin API endpoints return `403 Forbidden` for authenticated non-admin users.
- [ ] All admin API endpoints return `401 Unauthorized` for unauthenticated requests.
- [ ] The `/admin` frontend route redirects non-admins to `/lobby` with a toast message.
- [ ] If the `ADMIN_SEED_EMAIL` environment variable (or equivalent app setting) is set at startup, the `AdminRoleSeeder` finds or creates a user with that email and assigns them the `Admin` role. This is idempotent — re-running with the same email does nothing if the role is already assigned.
- [ ] If `ADMIN_SEED_EMAIL` is not set, the seeder only ensures the `Admin` role exists and does not assign it to anyone.

#### User management

- [ ] `GET /api/admin/users` returns a paginated list of all users; each entry includes: `id`, `displayName`, `email`, `emailConfirmed`, `createdAt`, `lastSeenAt`, `loginMethods` (`google`, `password`, or both), `isLockedOut`, `lockoutEnd`.
- [ ] `GET /api/admin/users` supports query parameters: `search` (matches display name or email prefix), `page` (1-based, default 1), `pageSize` (default 25, max 100).
- [ ] `POST /api/admin/users/{userId}/send-password-reset` generates a password reset token and sends the reset email to the user's address using the existing `IEmailSender`. Returns `204` on success. Returns `400` if the user has no email or has no password login method (Google-only accounts with no password cannot use this flow). Returns `404` if the user does not exist.
- [ ] `POST /api/admin/users/{userId}/unlock` clears an active lockout (`UserManager.SetLockoutEndDateAsync(user, null)`) and resets the access failed count. Returns `204`. Returns `404` if the user does not exist.
- [ ] `DELETE /api/admin/users/{userId}` hard-deletes the user account. Cascade-deletes their `room_players` rows and any rooms where they are the host (and the associated `room_players` and `action_log` rows for those rooms). Returns `204`. Returns `400` if `userId == currentUserId` (an admin cannot delete their own account). Returns `404` if the user does not exist.
- [ ] `POST /api/admin/users/{userId}/grant-admin` adds the user to the `Admin` role. Returns `204`. Returns `400` if the user is already an Admin, or if `userId == currentUserId`. Returns `404` if the user does not exist.
- [ ] `POST /api/admin/users/{userId}/revoke-admin` removes the user from the `Admin` role. Returns `204`. Returns `400` if `userId == currentUserId`. Returns `404` if the user does not exist.
- [ ] The frontend admin user list shows a row per user with display name, email, confirmed status, login methods, last seen, and lockout status.
- [ ] Each user row has action buttons: "Send reset email" (disabled for Google-only), "Unlock" (disabled unless locked out), "Delete user", and either "Grant admin" or "Revoke admin" depending on the user's current role.
- [ ] The current user's "Grant admin" / "Revoke admin" buttons are disabled (an admin cannot change their own role).
- [ ] Admin users display an "Admin" badge in their row.
- [ ] The "Delete user" button opens a confirmation dialog before calling the delete endpoint. The dialog names the user's display name and email to prevent accidental deletion.
- [ ] An admin cannot delete their own account — the delete button is disabled for the current user's row.

#### Room/game management

- [ ] `GET /api/admin/rooms` returns a paginated list of all rooms; each entry includes: `id`, `joinCode`, `gameId`, `gameName`, `hostId`, `hostDisplayName`, `status`, `playerCount`, `connectedCount`, `createdAt`, `updatedAt`, `expiresAt`. (`connectedCount` is the number of `room_players` rows where `connected = true`.)
- [ ] `GET /api/admin/rooms` supports query parameters: `status` (filter by room status; comma-separated values allowed, e.g. `waiting,in_progress`), `gameId`, `page`, `pageSize` (same defaults as users).
- [ ] `DELETE /api/admin/rooms/{roomId}` hard-deletes the room and all `room_players` and `action_log` rows (cascade). Returns `204`. Returns `404` if not found.
- [ ] The frontend admin rooms list shows a row per room with join code, game name, status badge, host name, player count, and created time.
- [ ] Each room row has a "Delete" button that opens a confirmation dialog before calling the delete endpoint.

#### Log viewer

- [ ] `GET /api/admin/logs` returns the most recent N structured log entries emitted by the API process. Default N = 200, max 1000.
- [ ] Each log entry includes: `timestamp`, `level` (`Debug`, `Information`, `Warning`, `Error`, `Critical`), `message`, `category` (logger name), `eventId` (if present), `exception` (if present, as a string).
- [ ] The log viewer is backed by an **in-process ring buffer** (`ILoggerProvider`) that retains the last 1000 log entries in memory. No external dependency (no Application Insights, no file sink). Entries are lost on process restart — this is acceptable for a hobby project.
- [ ] `GET /api/admin/logs` supports query parameters: `level` (minimum severity to include; default `Information`), `limit` (default 200, max 1000), `category` (filter by logger category prefix).
- [ ] The frontend log viewer shows a scrollable list of log entries, colour-coded by level (info = neutral, warning = amber, error/critical = red).
- [ ] The frontend log viewer has a "Refresh" button and shows the time of the last fetch.

#### Frontend routing

- [ ] The admin portal is a separate top-level route (`/admin/*`) nested inside the existing React Router shell.
- [ ] The admin nav has three tabs: Users, Rooms, Logs.
- [ ] On mobile (375px viewport), the admin tables scroll horizontally; no information is hidden by overflow clipping.

---

### API changes

All new endpoints live under `/api/admin` and require the `Admin` role.

```
GET    /api/admin/users                               → AdminUserListDto (paginated)
POST   /api/admin/users/{userId}/send-password-reset  → 204
POST   /api/admin/users/{userId}/unlock               → 204
DELETE /api/admin/users/{userId}                      → 204
POST   /api/admin/users/{userId}/grant-admin          → 204
POST   /api/admin/users/{userId}/revoke-admin         → 204

GET    /api/admin/rooms                               → AdminRoomListDto (paginated)
DELETE /api/admin/rooms/{roomId}                      → 204

GET    /api/admin/logs                                → AdminLogEntryDto[]
```

New authorization policy in `Program.cs`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});
```

New endpoint file: `src/Meepliton.Api/Endpoints/AdminEndpoints.cs`

---

### Data model changes

No new tables. The `roles` and `user_roles` tables are already created by Identity migrations (noted in requirements §9.3 as "created by migrations, unused in v1"). The Admin role is created at startup; the `user_roles` entries are added or removed via `UserManager.AddToRoleAsync` / `UserManager.RemoveFromRoleAsync`.

**User deletion cascade:** Deleting a user via `DELETE /api/admin/users/{userId}` must:
1. Find all rooms where the user is the host and delete those rooms (which cascade-deletes their `room_players` and `action_log` rows via ON DELETE CASCADE).
2. Delete the user's `room_players` rows in rooms they did not host.
3. Delete the ASP.NET Core Identity user record (which cascade-deletes `user_roles`, `user_claims`, `user_logins`, `user_tokens` via Identity's FK setup).

This is implemented in the endpoint handler, not as a DB-level cascade on the Identity user table, because the host-room deletion requires application-level logic.

**Room ID type note:** `Room.Id` in the C# model is `string` (GUID as text), matching ASP.NET Core Identity's convention. The PostgreSQL column is `UUID`. Admin endpoints accept the GUID string and EF Core handles the conversion — consistent with how `RoomEndpoints.cs` currently handles room lookups.

No EF migration is required because the schema already exists. The only startup change is seeding the `Admin` role if it does not exist — this goes in a new `AdminRoleSeeder` service that runs from `Program.cs` after migrations, similar to `DevSeeder`.

---

### In-process log ring buffer

The log viewer does not require Application Insights or any Azure service. It uses a custom `ILoggerProvider`:

```
src/Meepliton.Api/Services/RingBufferLoggerProvider.cs
src/Meepliton.Api/Services/RingBufferLogger.cs
```

The provider holds a `ConcurrentQueue<LogEntry>` capped at 1000 entries. When the queue exceeds 1000, the oldest entry is dequeued before adding the new one. The provider is registered as a singleton in `Program.cs` and is also injected into `AdminEndpoints` to read the current buffer.

This keeps the implementation entirely in-process — no additional Azure resources, no cost increase, no configuration secrets needed.

---

### Out of scope

- Account anonymisation / soft-delete (hard delete only; see data model section above).
- Game state reset per-player (the room delete endpoint already covers the room-level case).
- Bulk actions (delete all rooms older than N days, etc.).
- Admin audit log (recording who performed which admin action).
- Real-time log streaming (the refresh button is sufficient for a hobby platform).
- Multi-environment (staging vs production) — there is one environment.
- Application Insights integration — intentionally deferred (OQ in requirements §17).

---

### Open questions

| # | Question | Impact | Status |
|---|---|---|---|
| OQ-ADMIN-01 | Was "force reset" meant to be account deletion rather than password reset email? If so, this spec needs a separate story with a confirmed deletion strategy (hard delete vs soft delete, referential integrity impact). | High — changes the user management story entirely | **Resolved 2026-03-26:** "Force reset" means password reset email. The spec's interpretation is correct. Account deletion is separately supported via `DELETE /api/admin/users/{userId}` (added as a new requirement). |
| OQ-ADMIN-02 | Should the admin portal be reachable in production only (not dev), or in all environments? Recommended: available in all environments, but the admin role seed is dev-only. | Low — cosmetic | Proposed default accepted (no owner decision needed) |
| OQ-ADMIN-03 | Who should receive the first Admin role in production? There is no UI for this. The proposed solution is a `dotnet run` CLI argument or a one-time SQL snippet. This needs a documented runbook. | Medium — blocks going live | **Resolved 2026-03-26:** Seed from `ADMIN_SEED_EMAIL` environment variable. If present at startup, the seeder finds or creates the user with that email and assigns the Admin role. Idempotent. No separate CLI or SQL runbook required. |

### Story

`docs/stories/story-031-admin-portal.md`
