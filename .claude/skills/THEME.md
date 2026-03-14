# Meepliton — Platform Theme & Design System Skill
**Attach this file when building any Meepliton UI — lobby pages, game boards, components, or new screens.**

---

## Design Philosophy

Meepliton's visual identity is derived from the Skyline game. There is **one aesthetic** used throughout — both dark and light modes are intentionally dark. The dark theme is a Blade Runner night city; the light theme is neon signs on wet concrete (mid-tone grey, dark enough for neon to glow).

This is not a generic web app. Every component should feel like it belongs in a rain-soaked cyberpunk cityscape at 2am.

**The three rules:**
1. Neon glows on dark surfaces — never neon on white
2. Gold (`--accent`) is the primary brand colour — it means "important"
3. Cyan (`--neon-cyan`) is for interactive/informational; Magenta (`--neon-magenta`) is for primary CTAs

---

## Fonts

Always import these three from Google Fonts:
```html
<link href="https://fonts.googleapis.com/css2?family=Orbitron:wght@700;800;900&family=Share+Tech+Mono&family=Outfit:wght@300;400;500;600&display=swap" rel="stylesheet">
```

| Token | Family | Use |
|---|---|---|
| `var(--font-display)` | Orbitron | Headings, labels, buttons, codes, names |
| `var(--font-mono)` | Share Tech Mono | Data, prices, codes, metadata, status |
| `var(--font-body)` | Outfit | Body copy, descriptions, paragraph text |

**Never use Inter, Roboto, or system fonts.**

---

## CSS Token Reference

### Apply tokens with `data-theme` on `<html>`
```html
<html data-theme="dark">   <!-- default: Blade Runner night -->
<html data-theme="light">  <!-- neon on wet concrete -->
```

### Full token set (copy verbatim into `:root`)
```css
:root {
  /* Surfaces — darkest to lightest */
  --surface-base:    #03060b;   /* page background */
  --surface-raised:  #070d19;   /* board, inputs */
  --surface-float:   #0a1220;   /* cards, chips */
  --surface-overlay: #0d1828;   /* panels, sidebar, header */

  /* Edges */
  --edge-subtle:  #132030;      /* hairlines, empty tiles */
  --edge-strong:  #1c3050;      /* active borders */

  /* Text */
  --text-muted:   #3d5a78;      /* labels, placeholders */
  --text-primary: #c0d8f0;      /* body copy */
  --text-bright:  #e8f6ff;      /* headings, values */

  /* Accent — gold/amber */
  --accent:       #f0c040;
  --accent-dim:   #f0c04018;
  --accent-glow:  #f0c04055;

  /* Blade Runner neons */
  --neon-cyan:    #00d4ff;
  --neon-magenta: #ff2060;
  --neon-orange:  #ff6010;

  /* Glow intensities — combine with a color: 0 0 8px #color */
  --glow-sm:    0 0 8px;
  --glow-md:    0 0 18px;
  --glow-lg:    0 0 35px;
  --glow-inset: inset 0 0 10px;

  /* Typography */
  --font-display: 'Orbitron', sans-serif;
  --font-mono:    'Share Tech Mono', monospace;
  --font-body:    'Outfit', sans-serif;
}

[data-theme="light"] {
  --surface-base:    #1e2530;
  --surface-raised:  #252d3a;
  --surface-float:   #2c3545;
  --surface-overlay: #323d50;
  --edge-subtle:  #3a4a60;
  --edge-strong:  #4a5e78;
  --text-muted:   #6a88a8;
  --text-primary: #b8d0e8;
  --text-bright:  #ddeeff;
  --accent-glow:  #f0c04044;
  /* Glows slightly softer — overcast diffuses light */
  --glow-sm:  0 0 6px;
  --glow-md:  0 0 14px;
  --glow-lg:  0 0 28px;
  --glow-inset: inset 0 0 8px;
  /* Neons unchanged — same vivid hues in both themes */
}
```

---

## Atmospheric Background

Always add this to `body` — it creates the city-glow depth:
```css
body::before {
  content: ''; position: fixed; inset: 0; pointer-events: none; z-index: 0;
  background:
    radial-gradient(ellipse 90% 40% at 15% 0%,   #001830 0%, transparent 60%),
    radial-gradient(ellipse 70% 50% at 85% 100%,  #200040 0%, transparent 55%),
    radial-gradient(ellipse 50% 30% at 50% 50%,   #080018 0%, transparent 70%);
}
[data-theme="light"] body::before {
  background:
    radial-gradient(ellipse 90% 40% at 15% 0%,  #1a2840 0%, transparent 60%),
    radial-gradient(ellipse 70% 50% at 85% 100%, #28183a 0%, transparent 55%);
  opacity: .7;
}
body > * { position: relative; z-index: 1; }
```

---

## Header

```css
header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 0 28px; height: 58px;
  background: var(--surface-overlay);
  border-bottom: 1px solid var(--edge-subtle);
  box-shadow: 0 1px 0 var(--edge-strong), 0 4px 30px rgba(0,0,0,.4);
  position: sticky; top: 0; z-index: 50;
  backdrop-filter: blur(16px);
}
@media (max-width: 600px) { header { padding: 0 16px; } }
```

**Logo pattern:**
```html
<a class="logo" href="/">MEE<em>PL</em>Y</a>
```
```css
.logo {
  font-family: var(--font-display); font-weight: 900;
  font-size: 1.25rem; letter-spacing: 6px; color: var(--accent);
  text-shadow: var(--glow-sm) var(--accent-glow), var(--glow-lg) var(--accent-dim);
  text-decoration: none;
}
.logo em { /* highlight middle letters in cyan */
  color: var(--neon-cyan); font-style: normal;
  text-shadow: var(--glow-sm) var(--neon-cyan),
               var(--glow-md) color-mix(in srgb, var(--neon-cyan) 40%, transparent);
}
```

---

## Buttons

**Primary CTA** (magenta, pulsing animation):
```css
@keyframes btn-primary-pulse {
  0%, 100% { box-shadow: 0 0 10px var(--neon-magenta), 0 0 22px color-mix(in srgb, var(--neon-magenta) 30%, transparent); }
  50%       { box-shadow: 0 0 18px var(--neon-magenta), 0 0 40px color-mix(in srgb, var(--neon-magenta) 40%, transparent); }
}
.btn-primary {
  background: transparent; color: var(--neon-magenta);
  border: 1.5px solid var(--neon-magenta); text-shadow: 0 0 12px var(--neon-magenta);
  animation: btn-primary-pulse 2.8s ease-in-out infinite;
}
.btn-primary:hover { background: var(--neon-magenta); color: var(--surface-base); text-shadow: none; animation: none; }
```

**Secondary** (cyan ghost):
```css
.btn-secondary {
  background: transparent; color: var(--neon-cyan);
  border: 1.5px solid var(--neon-cyan);
  box-shadow: var(--glow-sm) color-mix(in srgb, var(--neon-cyan) 25%, transparent);
  text-shadow: 0 0 10px var(--neon-cyan);
}
.btn-secondary:hover { background: var(--neon-cyan); color: var(--surface-base); text-shadow: none; }
```

**Button base (apply to both):**
```css
.btn {
  display: inline-flex; align-items: center; gap: 7px; padding: 10px 22px;
  font-family: var(--font-display); font-weight: 700; font-size: .82rem;
  letter-spacing: 1.5px; text-transform: uppercase;
  border-radius: 8px; cursor: pointer; transition: all 200ms ease;
  white-space: nowrap; text-decoration: none;
}
.btn-sm { padding: 7px 14px; font-size: .74rem; }
.btn:disabled { opacity: .35; cursor: not-allowed; animation: none !important; box-shadow: none !important; }
```

---

## Inputs

```css
input, select {
  background: var(--surface-float); border: 1px solid var(--edge-strong);
  color: var(--text-primary); padding: 9px 12px;
  font-family: var(--font-mono); font-size: .78rem;
  border-radius: 8px; outline: none; width: 100%;
  transition: border-color 200ms, box-shadow 200ms;
}
input:focus, select:focus { border-color: var(--accent); box-shadow: 0 0 0 2px var(--accent-dim); }
input::placeholder { color: var(--text-muted); }
```

**Join code input** (display-font, wide letter-spacing):
```css
.join-input {
  font-family: var(--font-display); font-weight: 700;
  font-size: 1rem; letter-spacing: 6px; text-transform: uppercase;
  border: 1.5px solid var(--edge-strong);
}
.join-input:focus {
  border-color: var(--neon-cyan);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--neon-cyan) 20%, transparent),
              inset 0 0 12px color-mix(in srgb, var(--neon-cyan) 5%, transparent);
}
```

---

## Cards

**Panel (form container, options):**
```css
.panel {
  background: var(--surface-overlay); border: 1px solid var(--edge-subtle);
  border-radius: 8px; padding: 20px;
}
.panel-title {
  font-family: var(--font-display); font-weight: 700; font-size: .85rem;
  letter-spacing: 2px; color: var(--accent); margin-bottom: 16px; text-transform: uppercase;
}
```

**Room card** (lobby list item):
```css
.room-card {
  display: grid; grid-template-columns: auto 1fr auto;
  align-items: center; gap: 14px; padding: 14px 16px;
  border-radius: 10px; background: var(--surface-float);
  border: 1px solid var(--edge-subtle); cursor: pointer;
  transition: all 200ms; text-decoration: none; color: inherit;
}
.room-card:hover {
  border-color: var(--neon-cyan); transform: translateX(3px);
  box-shadow: var(--glow-sm) color-mix(in srgb, var(--neon-cyan) 20%, transparent);
}
.room-code {
  font-family: var(--font-display); font-weight: 900;
  font-size: 1rem; letter-spacing: 4px; color: var(--neon-cyan);
  text-shadow: var(--glow-sm) var(--neon-cyan);
}
```

**Game card** (catalogue grid item):
```css
.game-card {
  background: var(--surface-float); border: 1px solid var(--edge-subtle);
  border-radius: 10px; overflow: hidden; cursor: pointer;
  transition: all 200ms; text-decoration: none; color: inherit;
}
.game-card:hover { border-color: var(--accent); transform: translateY(-3px);
  box-shadow: var(--glow-sm) var(--accent-glow), var(--glow-md) var(--accent-dim); }
.game-thumb {
  aspect-ratio: 16/9; background: var(--surface-raised);
  display: flex; align-items: center; justify-content: center;
}
.game-thumb-title {
  font-family: var(--font-display); font-weight: 900;
  font-size: 1.15rem; letter-spacing: 5px; color: var(--accent);
  text-shadow: var(--glow-sm) var(--accent-glow);
}
```

---

## Status Badges

```css
.status-badge {
  font-family: var(--font-mono); font-size: .58rem; font-weight: 700;
  padding: 3px 8px; border-radius: 999px; text-transform: uppercase; letter-spacing: .5px;
}
.status-waiting  { color: var(--neon-cyan);    background: color-mix(in srgb, var(--neon-cyan) 12%, transparent);    border: 1px solid color-mix(in srgb, var(--neon-cyan) 35%, transparent); }
.status-playing  { color: var(--neon-magenta); background: color-mix(in srgb, var(--neon-magenta) 12%, transparent); border: 1px solid color-mix(in srgb, var(--neon-magenta) 35%, transparent); }
.status-finished { color: var(--text-muted);   background: color-mix(in srgb, var(--text-muted) 12%, transparent);   border: 1px solid var(--edge-subtle); }
```

---

## Mobile Patterns

### Bottom sheet (replaces modals on mobile)
```css
.bottom-sheet {
  position: fixed; left: 0; right: 0; bottom: 0; z-index: 120;
  background: var(--surface-overlay); border-top: 1px solid var(--edge-strong);
  border-radius: 16px 16px 0 0; max-height: 85vh; overflow-y: auto;
  transform: translateY(100%); transition: transform 320ms cubic-bezier(0.4,0,0.2,1);
}
.bottom-sheet.open { transform: translateY(0); }
/* Handle */
.sheet-handle {
  width: 44px; height: 4px; border-radius: 2px; margin: 10px auto 0; cursor: pointer;
  background: var(--neon-cyan);
  box-shadow: 0 0 10px var(--neon-cyan), 0 0 20px color-mix(in srgb, var(--neon-cyan) 40%, transparent);
  opacity: .6;
}
```

### FAB (Floating Action Button)
```css
@keyframes fab-breathe {
  0%, 100% { box-shadow: 0 0 12px var(--neon-cyan), 0 4px 20px rgba(0,0,0,.5); }
  50%       { box-shadow: 0 0 24px var(--neon-cyan), 0 0 40px color-mix(in srgb, var(--neon-cyan) 30%, transparent), 0 4px 20px rgba(0,0,0,.5); }
}
.fab {
  position: fixed; bottom: 22px; right: 18px; z-index: 70;
  height: 44px; padding: 0 16px; border-radius: 999px;
  background: var(--surface-overlay); border: 1.5px solid var(--neon-cyan);
  color: var(--neon-cyan); font-family: var(--font-display); font-weight: 700;
  font-size: .62rem; letter-spacing: 1.5px; text-transform: uppercase;
  animation: fab-breathe 3s ease-in-out infinite;
  display: flex; align-items: center; gap: 6px;
}
```

### Right-side drawer
```css
.drawer {
  position: fixed; top: 0; right: 0; bottom: 0; width: min(340px, 92vw);
  background: var(--surface-overlay); border-left: 1px solid var(--edge-strong);
  z-index: 90; overflow-y: auto; padding: 20px 18px;
  transform: translateX(100%); transition: transform 320ms cubic-bezier(0.4,0,0.2,1);
}
.drawer.open { transform: translateX(0); }
```

### Mobile breakpoint pattern (use 700px for game layout, 600px for general)
```css
/* On mobile: sidebar becomes drawer, desktop layout collapses */
@media (max-width: 700px) {
  .side-col   { display: none; }           /* hide sidebar */
  .fab-stats  { display: flex; }           /* show FAB */
  #action-area { display: none; }          /* hide desktop actions */
  /* bottom-sheet handles actions instead */
}
@media (min-width: 701px) {
  .fab-stats     { display: none !important; }
  .bottom-sheet  { display: none !important; }
  .drawer        { display: none !important; }
}
```

---

## Scanline Effect (opt-in)

Add class `scanlines` to any element for a CRT texture:
```css
@keyframes scanlines-scroll { from { background-position: 0 0; } to { background-position: 0 8px; } }
.scanlines::after {
  content: ''; position: absolute; inset: 0; pointer-events: none; z-index: 10;
  background: repeating-linear-gradient(
    to bottom, transparent 0px, transparent 3px,
    rgba(0,0,0,.12) 3px, rgba(0,0,0,.12) 4px
  );
  animation: scanlines-scroll 0.5s linear infinite;
  border-radius: inherit;
}
[data-theme="light"] .scanlines::after { opacity: .3; }
```

---

## Join Code Display

Large, readable join code — used on the room waiting screen:
```css
.join-code {
  font-family: var(--font-display); font-weight: 900;
  font-size: clamp(2rem, 8vw, 3.5rem);
  letter-spacing: 12px; color: var(--accent);
  text-shadow: var(--glow-sm) var(--accent-glow), var(--glow-lg) var(--accent-dim);
  text-align: center; user-select: all;
}
```

---

## Theme Toggle

```html
<button class="theme-toggle" onclick="toggleTheme()">☀</button>
```
```css
.theme-toggle {
  width: 30px; height: 30px; border-radius: 50%;
  border: 1px solid var(--edge-strong); background: none; color: var(--text-muted);
  cursor: pointer; font-size: 1rem; display: flex; align-items: center; justify-content: center;
  transition: all 200ms;
}
.theme-toggle:hover { border-color: var(--accent); color: var(--accent); box-shadow: var(--glow-sm) var(--accent-glow); }
```
```javascript
function toggleTheme() {
  const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
  document.documentElement.setAttribute('data-theme', isDark ? 'light' : 'dark');
  document.querySelector('.theme-toggle').textContent = isDark ? '🌙' : '☀';
  localStorage.setItem('meepliton-theme', isDark ? 'light' : 'dark');
}
// On load:
const saved = localStorage.getItem('meepliton-theme');
if (saved) document.documentElement.setAttribute('data-theme', saved);
```

---

## Toast Notifications

```html
<div class="toast" id="toast"></div>
```
```css
.toast {
  position: fixed; bottom: 22px; right: 22px; z-index: 999;
  background: var(--surface-overlay); border: 1px solid var(--accent); border-radius: 8px;
  padding: 12px 18px; font-family: var(--font-mono); font-size: .76rem; color: var(--text-primary);
  max-width: 280px; transform: translateY(80px); opacity: 0; transition: all 320ms ease;
  pointer-events: none;
}
.toast.show { transform: translateY(0); opacity: 1; }
@media (max-width: 500px) { .toast { left: 16px; right: 16px; bottom: 16px; max-width: none; } }
```
```javascript
let _toastTimer;
function toast(msg) {
  const el = document.getElementById('toast');
  el.textContent = msg; el.classList.add('show');
  clearTimeout(_toastTimer);
  _toastTimer = setTimeout(() => el.classList.remove('show'), 2800);
}
```

---

## Animations

**Fade-in (screen transitions):**
```css
@keyframes fade-in { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: none; } }
.fade-in { animation: fade-in .25s cubic-bezier(0.4,0,0.2,1); }
```

**Staggered list reveal:**
```css
@keyframes stagger-in { from { opacity:0; transform:translateY(8px); } to { opacity:1; transform:none; } }
/* Apply to children with increasing animation-delay: 0ms, 60ms, 120ms, 180ms... */
```

---

## Checklist for any new screen

- [ ] `data-theme="dark"` on `<html>` (default)
- [ ] Google Fonts import (Orbitron + Share Tech Mono + Outfit)
- [ ] Atmospheric background (`body::before` gradient)
- [ ] Sticky header with `.logo` (gold) and `.logo em` (cyan)
- [ ] Theme toggle button wired up
- [ ] `max-width: 900px` container, centred
- [ ] All buttons use `.btn-primary` or `.btn-secondary` patterns
- [ ] All inputs use the standard pattern with `:focus` gold highlight
- [ ] Mobile: bottom sheet OR drawer for panels/actions below 700px
- [ ] Mobile: `min-height: 44px` on all tap targets
- [ ] Toast element present in DOM
- [ ] `position: relative; z-index: 1` on all content wrappers (above `body::before`)
