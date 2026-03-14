---
name: theme
description: Meepliton design system — Blade Runner aesthetic, CSS tokens, fonts, components, mobile patterns. Load when building any UI screen or component.
user-invocable: false
---

See full design system in `.claude/skills/THEME.md` (legacy reference — content below is the authoritative summary).

## Design language

**Blade Runner night city.** Dark surfaces, neon glows, gold brand accent.
- Dark theme: near-black surfaces with neon on dark — 2am in the rain
- Light theme: neon on wet concrete — mid-tone grey, same vivid neons

**Three rules:**
1. Neon on dark — never neon on white
2. Gold (`--accent`) = important, use sparingly
3. Cyan (`--neon-cyan`) = interactive; Magenta (`--neon-magenta`) = primary CTA

## Fonts — always all three

```html
<link href="https://fonts.googleapis.com/css2?family=Orbitron:wght@700;800;900&family=Share+Tech+Mono&family=Outfit:wght@300;400;500;600&display=swap" rel="stylesheet">
```

| Token | Family | Use |
|---|---|---|
| `var(--font-display)` | Orbitron | Headings, labels, buttons, codes |
| `var(--font-mono)` | Share Tech Mono | Data, scores, codes, metadata |
| `var(--font-body)` | Outfit | Body copy, descriptions |

## Core tokens

```css
--surface-base / --surface-raised / --surface-float / --surface-overlay
--edge-subtle / --edge-strong
--text-muted / --text-primary / --text-bright
--accent / --accent-dim / --accent-glow
--neon-cyan / --neon-magenta / --neon-orange
--glow-sm / --glow-md / --glow-lg / --glow-inset
--space-1 … --space-8  (4px–32px, 8-point scale)
--radius-sm / --radius-md / --radius-lg / --radius-xl / --radius-pill
--font-display / --font-mono / --font-body
```

## CSS rules — always enforced

1. All colours via tokens — no hex in component CSS
2. All spacing via `--space-*`
3. All radii via `--radius-*`
4. All fonts via `--font-*`
5. Glows: `box-shadow: var(--glow-sm) var(--neon-cyan)`
6. Hover transitions: `200ms ease`; panel animations: `320ms cubic-bezier(0.4,0,0.2,1)`
7. CSS Modules for game components; global class names for platform components

## Mobile patterns

- **< 700px**: single column, bottom sheet / FAB instead of sidebar, 44px min tap targets
- **700–1100px**: two column where useful, drawer instead of sidebar
- **> 1100px**: full layout, max-width container centred

```css
/* Bottom sheet */
.bottom-sheet { position: fixed; left:0; right:0; bottom:0;
  background: var(--surface-overlay); border-top: 1px solid var(--edge-strong);
  border-radius: 16px 16px 0 0; transform: translateY(100%);
  transition: transform 320ms cubic-bezier(0.4,0,0.2,1); }
.bottom-sheet.open { transform: translateY(0); }

/* FAB */
.fab { position: fixed; bottom: 22px; right: 18px;
  border: 1.5px solid var(--neon-cyan); color: var(--neon-cyan);
  font-family: var(--font-display); height: 44px; padding: 0 16px;
  border-radius: var(--radius-pill); background: var(--surface-overlay); }
```

## New screen checklist

- [ ] `data-theme="dark"` on `<html>` (default)
- [ ] Google Fonts imported
- [ ] Atmospheric `body::before` gradient
- [ ] All buttons use `.btn-primary` or `.btn-secondary`
- [ ] Inputs use standard pattern with `:focus` gold highlight
- [ ] Mobile: bottom sheet or drawer below 700px
- [ ] All tap targets ≥ 44px
- [ ] `position: relative; z-index: 1` on all content wrappers

Full patterns in `.claude/skills/THEME.md`.
