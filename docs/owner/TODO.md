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

*Agents: add items with a short description, the date, which story is blocked, and which agent surfaced it.*
