# UI Plan: Enhanced Lobby Room Cards

**Status:** Agreed
**Date:** 2026-03-26
**Authors:** ux + frontend

## Design intent

The room card is the primary re-entry surface for a returning player. Today it answers only "what game are we playing?" The enhanced card answers all three questions a player needs in under two seconds: what game, who else is here right now (and are they online), and what is happening in the game at this moment. The design adds player avatars with presence dots, a compact turn/round metadata line, and changes "Rejoin" to "View" for finished rooms.

## Breakpoint behaviour

| Breakpoint | Layout |
|---|---|
| < 700px | Avatar strip (32px, overlapping) with comma-separated names on one line below. "Your turn" / "[Name]'s turn" on its own line. Full-width `btn-primary` Rejoin button. Join code hidden. |
| 700–1100px | Join code visible (cyan glow). Avatars in horizontal row with inline name labels + presence dot. Turn + round on single line separated by mid-dot (·). Right-aligned Rejoin button (`btn-secondary`). |
| > 1100px | Same as 700–1100px. Max-width container means cards don't grow wider than ~768px in practice. |

## Components

| Component | Location | New or existing |
|---|---|---|
| `AvatarStrip` | `packages/ui/src/components/AvatarStrip.tsx` | **New** |
| `AvatarStrip.module.css` | `packages/ui/src/components/AvatarStrip.module.css` | **New** |
| `Avatar` | `packages/ui/src/components/Avatar.tsx` | Existing — reuse `size="sm"` |
| Room card layout | `apps/frontend/src/platform/lobby/LobbyPage.tsx` | Existing — extend |
| Room card CSS | `apps/frontend/src/platform/lobby/lobby.css` | Existing — extend |

`AvatarStrip` must be exported from `packages/ui/src/index.ts` after creation.

## AvatarStrip component spec

```typescript
interface AvatarStripProps {
  players: PlayerInfo[]   // from @meepliton/contracts
  max?: number            // default 5; controls overflow chip threshold
}
```

- Renders `sm` (32px) `Avatar` components horizontally
- Each avatar has a 6px presence dot: online = `var(--status-success)` (#00e070), offline = `var(--text-muted)`
- When `players.length > max`, shows first `max - 1` avatars + "+N" overflow chip (same 32px size, `var(--surface-float)` background, `var(--font-mono)` text in `var(--text-muted)`, `var(--edge-strong)` border)
- At mobile (< 700px): negative margin overlap (-8px) between avatar units for space efficiency
- No interactive elements inside the component

## Room card data changes

Extend the local `RoomInfo` interface in `LobbyPage.tsx`:

```typescript
import { PlayerInfo } from '@meepliton/contracts'

interface RoomInfo {
  roomId: string
  gameId: string
  gameName: string
  status: 'waiting' | 'playing' | 'finished'
  playerCount: number
  joinCode: string
  // New fields (all optional — graceful degradation when backend hasn't shipped yet)
  players?: PlayerInfo[]
  currentTurnPlayerId?: string
  roundNumber?: number
}
```

**Graceful degradation:** if `players` is absent or empty, the avatar strip renders nothing and the existing `playerCount` integer serves as fallback. If `currentTurnPlayerId` is absent, the turn line is omitted. If `roundNumber` is absent or 0, the round segment is omitted.

## Turn / round metadata rendering rules

- Only render when `status === 'playing'`
- Compare `room.currentTurnPlayerId === user?.id` (via `useAuth()` hook, already available in `AuthContext`)
  - Match: render "Your turn" in `var(--accent)` (#f0c040) with glow — contrast 9.4:1 against `--surface-raised`, passes WCAG AA
  - No match: render "[Name]'s turn" in `var(--text-muted)` — look up name from `players` array by `currentTurnPlayerId`
- Render "Round N" when `roundNumber` is present and > 0; omit entirely otherwise (no fallback text)
- Turn and round appear on a single line at 700px+, separated by ` · `; stacked on separate lines at < 700px

## Finished room treatment

- Apply `opacity: 0.6` to the **informational content wrapper** only (card header, game name, avatar strip, meta line)
- The button row is **outside** the dimmed wrapper — it renders at full opacity
- Button label: "View" (not "Rejoin") when `status === 'finished'`
- Button `aria-label`: `"View {gameName} room {joinCode}"` (vs `"Rejoin {gameName} room {joinCode}"` for active rooms)

## CSS approach

- Platform chrome (room card layout, turn/round lines, finished-card dimming): **global class names** in `apps/frontend/src/platform/lobby/lobby.css`
- `AvatarStrip` internal styles: **CSS Modules** in `packages/ui/src/components/AvatarStrip.module.css`
- Key tokens:
  - `--accent` (#f0c040) — "Your turn" text
  - `--text-muted` — offline dot, "[Name]'s turn", round number
  - `--font-mono` — round number, overflow chip count
  - `--status-success` (#00e070) — online presence dot
  - `--surface-raised` — card background
  - `--neon-cyan` — join code (existing, unchanged)
  - `--surface-float` — overflow chip background
  - `--edge-strong` — overflow chip border
  - `--dur-base` (200ms) — hover transitions (existing)

## Accessibility

- Avatar images: `aria-hidden="true"` (already the case in Avatar component)
- Presence dots: `aria-hidden="true"`; online/offline state carried by visually-hidden `<span>` alongside each player name in a sr-only list
- "Your turn" / "[Name]'s turn": conveyed as plain text, not colour alone
- Overflow chip: `aria-label="and N more players"`
- Rejoin/View button: `aria-label="Rejoin {gameName} room {joinCode}"` or `"View {gameName} room {joinCode}"` — distinguishes multiple buttons in the list for screen readers
- Touch target: Rejoin/View button `min-height: 44px`
- No focus trap; only the Rejoin/View button is focusable within each card

## Backend dependency

**The `players`, `currentTurnPlayerId`, and `roundNumber` fields require a backend change to `GET /api/lobby`.** The frontend can be implemented defensively today (all new fields optional), but real data depends on the backend shipping:
1. Player info (displayName, avatarUrl, connected status) joined from `RoomPlayers` + `ApplicationUser`
2. `currentTurnPlayerId` sourced from game state (game module must expose it, or platform reads a convention field)
3. `roundNumber` sourced from game state (optional — only games that track rounds need to provide it)

The backend agent must confirm how `currentTurnPlayerId` and `roundNumber` are populated before those fields are considered stable in the contract.

## Implementation checklist

- [ ] Create `packages/ui/src/components/AvatarStrip.tsx`
- [ ] Create `packages/ui/src/components/AvatarStrip.module.css`
- [ ] Export `AvatarStrip` from `packages/ui/src/index.ts`
- [ ] Extend `RoomInfo` interface in `LobbyPage.tsx` (add optional fields + `PlayerInfo` import)
- [ ] Add `useAuth()` call to `LobbyPage` for current user ID
- [ ] Render `AvatarStrip` in room card (with graceful degradation)
- [ ] Render turn/round metadata line (conditional on `status === 'playing'` and field presence)
- [ ] Apply finished-card dimming to content wrapper only (button row outside wrapper)
- [ ] Update Rejoin button: label + aria-label conditional on `status`
- [ ] Update `lobby.css` with new classes for avatar strip wrapper, turn line, round line, finished content wrapper
- [ ] Backend: extend `GET /api/lobby` to return `players`, `currentTurnPlayerId`, `roundNumber`
- [ ] Run `ally` agent before marking story done

## Out of scope

- Scores, last move text, timer, or any other game-specific state
- Time elapsed / last active timestamp
- Per-room real-time updates via SignalR (lobby page does a one-time fetch on load; live updates are a future story)
- Skeleton/shimmer loading states (noted as future-friendly but not required for initial cut)
- `RoomWaitingScreen` changes (a likely future consumer of `AvatarStrip`, but not in this story)
