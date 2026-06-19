# Owner TODO

Actions that only you can take. Agents add items here when they are blocked or need a decision. Delete or check items off when done.

---

## Urgent

_Nothing urgent yet._

## Hex Escape (Outbreak) — v2 known limitations

- [ ] **2026-06-16** Hex Escape v2 ships with a known limitation: a disconnected player's seat stalls the round indefinitely (no auto-skip, no turn timer). This is acceptable for the current friend-group context but must be fixed before the game is opened to a broader audience. A follow-up story must add auto-skip or a per-seat turn timer. A `[Fact(Skip=...)]` test in `HexEscapeModuleTests.cs` documents the gap. Do not mark the hexescape game as publicly available until this is resolved. — blocks public launch of hexescape. (analyst)

## Round-3 playtest follow-ups (Hex Escape)

Found by a 4-agent round-3 playtest (tester/architect/ux/backend) against the implemented code. The crash in MoveCharacter, the qualifying-actions softlock, and the wrong direction labels were FIXED in-branch. The rest are deferred:

- [ ] **2026-06-16 — PLATFORM (devops):** malformed action JSON (e.g. `{"type":"NotARealAction"}` or missing `type`) throws an unhandled `JsonException` out of `IGameHandler.Handle` — the dispatcher does not catch it, so it surfaces as a 500 and (with the retrying execution strategy) retries the poison action. This affects EVERY game module (LoveLetter etc. deserialize the action the same way), so the right fix is a try/catch around `handler.Handle(ctx)` in `GameDispatcher` that converts deserialization/handler exceptions into a clean rejection. Remotely triggerable. (backend/chaos-monkey)
- [ ] **2026-06-16 — backend (low):** Phase-2 break-out can target the same spawn cell twice in one pass (the frozen-occupancy set excludes live same-pass spawns), producing two stacked zombies on one cell. Stacking is legal so it's not a crash, just off from spec intent. Also an O(Z²) rebuild of that set inside the loop (negligible at realistic zombie counts). Fix together: build the set once outside the loop and add live spawns to it. (backend)
- [ ] **2026-06-16 — backend (low):** `SpawnZombieAt` is `static` with no logger, so hitting `MaxZombies` via `PlaceZombieTile` skips the spawn silently (no WARNING), unlike the D1/horde paths. Observability gap only. (tester)
- [ ] **2026-06-16 — backend (fragility):** deck-band math has no guard if constants are ever retuned such that `zombieTileCount > middleBandSize` (would overflow the deck length). Safe for all current player-count constants. Add a defensive assert. (tester/backend)

## Round-3 UX follow-ups (Hex Escape) — for a polish story

A round-3 UX playtest found the first-game experience has high friction. Highest-impact items, in priority order:

- [x] **2026-06-16 — ux/frontend:** tile rotation has no visual preview — the ActionPicker shows "Rotation: 2" as a number. Render the actual pipe-edge SVG at the chosen rotation. Flagged as the single highest-impact change: it makes the core puzzle visual and teaches the connection rule implicitly. (ux) — **DONE 2026-06-16: `TilePreview` SVG component added to ActionPicker; uses same `openEdges` logic as the board; updates live as type/rotation changes; aria-hidden; numeric rotation preserved.** (frontend)
- [x] **2026-06-16 — ux/frontend:** the exit reveal has no ceremony for sighted players (only a screen-reader alert). Add a board/overlay banner + highlight when `exitRevealed` flips true — it's the game's first-act climax. (ux) — **DONE 2026-06-16: `ExitRevealBanner` pill banner with slide-in animation (respects prefers-reduced-motion); `hexExitPulse` polygon animation on exit cell; both auto-dismiss after 4s. SR alert preserved.** (frontend)
- [ ] **2026-06-16 — ux/frontend:** no onboarding. A first-time player faces a coordinate-labelled grid with no goal/turn explanation. Add a dismissible "how to play" card or a turn-1 contextual coach (goal, your reserved spawn cell, the ≥2-actions rule, what zombies do). (ux)
- [x] **2026-06-16 — ux/frontend:** the dev-only axial coordinate labels (e.g. "-4,0") render on every cell in production — visual noise for players. Hide behind a debug flag or remove. (ux) — **DONE 2026-06-16: coord labels gated behind `showCoords` prop, defaulting to `import.meta.env.DEV`; hidden in production, visible in dev builds.** (frontend)
- [x] **2026-06-16 — ux/frontend (minor):** zombie movement shows positions instantly under a text log rather than animating movement on the board; the threat ramp (deck bands) is invisible; the game-over "Exit reach" stat reads as a cryptic number; one inline style remains in `PlayerRow`. (ux) — **DONE 2026-06-16 (partial): "Exit reach" relabelled to "Characters at exit"; "Round" stat changed to "Rounds survived"; `gameOverStatValueSmall` CSS class for level name (removes inline style); `playerNameYou` CSS class replaces inline style on "(you)" label. Zombie board animation and deck-band visibility deferred (require larger changes).** (frontend)

## Needs your decision

- [x] **2026-03-26** Admin portal — OQ-ADMIN-01: the request said "force reset a user". This spec interprets that as sending an admin-triggered password reset email. If you meant **account deletion** (hard delete or anonymisation), story-031b needs to be redesigned before backend work begins. Confirm: password reset email, or account deletion? — **Resolved 2026-03-26: "Force reset" means password reset email. Account deletion is separately supported via `DELETE /api/admin/users/{userId}`.** (analyst)

- [x] **2026-03-26** Admin portal — OQ-ADMIN-03: how should the first Admin role be granted in production? There is no UI for this. Options: (a) a `dotnet run --admin-seed email@example.com` CLI argument checked at startup, (b) a one-time SQL snippet in a runbook, or (c) a short script in `scripts/`. A documented runbook is required before the portal is usable in production — **Resolved 2026-03-26: seed from `ADMIN_SEED_EMAIL` environment variable. If present at startup, `AdminRoleSeeder` finds or creates the user with that email and assigns the Admin role. Idempotent. No separate CLI or SQL runbook required.** (analyst)

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

- [x] **2026-03-26** Profile-images ally review — `.icon-btn:focus-visible` rule confirmed present in `tokens.css`; Avatar.tsx `fontSize` values confirmed as rem strings. Both were already in the codebase. No commit needed. (ally)

- [x] **2026-03-26** Profile-images ally review — `<nav aria-label="Platform actions">` in `AppShell.tsx` changed to `<div>`. `<nav>` is a navigation landmark and must not wrap action buttons. Fixed directly in `packages/ui/src/components/AppShell.tsx`. (ally)

- [ ] **2026-03-26** Profile-images ally review — `TurnIndicator` has no flex layout on `.turn-indicator`. The avatar and label will stack or flow awkwardly without `display: flex; align-items: center; gap: var(--space-2)`. Ask `ux` to confirm layout intent and ask `frontend` to add it. Non-blocking (visual only). (ally)

- [ ] **2026-03-26** Profile-images ally review — manual contrast verification: Avatar initials use `--text-bright` over `--accent-dim` (`#f0c040` at 9.4% alpha) composited on `--surface-base`. Dark theme effective background ≈ `#161308`. Verify `#e8f6ff` on `#161308` meets 4.5:1 for both themes using WebAIM Contrast Checker. Calculated ratio is high (likely >15:1) but confirm before ship. (ally)

- [x] **2026-06-16** Hex Escape — spawn zone colour-only distinction. RESOLVED: all spawn-zone cells now render a small "S" marker (`.spawnZoneIcon`) as a non-colour cue, mirroring the reserved cell's larger "S". (ally → fixed)

- [ ] **2026-06-16** Hex Escape — exit zone colour-only distinction: cells in `exitZoneCells` use an orange stroke/tint (`--neon-orange`) and a small "X" letter marker. The "X" provides a non-colour cue which helps, but the overall zone colour (orange vs empty grey) is the primary indicator. Ask `ux` to confirm the "X" label is sufficient or add a more prominent non-colour cue. (ally)

- [ ] **2026-06-16** Hex Escape — manual contrast verification needed: (1) `--neon-magenta` (`var(--neon-magenta)`) on `--surface-raised` in `.zombieBanner`, `.handTileZombie` border, `.badgeEliminated` — verify 4.5:1 at 0.6–0.78rem sizes using WebAIM Contrast Checker; (2) `--status-success` (green) on `--surface-float` for `.charToken` circle fill — visual token only, no text, so 3:1 boundary applies; (3) `--text-muted` on `--surface-raised` at 0.6rem in `.sideTitle`, `.roundInfo`, `.qualCount`, `.playerBadgePending` — these carry game-relevant information and must meet 4.5:1 at normal weight. (ally)

- [x] **2026-06-16** Hex Escape — SVG cell board keyboard `aria-disabled`. RESOLVED: `Game.tsx` computes an `actionableCoords` set (mirroring the click logic) and passes it to `HexBoard`; non-actionable cells stay focusable (readable) but carry `aria-disabled` and don't fire, so keyboard users get clear feedback. (ally → fixed)

- [x] **2026-06-19** Hex Escape (v2 zombie-growth rework) — DEAD UX PATH: `hasZombieObligation`, the zombie obligation banner, the `placeZombie` picker branch, and the "Place your zombie tile first" disabled-reason strings are all unreachable because zombie tiles no longer enter the player's hand. Confirm with the analyst that the hand-zombie mechanic is permanently retired, then ask `frontend` to delete `myZombieTile`, `hasZombieObligation`, `picker.kind === 'placeZombie'`, the zombie obligation banner JSX, and the `placeZombie` case in `confirmPicker`, `getTitle`, and `ActionPicker`. Also remove `PickerMode { kind: 'placeZombie' }` from the type union. The hand tile `isZombieTile` render path in the sidebar hand list can also be removed. (ally)
  - **DONE 2026-06-19 (dead-code cleanup PR, branch `cleanup-hexescape-dead-code`):** removed `myZombieTile`, `hasZombieObligation`, the `placeZombie` `PickerMode` member and its `handleCellClick`/`confirmPicker`/`getTitle`/`ActionPicker` branches, the zombie obligation banner JSX, the `placeZombie` disabled-reason strings, and `hordeOriginCells` from the frontend state type. Server-side, the `PlaceZombieTile` handler, `DoForcedZombieDiscard`, and the inert horde-origin subsystem were deleted, confirming the hand-zombie mechanic is permanently retired. Left in place (out of scope, both unreachable since zombie tiles never enter the hand): the sidebar `isZombieTile` render path and the defensive `HasZombieTileInHand` obligation guards. (session owner)

- [ ] **2026-06-19** Hex Escape — move-target visual: the new dashed-stroke indicator on slide-reachable cells (`.hexMoveTarget`) applies `stroke` and `stroke-dasharray` to `polygon:first-child`. This overrides the hex cell fill polygon correctly in isolation, but ask `ux` to verify the amber dashed outline reads distinctly enough from the amber solid focus ring and from the amber exit-cell border in the apocalyptic theme (all use `--accent`). Consider a wider dash gap or a different dash rhythm to distinguish "I can slide here" from "this has keyboard focus". (ally)

- [ ] **2026-06-19** Hex Escape — zombie spawn cascade announcement: when a zombie card is drawn, the backend can chain-spawn multiple new zombies from the centre seeds in a single server-side pass. The frontend `ZombieRollOverlay` shows each individual zombie's die roll (moved/blocked), but there is no screen-reader announcement of how many new zombies were added to the board or which coordinates they occupy. A blind player loses track of horde size. Ask `frontend` to add a `role="alert"` announcement after the zombie-card pass completes (e.g. "Zombie horde grew — N new zombies added") sourced from a new `lastZombieSpawnCount` state field, or derive from `state.zombies.length` diff. (ally)

*Agents: add items with a short description, the date, which story is blocked, and which agent surfaced it.*
