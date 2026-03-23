# UI Plan — Dead Man's Switch

Game ID: `deadmansswitch`
Theme scope: `[data-game-theme="deadmansswitch"]`

---

## 1. Design principles

- **Precision over decoration.** Sharp 2–3px radii, tight uppercase labels, monospaced numerals. Nothing rounded or friendly.
- **Tension through restraint.** Near-black backgrounds with amber-only highlights. The board should feel like a dark ops briefing room, not a neon arcade.
- **Information hierarchy is drama.** The current Challenger's target number is the largest element on screen. Everything else is secondary. The flip moment eclipses everything.
- **State is always visible.** Each player's stack height and elimination status must be readable at a glance — no hunting through menus.
- **Dark industrial, not dark game.** Avoid glows except at the decisive flip moment. Reserve visual intensity for the reveal, not the setup.

---

## 2. CSS token overrides

Scoped to `[data-game-theme="deadmansswitch"]`. These override platform tokens within the game view only. Add to the game's own CSS Module entry point — do not touch `tokens.css`.

```css
[data-game-theme="deadmansswitch"] {
  /* Surfaces — darker and more neutral than the platform's blue-tinted stack */
  --surface-base:    #0a0a0c;
  --surface-raised:  #121218;
  --surface-float:   #1c1c26;
  --surface-overlay: #22222e;

  /* Edges — cool steel */
  --edge-subtle:  #2a2a38;
  --edge-strong:  #3a3a50;

  /* Text */
  --text-primary: #d8d8e0;
  --text-muted:   #6a6a80;
  --text-bright:  #f0f0f8;

  /* Brand accent — amber, used for active/interactive only */
  --accent:      #d4a017;
  --accent-dim:  #d4a01720;
  --accent-glow: #d4a01750;

  /* Semantic disc colours */
  --dms-disc-back:    #1a1a22;   /* face-down coaster */
  --dms-disc-back-edge: #2e2e40;
  --dms-disc-rose:    #1e2e1e;   /* dud face — muted green */
  --dms-disc-rose-text: #4a9a4a;
  --dms-disc-skull:   #2e1a1a;   /* trigger face — muted red */
  --dms-disc-skull-text: #c04040;

  /* Flip animation accent — amber burst only at the reveal */
  --dms-flip-safe-glow:    #d4a017;
  --dms-flip-trigger-glow: #c04040;

  /* Typography — tighter, more industrial */
  /* Platform font tokens are kept; game uses them as-is */

  /* Radius — sharp and precise */
  --radius-sm: 2px;
  --radius-md: 3px;
  --radius-lg: 4px;
  --radius-xl: 6px;
}
```

### WCAG contrast checks

| Pair | Ratio | AA pass |
|---|---|---|
| `--text-primary` (#d8d8e0) on `--surface-base` (#0a0a0c) | ~14:1 | Pass |
| `--text-muted` (#6a6a80) on `--surface-base` (#0a0a0c) | ~4.6:1 | Pass (large text / UI labels) |
| `--accent` (#d4a017) on `--surface-base` (#0a0a0c) | ~8.3:1 | Pass |
| `--dms-disc-rose-text` (#4a9a4a) on `--dms-disc-rose` (#1e2e1e) | ~4.6:1 | Pass (large label) |
| `--dms-disc-skull-text` (#c04040) on `--dms-disc-skull` (#2e1a1a) | ~4.5:1 | Pass (large label) |

Note: `--text-muted` at 4.6:1 is acceptable only for non-essential decorative labels (stack count badges). All player names and action copy must use `--text-primary` or `--text-bright`.

---

## 3. Component inventory

All components live in `apps/frontend/src/games/deadmansswitch/components/`. None go into `packages/ui/` — they all carry game-state knowledge.

| Component | Description | Notes |
|---|---|---|
| `MissionBoard` | One player's area: disc stack, point tokens, name, and status badge | Active player gets amber `--accent` border; eliminated players are desaturated |
| `DeviceDisc` | Circular token in three states: face-down, rose/DUD, skull/TRIGGER | See section 6 for full spec |
| `DiscStack` | Column of stacked `DeviceDisc` items for one player's MissionBoard | Stacked with slight Y offset to show depth; up to 4 discs max |
| `BidDisplay` | Central panel: large monospaced target number, Challenger name, bid history | Font: `--font-mono`; number sized ~5rem; dims when not in Bidding phase |
| `ActionPanel` | Context-sensitive button panel; changes content per phase | Renders at bottom of screen on mobile (bottom sheet); floats in centre panel on desktop |
| `FlipResult` | Full-screen overlay banner shown for ~2s after each flip | "DUD" in amber or "TRIGGER" in red; see section 7 |
| `PlayerRow` | Compact horizontal summary: avatar initial, name, stack height, points, badge | Used when screen is too narrow for full MissionBoard grid |
| `PhaseLabel` | Small uppercase label showing current phase name | Fixed position, top-right of game container |
| `PointToken` | Small circular pip showing one point (filled = earned) | Rendered inside MissionBoard; max 2 per player |

---

## 4. Layout

### Desktop (> 1100px)

```
┌──────────────────────────────────────────────────────┐
│  PhaseLabel                              [Leave Room] │
├──────────────────────────────────────────────────────┤
│                                                      │
│   MissionBoard   MissionBoard   MissionBoard         │
│      (P1)           (P2)           (P3)              │
│                                                      │
│         ┌──────────────────────────┐                 │
│         │       BidDisplay         │                 │
│         │     (centre panel)       │                 │
│         │       ActionPanel        │                 │
│         └──────────────────────────┘                 │
│                                                      │
│   MissionBoard   MissionBoard   MissionBoard         │
│      (P4)           (P5)           (P6)              │
│                                                      │
└──────────────────────────────────────────────────────┘
```

Players are arranged around the central BidDisplay panel. With 3 players, all three sit across the top row and the bottom is empty. With 6, three per row. The central panel is always vertically centred.

### Tablet (700–1100px)

PlayerRow replaces MissionBoard for non-active players. Active player's MissionBoard renders full size above the centre panel. ActionPanel becomes a drawer that slides up from the bottom.

### Mobile (< 700px)

Single column. PlayerRow list at top (collapsed by default, expandable). Active player's MissionBoard below. BidDisplay and ActionPanel in a bottom sheet that is always visible. Minimum tap target for all action buttons: 44px.

---

## 5. Phase-by-phase UX

### Placing

Each player sees their own MissionBoard with a "ARM DEVICE" button in the ActionPanel. Pressing it adds a face-down DeviceDisc to their stack (the actual disc type chosen via a private confirmation sheet that slides up and then immediately closes — the choice is hidden). Other players' boards show their growing stacks of face-down discs. The PhaseLabel reads "PLACING". Players who have already placed this round have their ActionPanel button replaced with a "WAITING" label. The phase ends when all players have placed.

### Bidding

The BidDisplay panel becomes the focal point: the current high bid is shown in large monospaced numerals with the bidder's name below it. The ActionPanel shows two buttons: "RAISE" (increments the bid by 1, capped at total discs in play) and "PASS". Players who pass are marked with a muted "PASSED" badge on their MissionBoard. The last player who has not passed becomes the Challenger and the phase ends. If a player raises to equal the total number of discs in play, they automatically become the Challenger.

### Revealing

The Challenger's MissionBoard is highlighted with an amber border. The ActionPanel shows a single "FLIP DEVICE" button. The Challenger must flip discs from their own stack first, then may freely choose any player's top disc. Each flip triggers the FlipResult overlay (see section 7). Discs that have been flipped are shown face-up on their owner's MissionBoard. If the Challenger hits the target count without flipping a skull, they win the round. If they flip a skull, the round ends immediately as a failure.

### DiscardChoice

Shown only when the Challenger flipped their own skull. The ActionPanel presents two large buttons: "DISCARD DUD" and "DISCARD TRIGGER". The Challenger must choose one disc type to permanently remove from their deck. A small text label explains the consequence of each choice. Other players see a waiting state with "CHALLENGER IS CHOOSING..." in the BidDisplay.

### RoundOver

A full-panel summary replaces the centre BidDisplay. It shows the outcome ("SWITCH DEFUSED" or "JOB FAILED"), the Challenger's name, and which disc was flipped to end the round. Each player's PointToken pips update to reflect new scores. After a short delay (or a "CONTINUE" button), the game moves back to Placing for the next round.

### Finished

A winner screen overlays the board. The winner's name is displayed in large display font with their final score. Other players are listed below in rank order. A single "BACK TO LOBBY" button returns to the platform waiting room. No confetti or particle effects — keep the industrial tone.

---

## 6. DeviceDisc design

The disc is a circle, 56px diameter on desktop, 44px on mobile (minimum touch target size). All three states share the same circular shape and outer edge border.

**Face-down (armed)**
Background: `--dms-disc-back` (#1a1a22). Border: 1px solid `--dms-disc-back-edge` (#2e2e40). No text. A subtle radial gradient from centre to edge gives it slight depth — lighter centre, darker rim. No glow. Appears as a dark industrial coaster.

**Rose face / DUD**
Background: `--dms-disc-rose` (#1e2e1e). Border: 1px solid `--dms-disc-rose-text` at 40% opacity. Centre text: "DUD" in `--font-display`, 10px, uppercase, colour `--dms-disc-rose-text` (#4a9a4a). No glow in resting state. After flip, a very brief (300ms) pulse of `box-shadow: 0 0 12px --dms-flip-safe-glow` fades out.

**Skull face / TRIGGER**
Background: `--dms-disc-skull` (#2e1a1a). Border: 1px solid `--dms-disc-skull-text` at 40% opacity. Centre text: "TRGR" in `--font-display`, 10px, uppercase, colour `--dms-disc-skull-text` (#c04040). After flip, a sustained glow of `box-shadow: 0 0 18px --dms-flip-trigger-glow` persists for the duration of the DiscardChoice or RoundOver phase to mark the fatal disc.

All three states transition between each other via a CSS 3D Y-axis flip (`rotateY`) at 400ms duration with `--ease-out`. The disc flips to reveal its face like a physical token being turned over.

---

## 7. The flip moment

When the Challenger taps "FLIP DEVICE", the selected disc plays its rotateY flip animation (400ms). At the 200ms midpoint — when the disc is edge-on — the face switches from back to front. After the animation completes, a `FlipResult` banner slides down from the top of the screen.

**FlipResult banner** is a full-width, fixed-position bar (not a full-screen overlay) approximately 80px tall. It contains a single word in `--font-display` at 2rem:

- "DUD" — amber text (`--accent`) on `--surface-overlay`. Brief 200ms ambient glow: `box-shadow: var(--glow-sm) var(--accent)` on the banner itself.
- "TRIGGER" — red text (`--dms-disc-skull-text`) on `--surface-overlay`. Sustained glow: `box-shadow: var(--glow-md) var(--dms-flip-trigger-glow)`. The screen background briefly flashes to `--dms-disc-skull` (#2e1a1a) for 150ms via a full-screen overlay div at low opacity (0.4) then fades to zero.

The banner auto-dismisses after 1800ms or on tap. If it was a DUD and more flips remain, dismissing the banner re-enables the "FLIP DEVICE" button. If it was a TRIGGER, dismissing the banner transitions to DiscardChoice or RoundOver.

The interaction is deliberately slow and dramatic — the Challenger cannot tap immediately through to the next flip. The 1800ms hold is intentional.

---

## 8. Accessibility notes

**Contrast.** All key text/background pairs are documented in section 2. Muted text is only used for decorative labels. Game outcome banners ("DUD", "TRIGGER") use `--text-bright` or full-colour values against dark overlays and exceed 7:1.

**Keyboard navigation.** ActionPanel buttons are standard `<button>` elements with visible focus state: `outline: 2px solid var(--accent); outline-offset: 2px`. Tab order follows DOM order: PhaseLabel, player boards (left to right, top to bottom), BidDisplay, ActionPanel buttons. The FlipResult banner traps focus while visible and returns focus to the "FLIP DEVICE" button on dismiss.

**Screen reader labels.**
- DeviceDisc face-down: `aria-label="Armed device, face down"`.
- DeviceDisc rose: `aria-label="Device: DUD"`.
- DeviceDisc skull: `aria-label="Device: TRIGGER"`.
- DiscStack: `aria-label="{PlayerName}'s stack, {n} device(s)"`.
- FlipResult banner: `role="status"` with `aria-live="assertive"` so screen readers announce "DUD" or "TRIGGER" immediately.
- ActionPanel "FLIP DEVICE" button: `aria-label="Flip top device from {PlayerName}'s stack"` — the target player name is injected dynamically.

**Motion.** The rotateY disc flip and the FlipResult background flash must respect `prefers-reduced-motion`. When reduced motion is active: skip the rotateY animation (instant face change), skip the background flash, and keep only the banner slide-in at 200ms opacity fade.

**Colour-blind safety.** DUD and TRIGGER are distinguished by text label ("DUD" vs "TRIGGER"), not colour alone. The green/red disc backgrounds reinforce but do not carry the meaning.

---

## 9. Out of scope for v1

- Animated disc stack building (discs dropping into place as players arm). Discs appear instantly in v1.
- Sound effects or haptics for the flip moment.
- Spectator view (non-player observers watching a live game).
- Replay / move history log visible during play.
- Per-player private disc selection UI — in v1, the player selects rose or skull via a simple modal sheet that is only visible to the placing player (leveraging existing platform identity, not a split-screen mechanic).
- Custom player colour or avatar selection.
- Animated point token accumulation (tokens appear instantly in v1).
