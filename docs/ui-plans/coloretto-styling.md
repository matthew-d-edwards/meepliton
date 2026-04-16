# UI Plan: Coloretto — "Chameleon Market" Interaction Polish

**Status:** Agreed
**Date:** 2026-04-16
**Authors:** ux + frontend

## Design intent

Coloretto's flat Mondrian aesthetic (light surfaces, 2px black borders, zero border-radius, bold colour chips) is intentional and must be preserved entirely. No gradients, no dark surfaces, no shadows, no border-radius. This pass adds the quality-of-life layer that is missing: animation on card arrivals, a breathing turn indicator, staggered winner reveal, improved hover feedback, and a 1280px two-column layout so rows and players are visible simultaneously.

## Breakpoint behaviour

| Breakpoint | Layout |
|---|---|
| < 768px | Single column. `.rowActions` renders below `.rowCards` (current behaviour). `.rowFullBadge` renders inline below chips. |
| 768px–1100px | Single column continues. `.row` switches to `flex-direction: row` — `.rowCards` left, `.rowActions` right. `.rowFullBadge` becomes absolute overlay centred on `.rowCards`. |
| > 1100px | Two-column grid. Header and status banner span full width. Rows + action row in column 1 (flexible). Players section fixed 280px right column. Scores panel and winner banner span full width. |

## Components

| Component / class | Location | New or existing |
|---|---|---|
| `.chip` | `styles.module.css` + `CardChip.tsx` | New class (extracted from inline styles) |
| `.chipArriving` | `styles.module.css` + `RowDisplay.tsx` | New class + local state in `RowDisplay` |
| `.row` | `styles.module.css` | Existing — improve transition, add `position: relative` |
| `.rowCards` | `styles.module.css` | Existing — add `position: relative` for badge overlay |
| `.rowActions` | `styles.module.css` | Existing — add 768px row-direction CSS |
| `.rowSelected` | `styles.module.css` | Existing — add transition |
| `.rowFullBadge` | `styles.module.css` | Existing — absolute overlay at ≥768px |
| `.btn` | `styles.module.css` | Existing — fix `transition: all`, add hover lift |
| `.playerCardCurrentTurn` | `styles.module.css` | Existing — `@keyframes turn-pulse` |
| `.playerCardTaken` | `styles.module.css` | Existing — add opacity transition |
| `.statusBanner` / `.statusBannerEndGame` | `styles.module.css` + `ColorettoGame.tsx` | Existing — consolidate into one persistent element |
| `.collectionEntry` (chip hover) | `styles.module.css` | Existing — add `scale(1.15)` micro-pulse |
| `.scoreRowWinner` | `styles.module.css` + `ScoreRow.tsx` | Existing — `@keyframes winner-arrive` with stagger |
| `RowDisplay` accessibility | `RowDisplay.tsx` | Existing component — add `role`, `tabIndex`, `onKeyDown`, `aria-label` |

All changes are game-scoped. Nothing extracted to `packages/ui/`.

## CSS approach

- **Scope:** Game-scoped CSS Modules only. No platform files touched.
- **Tokens:** Motion tokens added to `coloretto.css` inside `[data-game-theme="chameleon-market"]`
- **No new colour tokens** — the existing `--color-*` and `--card-*` set is complete

## New tokens to add to `coloretto.css`

```css
/* Inside [data-game-theme="chameleon-market"] */

/* Motion */
--duration-quick:    150ms;
--duration-standard: 200ms;
--duration-panel:    320ms;
--ease-standard:     ease;
--ease-panel:        cubic-bezier(0.4, 0, 0.2, 1);

/* Chip geometry */
--chip-lift-y:       -2px;
--chip-size:         36px;
--chip-border-width: 2px;
```

## Animations required

```css
@keyframes chip-arrive {
  0%   { transform: translateY(-8px); opacity: 0; }
  60%  { transform: translateY(2px);  opacity: 1; }
  100% { transform: translateY(0);    opacity: 1; }
}
/* 240ms cubic-bezier(0.4,0,0.2,1) — class .chipArriving, removed via onAnimationEnd */

@keyframes turn-pulse {
  0%   { box-shadow: 0 0 0 2px var(--color-primary); }
  50%  { box-shadow: 0 0 0 4px color-mix(in srgb, var(--color-primary) 30%, transparent); }
  100% { box-shadow: 0 0 0 2px var(--color-primary); }
}
/* 1800ms ease-in-out infinite — applied to .playerCardCurrentTurn */

@keyframes winner-arrive {
  0%   { transform: translateX(-12px); opacity: 0; }
  100% { transform: translateX(0);     opacity: 1; }
}
/* 280ms cubic-bezier(0.4,0,0.2,1) — stagger via animation-delay: calc(var(--row-index) * 80ms) */

@keyframes row-card-land {
  0%   { border-color: var(--color-primary); }
  60%  { border-color: var(--color-primary); }
  100% { border-color: var(--color-border);  }
}
/* 400ms ease — class .rowCardLanding, removed via onAnimationEnd */
```

Reduced motion overrides:
```css
@media (prefers-reduced-motion: reduce) {
  .chipArriving,
  .rowCardLanding,
  .playerCardCurrentTurn,
  .scoreRowWinner,
  .playerCardTaken {
    animation: none;
    transition: none;
  }
  .chip:hover,
  .btn:hover { transform: none; }
}
```

## Key styling notes

### `.chip` class (extracted from `CardChip.tsx`)
Move all static properties from inline style to class: `border`, `font-family`, `font-size`, `font-weight`, `letter-spacing`, `display`, `align-items`, `justify-content`, `flex-shrink`. Keep `background`, `color`, and `width`/`height` (size-dependent) as inline props. Add:
```css
.chip {
  transition: transform var(--duration-standard) var(--ease-standard),
              filter var(--duration-standard) var(--ease-standard);
  cursor: default;
}
.chip:hover {
  transform: scale(1.08);
  filter: brightness(1.15);
}
```
No glow on chip hover — glow is reserved for interactive targets.

### `.btn` — fix transition
Replace `transition: all 150ms` with:
```css
transition: background var(--duration-standard) var(--ease-standard),
            border-color var(--duration-standard) var(--ease-standard),
            transform var(--duration-standard) var(--ease-standard),
            filter var(--duration-standard) var(--ease-standard);
```
Add hover micro-lift:
```css
.btnPrimary:hover:not(:disabled)   { transform: translateY(-1px); }
.btnPrimary:active:not(:disabled)  { transform: translateY(0); }
.btnSecondary:hover:not(:disabled) { transform: translateY(-1px); }
```

### `.statusBanner` — merged persistent element
The two separate conditional blocks must be merged into one persistent `<div className={...}>` when `phase === 'Playing'`, toggling `.statusBannerEndGame` as a modifier. Add:
```css
.statusBanner {
  transition: border-color 200ms ease, background-color 200ms ease, color 200ms ease;
}
```

### `.rowFullBadge` — absolute overlay at ≥768px
`.rowCards` gets `position: relative`. At `≥768px`:
```css
.rowFullBadge {
  position: absolute;
  top: 50%; left: 50%;
  transform: translate(-50%, -50%);
  background: color-mix(in srgb, var(--color-surface) 85%, transparent);
  z-index: 1;
}
```
At `<768px`: `position: static` (reset to default, renders below chips).

### 1280px grid layout
```css
@media (min-width: 1100px) {
  .root {
    display: grid;
    grid-template-columns: 1fr 280px;
    grid-template-areas:
      "header         header"
      "statusBanner   statusBanner"
      "rows           playersSection"
      "actionRow      playersSection"
      "scoresPanel    scoresPanel"
      "winnerBanner   winnerBanner";
  }
  .header              { grid-area: header; }
  .statusBanner        { grid-area: statusBanner; }
  .rows                { grid-area: rows; }
  .actionRow           { grid-area: actionRow; }
  .playersSection      { grid-area: playersSection; }
  .scoresPanel         { grid-area: scoresPanel; }
  .winnerBanner        { grid-area: winnerBanner; }
}
```

## TSX changes required

Five changes across three files:

### `CardChip.tsx`
1. Add `className={styles.chip}` (or `styles.chipArriving`) to the root `<div>`. Move static style properties to the CSS class. Keep `background`, `color`, and `width`/`height` inline.

### `RowDisplay.tsx`
2. **`.chipArriving` tracking** — add `useRef<string[]>` for previous `row.cards` and `useState<Set<string>>` for arriving chip keys. On each render diff previous vs current array; add new index keys to the arriving set; remove via `onAnimationEnd`.
3. **`row-card-land` tracking** — add `useState<boolean>` for `.rowCardLanding` class; apply via `onAnimationEnd` removal.
4. **Accessibility** — apply `role="button"`, `tabIndex={0}`, `onKeyDown` (Enter/Space → `onSelectRow`), and `aria-label` describing row contents only when `isCurrentPlayer && !hasTaken`.

### `ColorettoGame.tsx`
5. **Status banner consolidation** — merge two conditional banner blocks into one persistent `<div>` when `phase === 'Playing'`, toggling `styles.statusBannerEndGame` modifier.
6. **`ScoreRow` `rowIndex` prop** — change `.map(score => ...)` to `.map((score, index) => ...)` and pass `rowIndex={index}` to `ScoreRow`.

### `ScoreRow.tsx`
7. **`rowIndex` prop + CSS custom property** — accept `rowIndex: number` prop, apply `style={{ '--row-index': rowIndex } as React.CSSProperties}` on the `.scoreRowWinner` element.

## Type requirements

No new TypeScript types needed. All existing state fields (`row.cards`, `state.phase`, `state.endGameTriggered`) are sufficient.

## Accessibility

| Requirement | Detail |
|---|---|
| `RowDisplay` keyboard | `role="button"` + `tabIndex={0}` + `onKeyDown` (Enter/Space) — only when `isCurrentPlayer && !hasTaken` |
| `RowDisplay` `aria-label` | Describe row: `"Row {n}, {count} card{s}"` — not just "Row" |
| `.row:focus-visible` | `outline: 2px solid var(--color-primary); outline-offset: 2px` — same as `.btn:focus-visible` |
| Yellow chip contrast | `--card-yellow` on white fails 3:1 fill-to-background — the 2px black border is mandatory and must not be omitted |
| Joker chip contrast | `--card-joker` (mid-grey) on off-white fails 3:1 — 2px black border mandatory |
| All other card colours | Pass 3:1 against white with margin |
| All `@keyframes` | Disabled in `prefers-reduced-motion: reduce` block |
| `.chip:hover` transform | `transform: none` in `prefers-reduced-motion: reduce` |

## Out of scope

- No platform component extractions (`packages/ui/`)
- No API, SignalR, or game logic changes
- No new TypeScript types
- No changes to other games
- No visual changes to the Mondrian flat aesthetic (no gradients, shadows, or border-radius)
- `CardChip` colour rendering logic (`cardColor`/`cardTextColor` helpers) is unchanged
