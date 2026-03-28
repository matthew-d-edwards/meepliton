# UI Plan: Liar's Dice — Immersive Tavern Redesign

**Status:** Agreed
**Date:** 2026-03-27
**Authors:** ux + frontend
**Branch:** `claude/enhance-game-room-design-0Eesz`

---

## Design Intent

The Liar's Dice game room should feel like you have pulled up a stool at a rough-hewn table in a harbour tavern at the edge of the known world — salt-stained wood underfoot, tallow candles guttering in the draught, and the muffled sound of rain on the quayside outside. Every player's leather cup sits heavy on the table. The dice inside are old bone, worn smooth by ten thousand throws, their pips burned in by a hot iron. When someone calls liar, the room goes quiet. The reveal is a verdict, not a UI state update. The visual language must carry that weight: warm candlelight amber fighting against deep harbour-night shadows, materials that look like they have history, and just enough theatrical drama that a bluff called well feels earned.

---

## Breakpoint Behaviour

| Breakpoint | Layout |
|---|---|
| < 700px | Single column. GameStatus full-width at top. Opponents stacked vertically (full-width cards). My cup last, full-width, slightly larger. BidControls sticky at bottom. |
| 700–1100px | Two-column. BidControls anchored left (~280px). Right column: opponents in 2-per-row grid, my cup full-width at bottom of right column. |
| > 1100px | Full layout. GameStatus centred top bar (max-width 900px). Opponents in a single row across the top (up to 6). BidControls bottom-left. My cup bottom-centre, prominent. |

---

## Components

| Component | Location | Change |
|---|---|---|
| `DiceFace.tsx` | `apps/frontend/src/games/liarsdice/components/` | Modify — bone gradient, pip divots, skull wild, `useId()` for gradient IDs |
| `DiceCup.tsx` | `apps/frontend/src/games/liarsdice/components/` | Modify — leather cup SVG, face-down placeholders, my-cup vs opponent distinction, active cup lift |
| `GameStatus.tsx` | `apps/frontend/src/games/liarsdice/components/` | Modify — game title element with IM Fell English at 24px |
| `BidControls.tsx` | `apps/frontend/src/games/liarsdice/components/` | Modify — Call Liar dramatic red styling only |
| `LiarsDiceGame.tsx` | `apps/frontend/src/games/liarsdice/components/` | Modify — render opponents first, my cup last; pass `--seat-index` via inline style |
| `felt-pattern.svg` | `apps/frontend/src/games/liarsdice/` | New — SVG felt texture pattern, imported via Vite `?url` |
| `liarsdice.css` | `apps/frontend/src/games/liarsdice/` | Modify — add `--pirate-*` tokens inside `[data-game-theme="pirates"]` block; add IM Fell English `@import` (behind CSP ship gate) |
| `styles.module.css` | `apps/frontend/src/games/liarsdice/` | Modify — die face gradient classes, cup SVG wrapper, felt texture via `--felt-texture-url`, reveal flip animation, stagger via `--seat-index` |

Nothing moves to `packages/ui/src/` in this pass.

---

## CSS Approach

- **Game-scoped CSS Modules** for all layout and component styling (`styles.module.css`)
- **Token overrides** in `liarsdice.css`, scoped to `[data-game-theme="pirates"]` (existing pattern — do not introduce `.liars-dice-root`)
- **Felt texture** delivered via a separate `felt-pattern.svg` file (Vite `?url` import), set as `--felt-texture-url` CSS custom property via inline style on the `cupsGrid` div
- No hex values in `styles.module.css` — only `var(--pirate-*)` references

---

## New CSS Tokens

All added inside `[data-game-theme="pirates"]` in `liarsdice.css`:

```css
[data-game-theme="pirates"] {
  /* Typography */
  --font-game-display: 'IM Fell English', Georgia, 'Times New Roman', serif;

  /* Candlelight and brass */
  --pirate-candle:        #e8a430;
  --pirate-brass:         #c8973a;   /* aliases existing --color-primary */
  --pirate-brass-dim:     #8a6428;

  /* Leather */
  --pirate-leather:       #6b3518;
  --pirate-leather-dark:  #3d1f0a;

  /* Table felt */
  --pirate-felt:          #1a2e1a;

  /* Parchment text */
  --pirate-parchment:     #e8dfc8;   /* aliases existing --color-text */
  --pirate-parchment-dim: #b8ab8a;

  /* Die face — bone / ivory */
  --pirate-bone-light:    #f5ead6;
  --pirate-bone-mid:      #d4b896;
  --pirate-bone-shadow:   #8a6a3e;
  --pirate-ink:           #2a1506;

  /* Danger */
  --pirate-liar-red:      #c0392b;
  --pirate-liar-glow:     rgba(192, 57, 43, 0.45);
}
```

---

## Implementation Details

### 1. Die Face — `DiceFace.tsx`

Add `useId()` to generate a unique prefix for each SVG instance. Add a `<defs>` block inside each SVG containing:

**Die body:** `<radialGradient id="{uid}-body" cx="35%" cy="30%" r="65%">`
- Stop 0%: `var(--pirate-bone-light)` — warm catchlight
- Stop 50%: `var(--pirate-bone-mid)` — aged ivory mid-tone
- Stop 100%: `var(--pirate-bone-shadow)` — deep amber shadow

Apply to the `<rect>` as `fill="url(#{uid}-body)"`.

**Die shadow:** apply `filter: drop-shadow(2px 4px 6px rgba(0,0,0,0.7))` to the SVG element via a CSS class (`.diceFace` in `styles.module.css`). Do not apply drop-shadow inline.

**Pip divots:** each pip `<circle>` gets a `<radialGradient id="{uid}-pip">`:
- Stop 0% centre: `var(--pirate-ink)` — near-black char
- Stop 70%: `#5c3010` — burnt sienna edge
- Stop 100%: transparent

Apply a `<filter id="{uid}-pipBlur">` with `<feGaussianBlur stdDeviation="0.4">` composited via `feComposite operator="in"` to simulate divot depth.

**Wild dice:** replace the `<text>★</text>` node with an inline `<path>` skull silhouette (two socket circles + jaw arc) rendered in `var(--pirate-candle)`. Wild pip circles use `fill="var(--pirate-candle)"` instead of the ink colour. The skull path sits at approx (10px, 10px) for `md` size. Exact `d` to be determined by implementor to taste — keep it simple and clearly readable at 32px.

**Accessibility:** preserve existing `aria-label` pattern. Wild die: `"Wild die showing {value}"`. Skull path: `aria-hidden="true"`.

---

### 2. Dice Cup SVG — `DiceCup.tsx`

Add a decorative SVG cup silhouette above the dice row. Dimensions: **80×96px** for opponents, **96×112px** for my cup (local player).

SVG construction:
- **Cup body:** `<path>` trapezoid, flared at rim. Approx: `M 10 80 Q 8 20 20 8 L 60 8 Q 72 20 70 80 Z` (refine to taste). Fill: `<linearGradient>` top `var(--pirate-leather-dark)` → bottom `var(--pirate-leather)`, with a 30%-width highlight seam at left edge fading to transparent.
- **Rim:** `<ellipse>` at cup top, `stroke="var(--pirate-leather)"`, `fill="var(--pirate-leather-dark)"`.
- **Rivets:** two `<circle r="2">` in `fill="var(--pirate-brass)"` on cup sides.
- **Interior:** inset `<ellipse>` slightly smaller than rim, `fill="#1a0a04"` — dark mouth.
- `aria-hidden="true"` on the whole SVG.

**Active cup (current turn):** CSS class `.cupSvgActive` adds `transform: translateY(-4px)` with `transition: transform 200ms ease` and `filter: drop-shadow(0 8px 12px rgba(200,151,58,0.4))`. Apply this class to the cup SVG when `isCurrentPlayer`.

**Eliminated cup:** CSS class `.cupSvgEliminated` adds `filter: grayscale(0.8) brightness(0.5)`.

**Reduced motion:** `.cupSvgActive` under `prefers-reduced-motion: reduce` drops the `transform` and `transition` — the gold glow filter still applies (static state, not motion).

---

### 3. Opponent Face-Down Placeholder Dice

During Bidding (`phase === 'Bidding'`), opponents receive `dice = []` from the server. Add logic to `DiceCup.tsx`:

```tsx
const showPlaceholders = diceToShow.length === 0 && player.active
```

When `showPlaceholders` is true, render `player.diceCount` placeholder elements in `.cupHiddenDice`:
- A `<div className={styles.cupHiddenDie}>` per die (existing class, already styled: 48×48px, dark border, 40% opacity)
- These represent the die backs — style with `var(--pirate-leather-dark)` background and a small `?` glyph in `var(--pirate-parchment-dim)` centered inside

When `phase === 'Reveal'`, the `diceToShow` array will be populated (from `revealDice` snapshot), so placeholders disappear naturally and real faces render.

---

### 4. My Cup vs Opponents

In `LiarsDiceGame.tsx`, replace `state.players.map(...)` with:

```tsx
const opponents = state.players.filter(p => p.id !== myPlayerId)
const myPlayer  = state.players.find(p => p.id === myPlayerId)

// Render opponents first, then me last
[...opponents, ...(myPlayer ? [myPlayer] : [])].map(player => ...)
```

Pass `--seat-index` via inline style on each cup's wrapper for stagger animation:

```tsx
<DiceCup
  key={player.id}
  ...
  style={{ '--seat-index': player.seatIndex } as React.CSSProperties}
/>
```

Add `style` prop to `DiceCup`'s props interface (`style?: React.CSSProperties`), forwarded to the root div.

**My cup distinction:**
- CSS class `.cupMe` applied when `isMe`:
  - Larger cup SVG (96×112px vs 80×96px — pass a `size` prop to the cup SVG or use a CSS-driven size via the `.cupMe` parent)
  - Subtle `radial-gradient(ellipse 200px 80px at center bottom, rgba(232,164,48,0.08), transparent)` as additional background layer — barely perceptible candlelight warmth
  - Player name in `var(--pirate-candle)` instead of `var(--pirate-parchment)`
  - A "YOU" label below the name in `var(--font-mono)` 10px `var(--pirate-parchment-dim)`

---

### 5. Felt Table Texture

Create `apps/frontend/src/games/liarsdice/felt-pattern.svg`:

```svg
<svg xmlns="http://www.w3.org/2000/svg" width="4" height="4">
  <rect width="4" height="4" fill="#1a2e1a"/>
  <circle cx="1" cy="1" r="0.4" fill="#22391f" opacity="0.6"/>
  <circle cx="3" cy="1" r="0.4" fill="#22391f" opacity="0.6"/>
  <circle cx="1" cy="3" r="0.4" fill="#22391f" opacity="0.6"/>
  <circle cx="3" cy="3" r="0.4" fill="#22391f" opacity="0.6"/>
</svg>
```

Import in `LiarsDiceGame.tsx`:

```tsx
import feltUrl from '../felt-pattern.svg?url'
```

Apply to `cupsGrid` wrapper div:

```tsx
<div
  className={styles.cupsGrid}
  style={{ '--felt-texture-url': `url(${feltUrl})` } as React.CSSProperties}
  data-stagger
>
```

In `styles.module.css`:

```css
.cupsGrid {
  background-image: var(--felt-texture-url),
    radial-gradient(ellipse 80% 60% at 50% 50%, transparent 40%, rgba(0,0,0,0.55));
  background-repeat: repeat, no-repeat;
  background-size: 4px 4px, 100% 100%;
}
```

The vignette overlay darkens the edges of the felt, lighter under the "candle" (centre).

---

### 6. Typography — Game Title

Add a game title element to `GameStatus.tsx`. Style with `var(--font-game-display)` at **24px / 1.5rem**, italic:

```tsx
<h2 className={styles.gameTitle}>Liar's Dice</h2>
```

```css
.gameTitle {
  font-family: var(--font-game-display);
  font-size: 1.5rem;
  font-style: italic;
  font-weight: 400;
  color: var(--pirate-parchment);
  margin: 0;
  letter-spacing: 0.5px;
}
```

All other elements keep their current font assignments. IM Fell English is not applied below 18px.

---

### 7. "Call Liar" Button Styling

Update `.bidActionBtnLiar` in `styles.module.css`:

```css
.bidActionBtnLiar {
  background: var(--pirate-leather-dark);
  color: var(--pirate-liar-red);
  border: 2px solid var(--pirate-liar-red);
  box-shadow: 0 0 6px var(--pirate-liar-glow);
  font-style: normal;  /* keep mono, no italic at this size */
}

.bidActionBtnLiar:hover:not(:disabled) {
  background: #5a1510;
  border-color: #e04030;
  color: #f5a090;
  box-shadow: 0 0 14px var(--pirate-liar-glow), inset 0 0 8px rgba(192,57,43,0.25);
}

.bidActionBtnLiar:active:not(:disabled) {
  background: #3a0d09;
  box-shadow: inset 0 2px 6px rgba(0,0,0,0.6);
}

.bidActionBtnLiar:focus-visible {
  outline: 2px solid var(--pirate-liar-red);
  outline-offset: 2px;
}
```

Tab order: "Place Bid" is first, "Call Liar" is second in the DOM — more destructive action reached last by keyboard.

---

### 8. Reveal Animation — 3D Card Flip

Each die in `DiceCup.tsx` wraps `DiceFace` in a flip wrapper:

```tsx
<div className={`${styles.dieFlipOuter} ${isRevealing ? styles.dieFlipRevealing : ''}`}>
  <div className={styles.dieFlipInner}>
    <div className={styles.dieFlipFront}>
      <DiceFace value={value} ... />
    </div>
    <div className={styles.dieFlipBack}>
      {/* leather-back placeholder */}
    </div>
  </div>
</div>
```

```css
.dieFlipOuter {
  perspective: 200px;
}

.dieFlipInner {
  position: relative;
  transform-style: preserve-3d;
  transition: transform 320ms cubic-bezier(0.4, 0, 0.2, 1);
}

/* Default: show back (hidden) */
.dieFlipInner {
  transform: rotateY(180deg);
}

/* Revealed: show front */
.dieFlipRevealing .dieFlipInner {
  transform: rotateY(0deg);
}

.dieFlipFront,
.dieFlipBack {
  position: absolute;
  top: 0; left: 0;
  backface-visibility: hidden;
  -webkit-backface-visibility: hidden;
}

.dieFlipBack {
  transform: rotateY(180deg);
}
```

Stagger per cup: `animation-delay: calc(var(--seat-index, 0) * 80ms)` on `.dieFlipInner`.

After flip completes, apply loser/winner filter:
- Loser's dice: `.dieFlipLoser` → `filter: drop-shadow(0 0 8px var(--pirate-liar-red))`
- Winner's dice: `.dieFlipWinner` → `filter: drop-shadow(0 0 8px var(--pirate-candle))`

```css
@media (prefers-reduced-motion: reduce) {
  .dieFlipInner { transition: none; }
  .dieFlipOuter { perspective: none; }
  /* Colour filters still apply — state change, not motion */
}
```

---

### 9. Active Turn Diamond Indicator

Add to `DiceCup.tsx` player name row when `isCurrentPlayer && player.active`:

```tsx
<span className={styles.cupActiveDiamond} aria-hidden="true">◆</span>
```

```css
.cupActiveDiamond {
  color: var(--pirate-candle);
  animation: diamondPulse 1.4s ease-in-out infinite;
}

@keyframes diamondPulse {
  0%, 100% { opacity: 1; }
  50%       { opacity: 0.4; }
}

@media (prefers-reduced-motion: reduce) {
  .cupActiveDiamond { animation: none; opacity: 1; }
}
```

---

## Type Requirements

- `DicePlayer.diceCount: number` — already present. Used for placeholder die count during Bidding.
- `DicePlayer.seatIndex: number` — already present. Used for stagger animation via `--seat-index` CSS custom property.
- `DiceCup` component: add `style?: React.CSSProperties` to props interface, forwarded to root div.
- No new types required. No changes to `types.ts`.

---

## Accessibility

- All new SVG decorations: `aria-hidden="true"` (cup silhouette, skull pip, active diamond, eliminated line)
- `DiceFace` aria-labels preserved: `"Die showing {value}"`, `"Wild die showing {value}"`
- Face selector buttons: `aria-label="Select face {n}"` (unchanged from existing)
- "Call Liar" button: `aria-label="Call liar"` (unchanged; uppercase is CSS only)
- Focus rings within game root: `outline: 2px solid var(--pirate-candle); outline-offset: 2px`; "Call Liar" button uses `var(--pirate-liar-red)` focus ring
- Stagger animations and die-flip wrapped in `prefers-reduced-motion: reduce` (instant, no motion; colour filters still apply)
- Active diamond pulse paused under reduced motion (static `opacity: 1`)
- Tab order: Place Bid before Call Liar in DOM

**Contrast pairs to verify before shipping** (WCAG AA):

| Foreground | Background | Minimum |
|---|---|---|
| `--pirate-parchment` `#e8dfc8` | `--pirate-felt` `#1a2e1a` | 4.5:1 |
| `--pirate-candle` `#e8a430` | `--pirate-leather-dark` `#3d1f0a` | 4.5:1 |
| `--pirate-liar-red` `#c0392b` | `--pirate-leather-dark` `#3d1f0a` | 3:1 (large/bold text) — if fails, lighten red to `#e04848` |
| `--pirate-parchment-dim` `#b8ab8a` | `--pirate-felt` `#1a2e1a` | 4.5:1 |

---

## Ship Gate — Google Fonts CSP

> **IM Fell English must not be merged to production until DevOps confirms that `fonts.googleapis.com` and `fonts.gstatic.com` are permitted in the Azure Static Web Apps CSP `style-src` and `font-src` directives.**

During development: the fallback stack `Georgia, 'Times New Roman', serif` is the active rendering until the CSP is confirmed. The game title will still render in a period-appropriate serif; the difference is subtle. The `@import` line can be present in `liarsdice.css` throughout development — fonts will simply fail gracefully if CSP blocks them.

---

## Out of Scope

- No backend changes — no C#, no SignalR events, no game actions
- No changes to `packages/ui/src/styles/tokens.css`
- No new platform components in `packages/ui/src/`
- No audio (no dice sounds, no ambient tavern noise)
- No changes to the room lobby, join code display, or player presence screens
- No changes to any other game module
- No changes to Vite build config, Aspire, or CI pipeline
- No changes to `GameStatus.tsx` beyond the title element addition
- No changes to `BidControls.tsx` beyond the Call Liar button styling
