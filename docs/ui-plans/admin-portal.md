# UI Plan: Admin Portal

**Status:** Agreed
**Date:** 2026-03-26
**Authors:** ux + frontend
**Story:** story-031 (sub-stories 031a, 031c, 031e, 031g, 031h)

---

## Design intent

The admin portal is internal tooling for the owner group. It shares the same Blade Runner night city design language as the rest of the platform — dark surfaces, neon glows, gold accent — but prioritises data density and operational clarity over ambient atmosphere. Tables are tight, badges are readable at a glance, and destructive actions require explicit confirmation. The portal re-uses the existing `AppShell` header without modification; all admin-specific chrome lives below it.

---

## Breakpoint behaviour

| Breakpoint | Layout |
|---|---|
| < 700px | Single column. Tables scroll horizontally inside a `overflow-x: auto` wrapper. The per-row action buttons collapse into a bottom sheet (`AdminMobileActionSheet`) triggered by a "..." icon button on each row. Tab bar fits on one line (icon + short label). |
| 700–1100px | Full table visible. Action buttons render inline in each row. Tab bar shows full labels. |
| > 1100px | Centred max-width container (`max-width: 1200px`). Table and filter controls share a two-column layout (filters in a sidebar column, table in the main column) on the rooms and users pages. |

---

## Route structure

| Route | Component | Guard |
|---|---|---|
| `/admin` | Redirects to `/admin/users` | `adminShell()` |
| `/admin/users` | `AdminUsersPage` | `adminShell()` |
| `/admin/rooms` | `AdminRoomsPage` | `adminShell()` |
| `/admin/logs` | `AdminLogsPage` | `adminShell()` |

`adminShell()` is a helper function in `App.tsx`, mirroring the existing `shell()` pattern. It:
1. Redirects to `/sign-in?next=…` if `!user` (unauthenticated).
2. Redirects to `/lobby` if `user && !user.isAdmin` (authenticated but not admin), with no `?next=` parameter.
3. Wraps the page in `AppShell` when the user is an admin.

`AdminPage.tsx` does not handle its own auth redirect. All redirect logic lives in `App.tsx`.

---

## Components

| Component | Location | New or existing | Notes |
|---|---|---|---|
| `Badge` | `packages/ui/src/components/Badge.tsx` | New (shared) | Generic badge with `variant` prop. Replaces the originally planned `AdminBadge` and `LevelBadge`. Variants: `admin`, `level-debug`, `level-info`, `level-warning`, `level-error`, `level-critical`, `status-waiting`, `status-in-progress`, `status-finished`, `status-closed`. Exported from `packages/ui/src/index.ts`. Architect must confirm before implementation. |
| `Pagination` | `packages/ui/src/components/Pagination.tsx` | New (shared) | Generic previous/next pagination with `page`, `totalPages`, `onPageChange` props. No game-state knowledge. Used by admin users and rooms tables. Exported from `packages/ui/src/index.ts`. Architect must confirm before implementation. |
| `AdminShell` | `apps/frontend/src/platform/admin/AdminShell.tsx` | New | Three-tab nav (Users, Rooms, Logs) plus "Back to Lobby" link. Rendered below `AppShell` header. Not exported to `packages/ui/` — admin-specific. |
| `AdminUsersPage` | `apps/frontend/src/platform/admin/AdminUsersPage.tsx` | New | Users table with search, pagination, and per-row actions. |
| `AdminRoomsPage` | `apps/frontend/src/platform/admin/AdminRoomsPage.tsx` | New | Rooms table with status/game filters, pagination, and delete action. |
| `AdminLogsPage` | `apps/frontend/src/platform/admin/AdminLogsPage.tsx` | New | Log viewer with level and category filters plus Refresh button. |
| `ConfirmDialog` | `apps/frontend/src/platform/admin/ConfirmDialog.tsx` | New | Modal confirmation dialog used for user delete and room delete. Uses `useFocusTrap`. |
| `AdminMobileActionSheet` | `apps/frontend/src/platform/admin/AdminMobileActionSheet.tsx` | New | Bottom sheet for per-row actions on mobile (< 700px). Uses `useFocusTrap`. |
| `useFocusTrap` | `apps/frontend/src/platform/admin/hooks/useFocusTrap.ts` | New | Vanilla DOM hook, ~20–30 lines. Traps keyboard focus within a container ref while active. Used by `ConfirmDialog` and `AdminMobileActionSheet`. Not promoted to `packages/ui/` at this stage. |

---

## CSS approach

- Platform chrome components (`AdminShell`, `AdminMobileActionSheet`, `ConfirmDialog`) use global class names consistent with the rest of `packages/ui/src/` and the existing platform pages.
- Page-level components (`AdminUsersPage`, `AdminRoomsPage`, `AdminLogsPage`) use CSS Modules (`.module.css` files alongside each `.tsx`), consistent with the game component convention and the existing profile/lobby pages.
- Shared components in `packages/ui/src/components/` (`Badge`, `Pagination`) use global class names.
- All colours via tokens — no hex values in component CSS files.
- All spacing via `--space-*`.
- All radii via `--radius-*`.
- All fonts via `--font-*`.
- Hover/focus transitions: `200ms ease`.
- Panel/sheet animations: `320ms cubic-bezier(0.4, 0, 0.2, 1)`.

### Colour decisions

**Delete button:** `--neon-magenta` is used as border colour and text colour on a transparent background (`background: transparent; border: 1px solid var(--neon-magenta); color: var(--neon-magenta)`). On hover/focus, the background fills to `var(--neon-magenta)` at low opacity (`--accent-dim` pattern). This avoids the contrast failure that would occur with a solid `--neon-magenta` background and `--text-bright` foreground (~3.2:1, insufficient for normal text at WCAG AA).

**Log level colours:**
- Debug: `var(--text-muted)`
- Information: `var(--text-primary)`
- Warning: `var(--status-warning)` (resolves to `--accent`, gold)
- Error: `var(--status-error)`
- Critical: `var(--status-error)` with `font-weight: 700`

There is no `--status-critical` token and none will be added. CRITICAL is visually distinguished from ERROR by weight alone.

**Status badges (rooms table):**
- Waiting: `var(--text-muted)` text, `var(--edge-subtle)` border
- InProgress: `var(--status-success)` with glow
- Finished: `var(--text-muted)` text, no border
- Closed: `var(--text-muted)` text, `var(--edge-subtle)` border, reduced opacity

**Admin badge:** `var(--accent)` text, `var(--accent-dim)` background — consistent with gold-means-important rule.

### Row delete animation

On delete confirmation, the row immediately receives an `.is-removing` class:
```css
.user-row.is-removing {
  opacity: 0;
  pointer-events: none;
  transition: opacity 200ms ease;
}
```
The row is removed from React state after `transitionend` fires (or after a 220ms fallback timeout). No height animation, no `useLayoutEffect`. The layout shift on removal is acceptable for an internal admin tool.

---

## Type requirements

The following TypeScript types are required. The backend must ship these shapes before the frontend can be fully integrated.

### `AuthContext.User` extension (story-031a)

```typescript
export interface User {
  id: string
  displayName: string
  avatarUrl: string | null
  email: string
  theme: 'light' | 'dark' | 'system'
  isAdmin: boolean   // NEW — added in story-031a
}
```

`isAdmin` must be returned by `/api/auth/me` in the `UserDto`. The `adminShell()` guard depends on this field.

### Admin user row

```typescript
interface AdminUserRow {
  id: string
  displayName: string
  email: string
  emailConfirmed: boolean
  createdAt: string          // ISO 8601
  lastSeenAt: string | null  // ISO 8601
  loginMethods: ('google' | 'password')[]
  isLockedOut: boolean
  lockoutEnd: string | null  // ISO 8601
  isAdmin: boolean
}
```

Note: `loginMethods` is `('google' | 'password')[]`, not a three-literal union. The value `'both'` does not exist.

### Paginated response envelope (used by users and rooms endpoints)

```typescript
interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}
```

### Admin room row

```typescript
interface AdminRoomRow {
  id: string
  joinCode: string
  gameId: string
  gameName: string
  hostId: string
  hostDisplayName: string
  status: 'waiting' | 'in_progress' | 'finished' | 'closed'
  playerCount: number
  connectedCount: number
  createdAt: string   // ISO 8601
  updatedAt: string   // ISO 8601
  expiresAt: string   // ISO 8601
}
```

### Log entry

```typescript
interface LogEntry {
  timestamp: string   // ISO 8601
  level: 'Trace' | 'Debug' | 'Information' | 'Warning' | 'Error' | 'Critical'
  message: string
  category: string
  eventId: number | null
  exception: string | null
}
```

---

## Known backend dependency

| Dependency | Required by | Blocks |
|---|---|---|
| `isAdmin: boolean` on `UserDto` and `/api/auth/me` response | story-031a (backend) | `adminShell()` guard, admin badge in header |
| `AdminOnly` authorization policy on all `/api/admin/*` endpoints | story-031a (backend) | All admin API calls |
| `GET /api/admin/users` with pagination + search | story-031b (backend) | `AdminUsersPage` |
| User action endpoints (reset, unlock, delete, grant/revoke admin) | story-031b (backend) | Per-row actions in `AdminUsersPage` |
| `GET /api/admin/rooms` with filters | story-031d (backend) | `AdminRoomsPage` |
| `DELETE /api/admin/rooms/{roomId}` | story-031d (backend) | Row delete in `AdminRoomsPage` |
| `GET /api/admin/logs` with level/category/limit params | story-031f (backend) | `AdminLogsPage` |

Story-031a must be complete before any frontend admin work begins. Backend stories 031b, 031d, and 031f can run in parallel with their corresponding frontend stories, but the frontend pages cannot be fully integration-tested until their backend counterpart is done.

---

## Log viewer behaviour

The log viewer issues a single API call to `GET /api/admin/logs` with the user's chosen filters (`level`, `category`, `limit`). The backend returns a capped, single-fetch response (default 200 entries, max 1000) newest-first. This is not a streaming or paginated endpoint. The frontend renders the returned slice as a scrollable list inside a fixed-height container with `overflow-y: auto`.

There is no cursor-based pagination or infinite scroll at this stage. The cap at 1000 entries and the Refresh-on-demand model is the intended design. If log volume grows beyond what this model can support, a future story will introduce filtering by time range or persistent log storage. That is explicitly out of scope here.

---

## Accessibility

- All icon-only buttons must have `aria-label` describing the action and the target (e.g. `aria-label="Delete user Alice Smith"`), not just the action.
- `ConfirmDialog` must trap focus while open (`useFocusTrap`) and restore focus to the triggering element on close.
- `AdminMobileActionSheet` must trap focus while open and close on `Escape`.
- The tab bar in `AdminShell` must use `role="tablist"` and `role="tab"` with `aria-selected` on the active tab.
- All table headers use `<th scope="col">`.
- Status badges and level badges are not the sole indicator of meaning — the text label is always present.
- All tap targets are ≥ 44px (height) on mobile. Row action buttons at < 700px appear in the bottom sheet, not squeezed into the table row.
- Confirm dialogs use `role="alertdialog"` with `aria-modal="true"` and `aria-labelledby` pointing to the dialog heading.
- Ally must run and clear all "Must fix" items before this story is marked done.

---

## Out of scope

- Real-time log streaming or WebSocket log tail — single-call Refresh model only.
- Cursor-based or infinite-scroll pagination in the log viewer.
- Audit log of admin actions (who deleted which user) — future story.
- Admin impersonation / login-as-user — future story.
- Any game-specific admin actions (resetting a game mid-session, etc.) — game responsibility.
- Email template preview or editing.
- Role management beyond grant/revoke the single `Admin` role.
- Admin portal dark/light theme divergence — the portal uses the same theme tokens as the rest of the platform.
