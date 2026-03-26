# Owner TODO

Actions that only you can take. Agents add items here when they are blocked or need a decision. Delete or check items off when done.

---

## Urgent

_Nothing urgent yet._

## Needs your decision

- [x] **2026-03-22** Dead Man's Switch — OQ-DMS-02: when the Challenger hits an opponent's skull, does the skull owner actively choose which of the Challenger's discs to discard, or does the server pick randomly and advance immediately? **Resolved 2026-03-23: server picks randomly. `OpponentDiscardChoice` phase and `ChooseDiscardForChallenger` action removed. `OpponentDiscardOwnerId` state field removed.** (analyst)

- [x] **2026-03-14** Choose avatar storage strategy for v1: **Gravatar** (derive from email hash). (analyst) — decided 2026-03-14

- [x] **2026-03-14** UX gap analysis found that `POST /api/rooms/{roomId}/transfer-host` is in the requirements (§11.1) but may not be implemented in the backend yet. Confirm whether it exists before story-028 (host transfer UI) is scheduled. (ux/backend) — **confirmed 2026-03-19: endpoint does not exist; story-028 includes backend implementation.**

- [x] **2026-03-14** Decide what the second game module should be. **Liar's Dice** — dice-based bluffing game, clean modern pirate theme (not cheesy). Lobby and header keep Skyline theme; game room controls its own look. Proves Aspire orchestration, per-game migrations, and dynamic game loading. (analyst) — decided 2026-03-14

- [x] **2026-03-14** `currentPlayerId` surfacing: **client-side JSONB extraction** — frontend reads `currentPlayerId` from game state JSON (simpler for v1). Blocks story-018. (analyst) — decided 2026-03-14

## Setup / credentials

- [ ] **2026-03-14** Configure email provider: **SendGrid free tier** chosen. Add `SENDGRID_API_KEY` to GitHub Secrets and Azure Container App environment variables. Blocks stories 001, 003 (end-to-end email flow only — backend code can be implemented now). (analyst)

- [ ] **2026-03-14** Create a Google OAuth 2.0 client in Google Cloud Console. Add `Client ID` and `Client Secret` to Azure Container App environment variables (or GitHub Secrets for CI). Blocks story-004. (analyst)

- [ ] **2026-03-14** Provision an Azure Application Insights resource and add `APPLICATIONINSIGHTS_CONNECTION_STRING` to GitHub Secrets and the Container App environment. Blocks story-023. (devops)

---

## Ally review — pending decisions

- [x] **2026-03-15** Liar's Dice ally fixes — merged in PR #29. (ally)

- [x] **2026-03-17** Story-007 and story-025 ally fixes — merged in PR #29. (ally)

- [x] **2026-03-19** Story-005 ally and docs fixes — merged in PR #29. (ally/docs)

- [x] **2026-03-17** `prefers-reduced-motion` guard — added to `tokens.css` in PR #28. (ux)

- [ ] **2026-03-17** `ally` agent has no `Bash` tool and cannot run `git commit` — this has now happened multiple times. Decision needed: add `Bash` to ally's tool list so it can commit its own edits, or formally establish that the session owner always commits ally's changes (and document this in CLAUDE.md). (trainer)

- [ ] **2026-03-15** Manual contrast verification needed for pirate theme: `--color-text-muted: #a0b8cc` on `--color-background: #0d1b2a`. Calculated ratio ~4.7:1 — verify with WebAIM Contrast Checker. (ally)

- [ ] **2026-03-17** Manual contrast verification for story-007 ProfilePage: `--text-primary` (#c0d8f0 dark / #b8d0e8 light) on `--surface-raised` (#070d19 dark / #252d3a light) at 0.65–0.7rem labels. Requires 4.5:1 WCAG AA. (ally)

- [ ] **2026-03-19** Manual contrast verification for story-005 sign-in methods: (1) `--neon-orange` (`#ff6010`) on `--surface-raised` at 0.65rem — switch to `--status-warning` if fails; (2) `--text-muted` (`#3d5a78`) on `--surface-base` at 0.8rem — switch to `--text-primary` if fails. (ally)

- [ ] **2026-03-19** Story-020/028 ally review — presence dot is colour-only: the green/grey dot conveys Online/Offline state visually without a text label visible to sighted users. Screen readers get the `.sr-only` text, but colour-blind sighted users cannot distinguish states. A visible, short text label ("Online"/"Offline") next to the dot is needed. This requires a layout change to the `PlayerPresence` and `RoomWaitingScreen` player rows — flagged to `ux` to design before implementation. (ally)

- [ ] **2026-03-19** Story-020/028 ally review — Start game button in `RoomWaitingScreen` has no CSS class and relies on the browser default focus ring. Verify that the browser default focus outline meets 3:1 contrast against `--surface-base` in Chrome, Safari, and Firefox. If any browser fails, add a `btn` class (or equivalent) to the button and style a focus ring matching the gold `outline: 2px solid var(--accent)` pattern used on the remove/transfer buttons. Flagged to `ux`/`frontend` for decision. (ally)

- [ ] **2026-03-19** Story-020/028 ally review — manual contrast verification needed: `--text-primary` (#c0d8f0) blended at 0.55 opacity over `--surface-raised` (#070d19) for disconnected player names. Calculated effective colour ≈ #6c7d8f, ratio ≈ 4.6:1 against `#070d19`. This is marginal AA pass at 0.9rem (≈14.4px normal weight, requires 4.5:1). Verify with WebAIM Contrast Checker using the blended hex. If it fails in practice, raise disconnected opacity to 0.65 minimum. (ally)

- [ ] **2026-03-19** F'That chip amber colour (#c8840a) fails WCAG AA 4.5:1 for normal text on `--surface-raised` (#070d19) — calculated ratio is ~3.0:1. Ask `ux` to lighten the chip amber token to approximately #e09030 (or verify exact value in WebAIM) before the frontend agent implements the chip HUD. This blocks ally sign-off on the F'That UI. (ally)

- [ ] **2026-03-19** F'That brand name "F'That" and button label "F'THAT" — if the platform opens to a broader or younger audience in the future, the name should be reviewed for appropriateness. No change needed for the current friend-group context. Product decision only if audience scope changes. (ally)

- [ ] **2026-03-19** Manual verification needed for F'That game board before ship: (1) `--text-muted` (#3d5a78) must not be used for any game-information text (opponent chip "???" label, deck count, chip count) — use `--text-primary` instead; (2) run full board through Chrome DevTools > Rendering > Emulate Vision Deficiency for Deuteranopia and Protanopia; (3) verify 200% zoom does not clip the dual action button row ("F'THAT" + "FINE, I'LL TAKE IT"). (ally)

- [ ] **2026-03-24** Dead Man's Switch — manual contrast verification needed: (1) `--color-text-muted` was `#6a6a80` (≈4.0:1 on `#0a0a0c`, fails AA) — changed to `#8a8aa8` (calculated ≈5.3:1) — verify exact ratio with WebAIM Contrast Checker using `#8a8aa8` on `#0a0a0c`; (2) `--color-primary: #d4a017` used as text colour in `.infoValue`, `.badgeTurn`, `.pointStar`, `.finishedTitle` — calculate ratio against `--color-surface-raised: #1c1c26` (≈7.5:1 estimated, should pass) — verify with WebAIM; (3) `#f87171` (updated red) as text on `#121218` — calculated ≈6.1:1, verify with WebAIM. (ally)

- [ ] **2026-03-24** Dead Man's Switch — colour-only state indicator: the `.playerCardActive` amber glow distinguishes whose turn it is visually. The "ACTIVE" badge text label is present in the DOM and provides a non-colour signal — confirm it is visible and readable at small viewport widths (≤375px) where the badge may wrap or be clipped. (ally)

- [ ] **2026-03-24** F'That — `--color-primary: #e85d04` is used as a text colour on `.playerNameBold`, `.winnerBanner`, and `.scoreRowWinner td`. Against the platform dark `--surface-base` (#03060b) the calculated ratio is ≈3.6:1, failing AA 4.5:1 for normal text. The UX agent previously flagged a related amber contrast issue (see 2026-03-19 entry above). Resolution: ask `ux` to either (a) lighten `--color-primary` to ≥ #f07030 in the F'That theme, or (b) use `--text-primary` for those text uses and keep the orange only for borders and button fills. This blocks ally sign-off on F'That. (ally)

- [ ] **2026-03-24** F'That — `--text-muted: #3d5a78` (platform dark token) is used on `.playerChips` (chip count text), `.metaLabel`, `.cardLabel`, `.actionLabel`, and `.noCards`. Calculated ratio against `--surface-base: #03060b` is ≈2.5:1 — fails AA badly. These elements carry game-critical information (chip count, deck count). F'That does not override `--text-muted` in its theme file. Ask `ux` whether to: (a) add `--color-text-muted` override in `fthat.css` at a passing value, or (b) change the component CSS classes that carry information to use `--color-text` / `--text-primary` instead of the muted token. Blocks ally sign-off on F'That. (ally)

- [ ] **2026-03-24** F'That — focus management after action: when a player clicks "F'THAT" or "Fine, I'll Take It", the action panel conditionally disappears on the next render (it is only shown when `isMyTurn`). Focus is dropped to `<body>`. No focus return target is set. Ask `frontend` to capture a ref to the players list or card info row and call `.focus()` on it after dispatch, to prevent focus loss. (ally)

- [ ] **2026-03-24** Dead Man's Switch — focus management after action: same issue as F'That above — the action panel rerenders or disappears after dispatch and focus is lost. Ask `frontend` to add a focus return ref. (ally)

- [ ] **2026-03-24** Manual screen reader verification needed for both games before ship: (1) VoiceOver/NVDA should announce the turn-change live region each time `currentPlayerIndex` changes; (2) the flip notification in Dead Man's Switch should announce via `role="status"` on each new flip; (3) the score table in F'That should be navigable with table-navigation keys. Recommended tools: VoiceOver (macOS), NVDA (Windows), axe DevTools browser extension. (ally)

- [ ] **2026-03-24** Docs sweep for story-030 (Dead Man's Switch + F'That): the session owner referenced three resolved ally items by the labels FTHAT-MUST-01, FTHAT-MUST-02, and DMS-MUST-01. Those labels do not appear anywhere in this file or in the specs. The six 2026-03-24 ally items in this file are all still open. Confirm which three correspond to those labels, check them off here, and confirm the remaining items are deferred (not blocking the PR). (docs)

- [ ] **2026-03-26** Profile-images ally review — commit two must-fix edits: (1) `packages/ui/src/styles/tokens.css` — added `.icon-btn:focus-visible` rule (WCAG 2.4.7 focus visible); (2) `packages/ui/src/components/Avatar.tsx` — changed initials `fontSize` from raw pixel numbers to `rem` strings (WCAG 1.4.4 resize text). No other files changed. Commit message: `fix(a11y): focus ring for icon-btn, rem font sizes in Avatar`. (ally)

- [ ] **2026-03-26** Profile-images ally review — `TurnIndicator` has no flex layout on `.turn-indicator`. The avatar and label will stack or flow awkwardly without `display: flex; align-items: center; gap: var(--space-2)`. Ask `ux` to confirm layout intent and ask `frontend` to add it. Non-blocking (visual only). (ally)

- [ ] **2026-03-26** Profile-images ally review — manual contrast verification: Avatar initials use `--text-bright` over `--accent-dim` (`#f0c040` at 9.4% alpha) composited on `--surface-base`. Dark theme effective background ≈ `#161308`. Verify `#e8f6ff` on `#161308` meets 4.5:1 for both themes using WebAIM Contrast Checker. Calculated ratio is high (likely >15:1) but confirm before ship. (ally)

*Agents: add items with a short description, the date, which story is blocked, and which agent surfaced it.*
