# UI Plan: Love Letter — "Affairs of the Court" Visual Elevation

**Status:** Agreed
**Date:** 2026-04-16
**Authors:** ux + frontend

## Design intent

Love Letter's "Affairs of the Court" theme is hushed, conspiratorial intimacy — a candlelit antechamber, not a throne room. The light source is warm and singular: a single taper on a writing desk. Shadows are deep. Gold catches the flame. Velvet swallows everything else. Every interaction feels like breaking a wax seal. The existing color tokens (deep plum, crimson rose, gold leaf) are correct but unused for material depth. This pass applies parchment-ruled backgrounds, velvet gradient panels, multi-layer shadows, and candlelight glow to make those tokens tangible.

## Breakpoint behaviour

| Breakpoint | Layout |
|---|---|
| < 700px | Single column. `.playersGrid` stays 2-col (fix `minmax(140px,1fr)`). Action panel full-width below. |
| 700–1100px | Two-column grid: `1fr 360px`. Players/status in column 1, action panel sticky-right in column 2. Requires `.contentColumn` wrapper div around column-1 content. |
| > 1100px | Three-column grid **only when set-aside has content**: `200px 1fr 400px`. Applied via `.rootWithSetAside` conditional class on `.root`. Defaults to two-column. |

## Components

| Component / class | Location | New or existing |
|---|---|---|
| `.gameTitle` | `styles.module.css` + `LoveLetterGame.tsx` | New class — replaces `.header` flex row |
| `.headerInfo` | `styles.module.css` + `LoveLetterGame.tsx` | Restructured from inline span to block div |
| `.contentColumn` | `styles.module.css` + `LoveLetterGame.tsx` | New wrapper div for column-1 grid content |
| `.root` | `styles.module.css` | Existing — add grid layout, parchment background |
| `.rootWithSetAside` | `styles.module.css` | New — three-column variant, applied conditionally |
| `.playerCard` | `styles.module.css` | Existing — velvet gradient, multi-layer shadow |
| `.playerCardCurrentTurn` | `styles.module.css` | Existing — add candle outer glow |
| `.playerHandmaidTag` | `styles.module.css` | Existing — move hardcoded hex to token |
| `.cardOption` | `styles.module.css` | Existing — velvet gradient, hover lift + glow |
| `.cardOptionSelected` | `styles.module.css` | Existing — full gold outer glow |
| `.cardOptionValue` | `styles.module.css` | Existing — gold text-shadow glow |
| `.actionPanel` | `styles.module.css` | Existing — velvet gradient, top accent border, depth |
| `.statusBanner` | `styles.module.css` | Existing — parchment texture, inset shadow |
| `.token` | `styles.module.css` | Existing — radial gradient (wax seal) + drop shadow |
| `.btnPrimary` | `styles.module.css` | Existing — gradient fill, warm box-shadow |
| `.winnerBanner` | `styles.module.css` | Existing — radial glow, candle-pulse animation |
| `.priestModal` (overlay) | `styles.module.css` | Existing — `@supports` backdrop-filter |
| `.priestModalBox` | `styles.module.css` | Existing — velvet panel treatment |

All changes are game-scoped. No extraction to `packages/ui/`.

## CSS approach

- **Scope:** Game-scoped CSS Modules only. No platform files touched.
- **Tokens:** All new tokens go inside `[data-game-theme="affairs-of-the-court"]` in `loveletter.css`
- **Font import:** `@import` at top of `loveletter.css`, outside the selector block

## New tokens to add to `loveletter.css`

```css
/* At top of file, outside selector */
@import url('https://fonts.googleapis.com/css2?family=Cormorant+Garamond:ital,wght@0,400;0,600;0,700;1,400;1,600&display=swap');

/* Inside [data-game-theme="affairs-of-the-court"] */
--font-game-display: 'Cormorant Garamond', 'Georgia', serif;

/* Candle light */
--court-candle:         #e8b84b;
--court-candle-dim:     #a07820;

/* Velvet surfaces */
--court-velvet:         #3d1030;
--court-velvet-dark:    #1a0812;
--court-velvet-light:   #5a1840;

/* Crimson */
--court-crimson:        #c0406a;
--court-crimson-glow:   rgba(192,64,106,0.35);

/* Wax seal */
--court-wax:            #8b1a2a;
--court-wax-glow:       rgba(139,26,42,0.4);

/* Gold glow */
--court-gold-glow:      rgba(212,168,67,0.45);
--court-gold-glow-sm:   rgba(212,168,67,0.25);

/* Parchment */
--court-parchment:      #f5ecd8;
--court-parchment-dim:  #c8a878;

/* Depth shadows */
--court-shadow-deep:    rgba(0,0,0,0.7);
--court-shadow-mid:     rgba(0,0,0,0.45);

/* Background stops */
--bg-chamber-surface:   #2a0e1c;
--bg-chamber-mid:       #1a0812;
--bg-chamber-deep:      #0d0408;

/* Handmaid — lightened from hardcoded #a0c8a0 for WCAG AA (#b4d4b4 = 6.3:1 on dark bg) */
--court-handmaid:       #b4d4b4;
```

## Animations required

```css
@keyframes candle-pulse {
  0%, 100% { box-shadow: 0 0 20px var(--court-gold-glow), inset 0 0 40px rgba(212,168,67,0.05); }
  50%       { box-shadow: 0 0 40px var(--court-gold-glow), inset 0 0 60px rgba(212,168,67,0.12); }
}
/* Applied to .winnerBanner — 2.8s ease-in-out infinite */
```

Reduced motion override:
```css
@media (prefers-reduced-motion: reduce) {
  .winnerBanner   { animation: none; }
  .cardOption:hover { transform: none; }
}
```

## Key styling notes

### `.root` background
Parchment horizontal ruled lines (barely visible, `opacity ≈ 0.018`) via `repeating-linear-gradient` + radial depth field with candle-light at top:
```css
background:
  repeating-linear-gradient(
    0deg,
    transparent 0px, transparent 28px,
    rgba(212,168,67,0.018) 28px, rgba(212,168,67,0.018) 29px
  ),
  radial-gradient(ellipse 110% 80% at 50% 10%,
    var(--bg-chamber-surface) 0%,
    var(--bg-chamber-mid) 50%,
    var(--bg-chamber-deep) 100%
  );
```

### `.gameTitle` banner
Full-width block element (replaces flex-row `.header`). Velvet gradient background, 2px `--court-candle` border top and bottom, deep box-shadow, inset top highlight. `::before` / `::after` pseudo-elements render `❧` at left/right edges in `--court-candle` at `opacity: 0.55`. Title text: `font-family: var(--font-game-display)`, gold text-shadow glow. Subtitle uses `font-style: italic`.

### `.playerCard` velvet panel
```
background: linear-gradient(145deg, --court-velvet-light 0%, --court-velvet 40%, --court-velvet-dark 100%)
border-top: 2px solid --court-candle-dim
box-shadow: inset 0 1px 0 rgba(212,168,67,0.15), inset 0 -1px 0 rgba(0,0,0,0.4), 0 4px 16px --court-shadow-mid
```
`.playerCardCurrentTurn` gains outer glow: `0 0 20px var(--court-gold-glow)`.

### `.cardOption` hover lift
Pure CSS — no TSX. `transition: transform 200ms ease, border-color 200ms ease, box-shadow 200ms ease`. Hover: `translateY(-2px)` + outer glow. No parent has `overflow: hidden` — safe.

### `.priestModal` backdrop-filter
```css
@supports (backdrop-filter: blur(3px)) {
  .priestModal { backdrop-filter: blur(3px); }
}
```
Fallback: opaque `color-mix(in srgb, #000 75%, var(--court-velvet-dark))` background (always set, guard adds blur as enhancement only).

### `font-weight: 900` → `700`
Find and replace all `font-weight: 900` in `styles.module.css` with `font-weight: 700`. Cormorant Garamond's maximum weight is 700; 900 silently fell back. This makes the code truthful — no visual change.

### Inline styles in TSX — must fix during this pass
Three locations in `LoveLetterGame.tsx` (lines 58, 241, 260) use inline `style={{}}` props for layout/typography. These must be moved to named CSS Module classes during this pass — they are a token compliance violation regardless of visual impact.

### `.playerHandmaidTag` token migration
Replace hardcoded `#a0c8a0` at `styles.module.css` lines 149–150 with `var(--court-handmaid)`. The lightened value `#b4d4b4` gives 6.3:1 on dark background, up from ≈4.6:1.

### Three-column layout — conditional class
```tsx
// LoveLetterGame.tsx root element
<div
  className={state.faceUpSetAside.length > 0 ? styles.rootWithSetAside : styles.root}
  data-game-theme="affairs-of-the-court"
>
```
Content-driven: collapses the set-aside column to zero automatically in 3–4 player games.

## TSX changes required

Three changes in `LoveLetterGame.tsx`:

1. **Header restructure** (lines 184–190) — replace `.header` flex row with block-stacked `.gameTitle` + `.headerInfo` divs.
2. **Content column wrapper** — wrap players grid, status banner, round-end panel in `<div className={styles.contentColumn}>` for grid placement at 768px+.
3. **Conditional root class** — apply `styles.rootWithSetAside` when `state.faceUpSetAside.length > 0`.
4. **Inline style cleanup** — move inline `style={{}}` at lines 58, 241, 260 into named CSS Module classes.

## Type requirements

All required fields confirmed present:
- `player.handmaid` — `bool` in C# / `boolean` in TS (`types.ts` line 28)
- `player.tokens` — `int` / `number` (`types.ts` line 27)
- `state.faceUpSetAside` — `string[]` (`types.ts` line 10), used for conditional class

No new types needed.

## Accessibility

| Requirement | Detail |
|---|---|
| `.playerHandmaidTag` | `#b4d4b4` on `#1a0812` = 6.3:1 — passes AA |
| `.statusBanner` muted text | Re-verify `--color-text-muted` against darkened gradient background after implementation |
| Gold text-shadow glows | Decorative only — contrast measured on base color, not glow |
| `.cardOption` focus-visible | Add `outline-offset: 3px` (currently 2px) — new box-shadow creates visual noise close to border |
| `candle-pulse` animation | Must be disabled in `prefers-reduced-motion: reduce` |
| `.cardOption:hover` lift | `transform: none` in `prefers-reduced-motion: reduce` |
| `backdrop-filter` | Wrapped in `@supports` — no accessibility impact |

## Out of scope

- No platform component extractions (`packages/ui/`)
- No API, SignalR, or game logic changes
- No new TypeScript types
- No changes to other games
- Status text and body copy remain in `var(--font-body)` — Cormorant Garamond is display-only
- The set-aside section content and rendering logic are unchanged — only its grid placement is conditional
