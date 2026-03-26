# Spec: Profile Images

**Status:** Agreed
**Date:** 2026-03-26
**Authors:** analyst + architect

---

## Problem

Player avatars are not shown anywhere in the platform UI — not in the lobby header, not in the player presence list, and not in the turn indicator — even though the data is already available. Google OAuth users have a profile picture URL stored in the database. Email/password users without a custom avatar can be served a Gravatar image derived from their email address. Both cases are currently handled inconsistently: the `GET /me` response resolves avatar URLs correctly, but `PlayerJoined` SignalR pushes and the `GET /rooms/{roomId}/players` REST response do not. Worse, `POST /rooms/{roomId}/start` passes unresolved `null` avatar URLs to `CreateInitialState`, meaning any game that copies `avatarUrl` into its own state will store null for email/password users permanently.

---

## Solution

The fix has three layers.

**Backend — single resolution point.** Extract the existing `ResolveAvatarUrl` logic from `AuthEndpoints.cs` into a shared static helper class in the `Meepliton.Api` project. Every place that assembles a `PlayerInfo` or a player DTO — the `PlayerJoined` SignalR push, the `GET /rooms/{roomId}/players` endpoint, and the `POST /rooms/{roomId}/start` handler — calls this helper. Gravatar URLs are generated with `s=80` and `d=identicon` query parameters. No caching of the MD5 computation is needed; the hash is cheap to recompute and caching would introduce staleness risk.

**Frontend — shared `Avatar` component.** A new `Avatar` component in `packages/ui` accepts a resolved URL (which may be null if the backend is older), a display name, and a size (`sm` or `md`). When the URL is present it renders an `<img>` with `alt=""` (decorative). If the URL is null or the image fails to load, it falls back to the user's initials in a styled placeholder. The `onError` fallback is handled internally so callers never need to manage it. This component is used in the `AppShell` header, the `PlayerPresence` list, and the `TurnIndicator`.

**Platform chrome — header and in-game presence.** The `AppShell` header gains an avatar in place of (or alongside) the current user indicator. Clicking the avatar navigates to `/account` directly, using a new `onAvatarClick: () => void` callback prop on `AppShell` that matches the existing `logoLinkAs` render-prop pattern. The `PlayerPresence` list and `TurnIndicator` components render each player's avatar using the shared `Avatar` component.

---

## Acceptance criteria

- [ ] AC-1: `PlayerJoined` SignalR push includes a resolved avatar URL for all users (Google URL or Gravatar URL, never null for existing accounts)
- [ ] AC-2: `GET /rooms/{roomId}/players` returns resolved avatar URLs for all players
- [ ] AC-3: `POST /rooms/{roomId}/start` passes resolved avatar URLs in the `PlayerInfo` list to `CreateInitialState`
- [ ] AC-4: `ResolveAvatarUrl` is extracted from `AuthEndpoints.cs` to a shared static helper class in `Meepliton.Api`; `AuthEndpoints` and `RoomEndpoints` both call the helper
- [ ] AC-5: `AppShell` header displays the current user's avatar (or initials fallback); clicking it navigates to `/account`
- [ ] AC-6: `PlayerPresence` component shows avatar or initials for every player in the room
- [ ] AC-7: `TurnIndicator` renders the current player's avatar or initials
- [ ] AC-8: Shared `Avatar` component exists in `packages/ui`; accepts `url: string | null`, `displayName: string`, `size: 'sm' | 'md'`; handles `onError` internally
- [ ] AC-9: When an avatar image fails to load, the component falls back to initials — no broken image icon is shown
- [ ] AC-10: Avatar `<img>` elements use `alt=""`; initials placeholder elements use `aria-hidden="true"`

---

## Architecture decisions

- `ResolveAvatarUrl` is extracted to a shared static helper in `Meepliton.Api` (not a service, not injected — a pure static function). Rationale: the existing function is already pure and stateless; a static helper is the lowest-friction, zero-allocation approach.
- Gravatar URLs use `s=80&d=identicon`. No per-context size variation. Rationale: 80px covers both `sm` and `md` avatar sizes at 2x density; premature to add per-size URL logic.
- No caching of Gravatar MD5 computation. Rationale: MD5 is negligibly cheap; caching adds staleness risk for no measurable gain.
- `Avatar` lives in `packages/ui`, not in a game or app. Rationale: it is platform chrome, used across lobby and game views by multiple consumers.
- `AppShell` receives `onAvatarClick: () => void` rather than embedding routing logic. Rationale: consistent with the existing `logoLinkAs` render-prop pattern; keeps `AppShell` decoupled from the router.
- No changes to wire contracts (`PlayerInfo`, SignalR message shapes). The fix is in how values are populated before serialization.
- No database migrations. Avatar URLs are computed at read time from existing stored data (Google URL or email hash).

---

## Out of scope

- Custom avatar upload (user-provided image files)
- Per-context Gravatar sizing (all requests use `s=80`)
- Caching of MD5 hash computation
- Avatar display in chat or spectator views (no such views exist yet)
- Removing or changing the Gravatar fallback in favor of a platform-generated image
- Any change to how Google OAuth profile picture URLs are fetched or refreshed

---

## Implementation hints

- **Backend** (`agent: backend`): Create `AvatarHelper.cs` (or similar) as a static class in `Meepliton.Api`. Move `ResolveAvatarUrl` logic there. Update `AuthEndpoints.cs` to call it. Find all three call sites in `RoomEndpoints.cs` — `PlayerJoined` push, the players list endpoint, and the start endpoint — and ensure each builds `avatarUrl` via the helper. The helper must accept the stored `AvatarUrl` (nullable) and the user's `Email`, and apply `s=80&d=identicon` to Gravatar URLs.
- **Frontend** (`agent: frontend, ux`): Create `packages/ui/src/Avatar.tsx` (and export from `packages/ui/src/index.ts`). Implement `sm` and `md` size variants, initials extraction from `displayName`, and internal `onError` fallback. Update `AppShell` to accept `onAvatarClick: () => void` and render the current user's avatar. Update `PlayerPresence` and `TurnIndicator` to use `Avatar`. Wire `onAvatarClick` in the app shell to `navigate('/account')`.
- **CI** (`agent: devops`): No migrations added; no CI changes required.
