# UI Plan: Coup — "Inner Circle" Visual Elevation

**Status:** Agreed
**Date:** 2026-04-15
**Authors:** ux + frontend

## Design intent

Coup's "Inner Circle" theme is a Cold War private club — marble floors, dark wood panelling, candlelight, tarnished gold, and the permanent possibility of betrayal. The elevated UI should feel like sitting around a mahogany table under a single overhead light. Surfaces have grain. Gold glows rather than merely colours. Cards feel physical — heavy, matte, with a subtle sheen on the edge. Actions that decide someone's fate deserve a moment. The reference bar is Liar's Dice, which earns its atmosphere through layered surfaces, glowing materials, and kinetic moments.

## Breakpoint behaviour

| Breakpoint | Layout |
|---|---|
| < 700px | Single-column stacked. Player grid 2-col. Action panel full-width bottom sheet with border-radius on top edge only. Buttons 2-per-row flex-wrap. |
| 700–1100px | Player grid 3–4 col. Status banner + coin total side by side. Action/response panel full-width below. |
| > 1100px | Max-width ~1100px centred. Full player row. Status + action panels below. |

No structural layout changes — styling only at all breakpoints.

## Components

| Component / class | Location | New or existing |
|---|---|---|
| `.root` | `styles.module.css` | Existing — add gradient grain |
| `.gameTitle` wrapper + `.gameTitleMain` | `styles.module.css` + `CoupGame.tsx` | New class + small TSX restructure |
| `.playerCard` | `styles.module.css` | Existing — add gradient, multi-layer shadow |
| `.playerCardMe` | `styles.module.css` | Existing — add pulse keyframe |
| `.playerCardActive` | `styles.module.css` | Existing — add ambient glow |
| `.influenceCard` | `styles.module.css` | Existing — add depth gradient, `position: relative` |
| `.influenceCardOwn` | `styles.module.css` | Existing — gold gradient, Cinzel font, hover lift |
| `.influenceCardHidden` | `styles.module.css` | Existing — dark stamped emboss |
| `.influenceCardRevealed` | `styles.module.css` | Existing — grayscale + `::after` diagonal slash |
| `.statusBanner` | `styles.module.css` | Existing — gradient + top accent |
| `.actionPanel` / `.responsePanel` | `styles.module.css` | Existing — leather gradient + depth |
| `.btnPrimary` | `styles.module.css` | Existing — gold gradient + glow |
| `.btnDanger` | `styles.module.css` | Existing — crimson token, alarming hover glow |
| `.coupWarning` | `styles.module.css` | Existing — pulsing border animation |
| `.winnerBanner` / `.winnerName` | `styles.module.css` | Existing — radial glow + shimmer |
| `.exchangeCard` | `styles.module.css` | Existing — add `focus-visible` outline |

All changes are game-scoped. No extraction to `packages/ui/`.

## CSS approach

- **Scope:** Game-scoped CSS Modules only. No platform files touched.
- **Tokens:** All new tokens go inside `[data-game-theme="inner-circle"]` in `coup.css`
- **Font import:** `@import` at top of `coup.css`, outside the selector block

## New tokens to add to `coup.css`

```css
/* At top of file, outside selector */
@import url('https://fonts.googleapis.com/css2?family=Cinzel:wght@700&display=swap');

/* Inside [data-game-theme="inner-circle"] */
--font-game-display: 'Cinzel', 'Trajan Pro', Georgia, serif;

/* Atmospheric surfaces */
--coup-wood-dark:   #100c07;
--coup-wood-mid:    #1a130a;
--coup-leather:     #2a1a0e;
--coup-marble:      #1c1c28;
--coup-marble-vein: rgba(180,160,120,0.06);

/* Blood / danger */
--coup-blood:       #9b2335;
--coup-blood-glow:  rgba(155,35,53,0.4);

/* Gold material */
--coup-gold-bright: #d4a83a;
--coup-gold-base:   #b8973a;
--coup-gold-shadow: #7a5e1c;
--coup-gold-glow:   rgba(184,151,58,0.35);
```

## Animations required

```css
@keyframes inner-circle-pulse {
  0%, 100% { box-shadow: 0 0 10px var(--coup-gold-glow), 0 0 0 1px var(--coup-gold-base); }
  50%       { box-shadow: 0 0 22px var(--coup-gold-glow), 0 0 0 2px var(--coup-gold-base); }
}
/* Duration: 3s ease-in-out infinite — slow, breathing */

@keyframes coup-must {
  0%, 100% { border-color: var(--coup-gold-base); }
  50%       { border-color: var(--coup-gold-bright); box-shadow: 0 0 14px var(--coup-gold-glow); }
}
/* Duration: 1.8s ease-in-out infinite */

@keyframes influence-loss-enter {
  from { opacity: 0; transform: translateY(6px); }
  to   { opacity: 1; transform: translateY(0); }
}
/* Duration: 320ms cubic-bezier(0.4,0,0.2,1) both */

@keyframes winner-shimmer {
  0%   { background-position: -200% center; }
  100% { background-position:  200% center; }
}
/* Duration: 2.5s linear infinite — applied via background-clip: text */
```

All `@keyframes` must have `prefers-reduced-motion` overrides:

```css
@media (prefers-reduced-motion: reduce) {
  .playerCardMe       { animation: none; }
  .coupWarning        { animation: none; }
  .influenceLossPanel { animation: none; }
  .winnerName         { animation: none; background: none; -webkit-text-fill-color: var(--coup-gold-bright); }
}
```

## Key styling notes

### `.root` background
Multi-layer gradient with faint diagonal grain pattern using `repeating-linear-gradient`. Dark marble base (`--coup-marble`) with subtle vein overlay (`--coup-marble-vein`).

### `.gameTitle` banner
Full-width block. Gold `border-top` and `border-bottom` rules. `::before` / `::after` pseudo-elements rendering ❖ diamond decorators. `font-family: var(--font-game-display)`. Mirrors `liarsdice.css` anchor treatment exactly.

### `.influenceCard` constraints (non-negotiable)
- `min-height: 80px` — required for Cinzel serif legibility on "Ambassador"
- `position: relative` — required for `::after` diagonal slash on revealed cards
- Role name `font-size` floor: `0.75rem` (not 0.72rem)

### `.influenceCardRevealed` diagonal slash
Via `::after` pseudo-element with `position: absolute`, full-width diagonal line (CSS border or `transform: rotate`). Companion: `filter: grayscale(100%)` on the card itself.

### `.winnerName` shimmer
Use `@supports (-webkit-background-clip: text)` wrapper. Safe fallback: `color: var(--coup-gold-bright)` always set first on the bare class. Inside the `@supports` block: gold gradient with `background-size: 200%`, `-webkit-background-clip: text`, `-webkit-text-fill-color: transparent`, `background-clip: text`.

### `.btnDanger` hover
Match Liar's Dice liar button exactly: `background: color-mix(in srgb, var(--coup-blood) 30%, transparent)`, `box-shadow: 0 0 20px var(--coup-blood-glow)`, `text-shadow: 0 0 12px rgba(220,80,80,0.6)`.

## TSX changes required

Two minimal changes in `CoupGame.tsx`:

1. **Header restructure** — wrap the existing `<span className={styles.headerTitle}>` in a block-level `<div className={styles.gameTitle}>` (or `<header>`). Inner text in a `<span className={styles.gameTitleMain}>`. Required for `::before`/`::after` banner decorators.

2. **Coin emoji accessibility** — wrap `💰` in `<span role="img" aria-label="coins">💰</span>` wherever the coin count is rendered.

## Type requirements

No new types needed. All existing `InfluenceCard`, `CoupPlayer`, and game state fields are sufficient:
- `card.revealed === false && card.character !== null` → `.influenceCardOwn`
- `card.character === null` → `.influenceCardHidden`
- `card.revealed === true` → `.influenceCardRevealed`
- `state.winner` → `.winnerBanner` / `.winnerName`

## Accessibility

| Requirement | Detail |
|---|---|
| Contrast — gold on dark | `--coup-gold-bright #d4a83a` on `#0f0f12` ≈ 5.1:1 — passes AA |
| Contrast — gold shadow on dark | `--coup-gold-shadow #7a5e1c` on dark — large text / display only (≥3:1 for large text) |
| Contrast — blood on dark | Border / glow use only — not body text |
| Focus states | All existing `.btn:focus-visible` correct. Add `.exchangeCard:focus-visible { outline: 2px solid var(--color-primary); outline-offset: 2px; }` |
| Coin emoji | Wrap in `<span role="img" aria-label="coins">` |
| Reduced motion | All four `@keyframes` must have overrides in `prefers-reduced-motion: reduce` block |

## Out of scope

- No platform component extractions (`packages/ui/`)
- No API, SignalR, or game logic changes
- No new TypeScript types
- No changes to other games
- Status text and body copy remain in `var(--font-body)` (Outfit) — Cinzel is display-only
