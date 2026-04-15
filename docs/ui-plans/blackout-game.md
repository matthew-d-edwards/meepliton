# UI Plan: Blackout Game

**Status:** Agreed
**Date:** 2026-04-11
**Authors:** ux + frontend

---

## Design intent

Blackout is an industrial pollution-management auction game. The visual register is
Blade Runner industrial decay: dark surfaces, cyan for active network tiles, magenta
for disruption and high-pollution danger. Growth tiles read as cool blue-cyan (healthy
network energy); Waste tiles read as hot red-orange (decay and contamination).

The player's own stats panel is dense and information-rich. Opponent cards are compact
and at-a-glance — they convey threat level (pollution) and phase-gated actions only.
The auction panel dominates when bidding is active. Outside of bidding, the lot grid
takes focus.

The layout is a three-region shell: lot grid centre, player stats panel (right on
desktop, bottom sheet on mobile), opponent strip (left on desktop, scrolling row on
mobile).

---

## Breakpoint behaviour

| Breakpoint | Layout |
|---|---|
| < 700px | Single column. LotGrid full-width top. Player stats in a bottom sheet (swipe up to expand). Opponent cards in a horizontal scrolling row below the grid. AuctionPanel slides up from bottom as a bottom sheet when auction phase is active. DisruptionConfirmModal is a full-width bottom sheet. |
| 700–1100px | Two columns. LotGrid left (flex-grow). Player stats + opponent cards stacked in a right sidebar (min-width 260px). AuctionPanel renders inside the right sidebar column. |
| > 1100px | Three columns. Opponent strip left (fixed 200px). LotGrid centre (flex-grow, max-width 720px). Player stats + AuctionPanel right (fixed 280px). Max-width container centred with `--space-8` horizontal padding. |

All tap targets are a minimum of 44px in every dimension. The Disrupt button on
OpponentCard and the BID button on AuctionPanel are minimum 44px tall.

---

## Game phases

The UI must track and respond to four named phases:

- `Draft` — players place tiles from their hand; no auction panel visible
- `Auction` — AuctionPanel is active; LotGrid is display-only
- `Burn` — Disrupt button appears on OpponentCard (if player has a disrupt token)
- `Resolution` — display-only; AuditAlert may fire

---

## Components

| Component | Location | New or existing |
|---|---|---|
| `LotGrid` | `apps/frontend/src/games/blackout/components/LotGrid.tsx` | New (game-owned) |
| `LotCell` | `apps/frontend/src/games/blackout/components/LotCell.tsx` | New (game-owned) |
| `TilePiece` | `apps/frontend/src/games/blackout/components/TilePiece.tsx` | New (game-owned) |
| `TileHand` | `apps/frontend/src/games/blackout/components/TileHand.tsx` | New (game-owned) |
| `PlayerStatsPanel` | `apps/frontend/src/games/blackout/components/PlayerStatsPanel.tsx` | New (game-owned) |
| `PollutionMeter` | `apps/frontend/src/games/blackout/components/PollutionMeter.tsx` | New (game-owned) |
| `OpponentCard` | `apps/frontend/src/games/blackout/components/OpponentCard.tsx` | New (game-owned) |
| `AuctionPanel` | `apps/frontend/src/games/blackout/components/AuctionPanel.tsx` | New (game-owned) |
| `BidInput` | `apps/frontend/src/games/blackout/components/BidInput.tsx` | New (game-owned) |
| `AuditAlert` | `apps/frontend/src/games/blackout/components/AuditAlert.tsx` | New (game-owned) |
| `DisruptionConfirmModal` | `apps/frontend/src/games/blackout/components/DisruptionConfirmModal.tsx` | New (game-owned) |
| `BottomSheet` | **Pending architect sign-off** — either extracted to `packages/ui/` or duplicated locally | Pre-condition (see below) |
| `ConfirmModal` | **Pending architect sign-off** — either extracted to `packages/ui/` or duplicated locally | Pre-condition (see below) |

**Pre-condition for 375px layout and DisruptionConfirmModal:** The session owner must
consult the `architect` agent before the frontend begins the 375px layout work or builds
`DisruptionConfirmModal`. `BottomSheet` and `ConfirmModal` are candidates for extraction
to `packages/ui/` but require architect sign-off to confirm they carry zero game-state
knowledge before extraction. Do not start these components without that sign-off.

All components are game-owned. None are imported from `packages/ui/`. None may be
extracted to `packages/ui/` without the architect sign-off process.

---

## CSS approach

- CSS Modules for all game components (`*.module.css` per component)
- Game token scope in `apps/frontend/src/games/blackout/blackout.css` — imported once
  at the game entry point
- `blackout.css` defines all game-specific tokens as CSS custom properties. All hex
  values live only in `blackout.css`. No hex values appear in component CSS.
- All platform tokens (`--surface-*`, `--text-*`, `--accent`, `--neon-*`, `--glow-*`,
  `--space-*`, `--radius-*`, `--font-*`, `--dur-*`) used directly by name in component
  CSS — never hardcoded
- No inline styles, ever
- Hover transitions: `200ms ease`
- Panel animations: `320ms cubic-bezier(0.4, 0, 0.2, 1)`
- Pulse animations: `1.2s ease-in-out infinite` (see PollutionMeter)

### Tile type tokens (defined in `blackout.css`)

Growth family (blue-green — healthy networked energy):

```css
--tile-growth-core:      #00e5ff;   /* bright cyan — most important growth tile */
--tile-growth-turbine:   #00bfae;   /* teal */
--tile-growth-connector: #4dd0e1;   /* light cyan */
--tile-growth-scrubber:  #26a69a;   /* muted teal */
```

Waste family (red-orange — decay and contamination):

```css
--tile-waste-sludge:     #ff5722;   /* deep orange */
--tile-waste-corroded:   #e64a19;   /* burnt orange */
--tile-waste-dead:       #4a1a1a;   /* near-black red — visually dead, very low saturation */
--tile-waste-disruption: #ff1744;   /* hot red — highest alarm */
```

Component CSS references only the token name, e.g.:

```css
.tileGrowthCore    { background-color: var(--tile-growth-core); }
.tileWasteSludge   { background-color: var(--tile-waste-sludge); }
```

---

## Component specifications

### LotGrid

A grid of `LotCell` components representing the player's lot (the board area where
tiles are placed). The grid dimensions are driven by server state.

- Rendered as CSS grid; cell size responsive (clamp between 40px and 64px)
- Each cell receives a `networkActive` boolean from server state; active cells show
  `box-shadow: var(--glow-sm) var(--neon-cyan)`
- Ghost preview renders at the cursor/touch position during tile placement, using
  reduced opacity (0.5) and a cyan outline
- Tile placement is click-to-place, not drag-and-drop (v1)
- Shape offset arrays use `[row, col]` format (agreed)
- Internal grid structure: `LotCell[][]` nested array (agreed)

### LotCell

Individual cell within `LotGrid`.

- Default state: `background: var(--surface-raised)`, border `1px solid var(--edge-subtle)`
- Occupied by Growth tile: background via `--tile-growth-*` token for tile type;
  glow `var(--glow-sm) var(--neon-cyan)` when `networkActive: true`
- Occupied by Waste tile: background via `--tile-waste-*` token for tile type; no glow
- Ghost state: outline `2px solid var(--neon-cyan)`, background-color at 0.5 opacity
  of the placement tile's token colour

### TilePiece

Renders a single polyomino piece in the player's hand or as a ghost on the grid.

- Shape defined by `[row, col]` offset arrays
- Rotate (R key) and flip (F key) keyboard shortcuts active when focus is NOT on
  `INPUT`, `TEXTAREA`, or `[contenteditable]`
- Visual: filled cells use tile type token colour, with `border-radius: var(--radius-sm)`

### TileHand

Horizontal scrolling strip of `TilePiece` components representing the player's
current hand of tiles.

- Selected tile has `box-shadow: var(--glow-md) var(--neon-cyan)` and a
  `2px solid var(--neon-cyan)` border
- Minimum tile tap target: 44px

### PlayerStatsPanel

The player's own stats panel. Shown in the right column (desktop) or as a bottom
sheet (mobile).

- Sections: cash balance, pollution level (full segmented PollutionMeter), network
  score, tile hand
- Cash balance uses `font-family: var(--font-mono)`; pollution and score use
  `font-family: var(--font-mono)` for the numeric value, `var(--font-display)` for
  the label
- Panel background: `var(--surface-float)`; border: `1px solid var(--edge-subtle)`

### PollutionMeter — full segmented (PlayerStatsPanel only)

12 discrete segments. Segment state:

- Empty: `background: var(--surface-raised)`, border `1px solid var(--edge-subtle)`
- Filled low (1–4): `background: var(--status-success)` (`#00e070`)
- Filled mid (5–8): `background: var(--status-warning)` (`#f0c040`)
- Filled high (9–12): `background: var(--status-error)` (`#ff2a50`)

Pulse animation when pollution is at 10 or above:

```css
@keyframes pollutionPulse {
  0%, 100% { opacity: 1; }
  50%       { opacity: 0.5; }
}
.pollutionPulse {
  animation: pollutionPulse 1.2s ease-in-out infinite;
}
```

The pulse is glow/opacity only — no scale transform on the meter.

### PollutionMeter — compact bar (OpponentCard only)

A single narrow bar (`height: 6px`, `border-radius: var(--radius-pill)`) using
`linear-gradient` from left to right. Fill percentage = `pollution / 12 * 100%`.

The gradient encodes the same hue progression as the segmented design:
`linear-gradient(to right, var(--status-success), var(--status-warning), var(--status-error))`
clipped to the fill width.

Glow applied via a CSS class determined by a `severity` prop (`'low' | 'mid' | 'high'`):

```css
.severityLow  { box-shadow: var(--glow-sm) var(--neon-cyan); }
.severityMid  { box-shadow: var(--glow-sm) var(--status-warning); }
.severityHigh { box-shadow: var(--glow-sm) var(--neon-magenta); }
```

Severity thresholds: low = 1–4, mid = 5–8, high = 9–12.

### OpponentCard

Compact card per opponent. Displayed in the opponent strip.

- Shows: player name (`var(--font-display)`), cash (`var(--font-mono)`), compact
  PollutionMeter bar, network score (`var(--font-mono)`)
- The Disrupt button is conditionally rendered: only when `phase === 'Burn'` AND the
  current player has at least one disrupt token available. Outside those conditions,
  `OpponentCard` is display-only.
- Disrupt button: `.btn-secondary` style, minimum 44px tall, magenta text
  (`color: var(--neon-magenta)`), triggers `DisruptionConfirmModal` on press
- Card background: `var(--surface-raised)`; border: `1px solid var(--edge-subtle)`

### AuctionPanel

Visible only during `Auction` phase. Contains the lot being auctioned, current high
bid, current high bidder (identified by `currentBidderId` from server state — agreed),
and `BidInput`.

- Panel slides in via `320ms cubic-bezier(0.4, 0, 0.2, 1)` transition
- Lot preview is a miniature `LotGrid` (read-only, no placement interaction)
- Current high bid: `var(--font-mono)`, gold colour (`var(--accent)`)
- Current high bidder name: `var(--font-body)`, `var(--text-primary)`

### BidInput

Free-text numeric input for entering a bid amount.

- Input `font-family: var(--font-mono)`; `:focus` border uses `var(--accent)` (gold)
- "BID" button: `.btn-primary` (magenta CTA), minimum 44px tall, `var(--font-display)`
- Inline validation: errors appear as `var(--neon-magenta)` text in `var(--font-mono)`
  directly below the input, with `role="alert"`. The BID button remains enabled.
  Error strings:
  - Below current high bid: "Below current bid of [X]"
  - Exceeds player cash: "You only have [X] available"
- Submitting while an error condition exists re-evaluates server-side and blocks the
  action if still invalid.

### AuditAlert

Inline notification that fires during or after `Resolution` phase when a pollution
audit occurs.

- Background: `var(--surface-overlay)`, left border `4px solid var(--neon-magenta)`
- Text: `var(--font-body)`, `var(--text-bright)`
- Label "AUDIT" in `var(--font-display)`, `var(--neon-magenta)`
- Auto-dismisses: holds for 4000ms, then fades out over `var(--dur-slow)` (320ms)
  Total visible duration approximately 4.3 seconds
- Manual dismiss available throughout (close button, `aria-label="Dismiss audit alert"`)
- `role="alert"` on the container

### DisruptionConfirmModal

Modal (or bottom sheet on mobile — pending architect sign-off) confirming that the
player wants to use a disrupt token against a named opponent.

- Triggered by Disrupt button on `OpponentCard`
- Body copy: `var(--font-body)`, `var(--text-primary)`
- Confirm button: `.btn-primary` (magenta), `var(--font-display)`
- Cancel button: `.btn-secondary`, `var(--font-display)`
- On mobile (<700px): renders as a full-width bottom sheet (BottomSheet component —
  pending architect sign-off on extraction)
- On desktop: centred overlay modal with `var(--surface-overlay)` background,
  `border: 1px solid var(--edge-strong)`

---

## Type requirements

All types live in the game's own types file:
`apps/frontend/src/games/blackout/types.ts`

```typescript
type TileType =
  | 'GrowthCore' | 'GrowthTurbine' | 'GrowthConnector' | 'GrowthScrubber'
  | 'WasteSludge' | 'WasteCorroded' | 'WasteDead' | 'WasteDisruption';

type GamePhase = 'Draft' | 'Auction' | 'Burn' | 'Resolution';

type PollutionSeverity = 'low' | 'mid' | 'high';

interface LotCell {
  tileType: TileType | null;
  networkActive: boolean;     // server sends this per cell (agreed)
}

// Grid is a nested array: LotCell[][] (agreed)
type LotGrid = LotCell[][];

interface PolyominoShape {
  offsets: [number, number][];  // [row, col] pairs (agreed)
}

interface AuctionState {
  activeLot: LotGrid;
  currentHighBid: number;
  currentBidderId: string;    // server sends this explicitly (agreed)
  currentHighBidderName: string;
}

interface OpponentSummary {
  playerId: string;
  playerName: string;
  cash: number;
  pollution: number;          // 0–12
  networkScore: number;
  severity: PollutionSeverity;
}
```

---

## Accessibility

- All icon-only buttons have `aria-label` describing the action (e.g. "Dismiss audit
  alert", "Disrupt [player name]", "Rotate tile", "Flip tile")
- `AuditAlert` and `BidInput` error messages use `role="alert"` for screen reader
  announcement
- Keyboard shortcuts R (rotate) and F (flip) are suppressed when focus is on `INPUT`,
  `TEXTAREA`, or `[contenteditable]`
- Visible focus states on all interactive elements: `outline: 2px solid var(--accent)`
  with `outline-offset: 2px`
- All text meets WCAG AA contrast against its background. Custom tile token colours
  (`--tile-growth-*`, `--tile-waste-*`) must be verified against their cell backgrounds
  before shipping — especially `--tile-waste-dead` (`#4a1a1a`) which has low
  luminance and must carry a text label or icon, not rely on colour alone to convey
  tile type
- All tap targets minimum 44px in height and width

---

## Out of scope (v1)

- Drag-and-drop tile placement (v1 is click-to-place)
- Tile animations beyond ghost preview and placement confirmation flash
- Spectator view
- Undo/redo for tile placement
- Tutorial or rule overlay
- Any new npm dependencies — zero new packages (agreed)
- `BottomSheet` and `ConfirmModal` extraction to `packages/ui/` without architect
  sign-off — these remain blocked until the architect reviews and confirms they carry
  no game-state knowledge
