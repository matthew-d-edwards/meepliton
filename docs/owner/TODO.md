# Owner TODO

Actions that only you can take. Agents add items here when they are blocked or need a decision. Delete or check items off when done.

---

## Urgent

_Nothing urgent yet._

## Needs your decision

- [x] **2026-03-14** Choose avatar storage strategy for v1: **Gravatar** (derive from email hash). (analyst) — decided 2026-03-14

- [ ] **2026-03-14** UX gap analysis found that `POST /api/rooms/{roomId}/transfer-host` is in the requirements (§11.1) but may not be implemented in the backend yet. Confirm whether it exists before story-028 (host transfer UI) is scheduled. (ux/backend)

- [x] **2026-03-14** Decide what the second game module should be. **Liar's Dice** — dice-based bluffing game, clean modern pirate theme (not cheesy). Lobby and header keep Skyline theme; game room controls its own look. Proves Aspire orchestration, per-game migrations, and dynamic game loading. (analyst) — decided 2026-03-14

- [x] **2026-03-14** `currentPlayerId` surfacing: **client-side JSONB extraction** — frontend reads `currentPlayerId` from game state JSON (simpler for v1). Blocks story-018. (analyst) — decided 2026-03-14

## Setup / credentials

- [ ] **2026-03-14** Configure email provider: **SendGrid free tier** chosen. Add `SENDGRID_API_KEY` to GitHub Secrets and Azure Container App environment variables. Blocks stories 001, 003 (end-to-end email flow only — backend code can be implemented now). (analyst)

- [ ] **2026-03-14** Create a Google OAuth 2.0 client in Google Cloud Console. Add `Client ID` and `Client Secret` to Azure Container App environment variables (or GitHub Secrets for CI). Blocks story-004. (analyst)

- [ ] **2026-03-14** Provision an Azure Application Insights resource and add `APPLICATIONINSIGHTS_CONNECTION_STRING` to GitHub Secrets and the Container App environment. Blocks story-023. (devops)

---

## Ally review — pending commit

- [ ] **2026-03-15** Ally finished all source file edits for the Liar's Dice accessibility review on branch `claude/review-pm-analyst-feedback-ZLEtl`. Git commands are not available to this agent — please run the following to commit and push:
  ```bash
  cd /home/user/meepliton
  git add apps/frontend/src/games/liarsdice/
  git commit -m "fix(ally): accessibility and inclusivity review for Liar's Dice UI"
  git push origin claude/review-pm-analyst-feedback-ZLEtl
  ```
  (ally)

- [ ] **2026-03-15** Manual contrast verification needed for `--color-text-muted: #a0b8cc` on `--color-background: #0d1b2a` (pirate theme). Calculated ratio is ~4.7:1 — should pass WCAG AA 4.5:1 for small text. Verify with WebAIM Contrast Checker before merge. (ally)

- [ ] **2026-03-15** Manual verification needed: `.cupEliminated` opacity raised to 0.6 — verify eliminated cups still look visually distinct from active ones in the browser. (ally)

- [ ] **2026-03-17** Ally finished all source file edits for the story-007 and story-025 accessibility review on branch `claude/agent-job-completion-XSKmZ`. Git commands are not available to this agent — please run the following to commit and push:
  ```bash
  cd /home/user/meepliton
  git add apps/frontend/src/platform/room/RoomPage.tsx \
          apps/frontend/src/platform/room/room.css \
          apps/frontend/src/platform/account/account.css
  git commit -m "fix(ally): accessibility review for story-007 and story-025"
  git push origin claude/agent-job-completion-XSKmZ
  ```
  (ally)

- [ ] **2026-03-17** `tokens.css` contains multiple keyframe animations (`btn-primary-idle`, `dot-pulse`, `fab-breathe`, `meepliton-scanlines`, `meepliton-fade-in`, `action-rejected-slide-in`, stagger children) that have no `prefers-reduced-motion` guard. The ally agent cannot edit `tokens.css` directly — this must be handled by the `ux` agent. Ask `ux` to add a `@media (prefers-reduced-motion: reduce)` block to `packages/ui/src/styles/tokens.css` that sets `animation-duration: 0.01ms !important` and `transition-duration: 0.01ms !important` for all elements. This is a WCAG 2.1 AA criterion 2.3.3 (AAA) / strong 2.3 best practice and is required for users with vestibular disorders. Blocks story-025 and story-007 ally sign-off on motion. (ally)

- [ ] **2026-03-17** Manual contrast verification needed for the following token pairs used in story-007 (ProfilePage) — the ally agent switched `--text-muted` to `--text-primary` for labels, section titles, char counter, and avatar hint, but the text sizes remain below 16px (0.65–0.7rem). Verify `--text-primary` (#c0d8f0 dark / #b8d0e8 light) on `--surface-raised` (#070d19 dark / #252d3a light) meets WCAG AA 4.5:1 for these small labels. Use WebAIM Contrast Checker. (ally)

- [ ] **2026-03-17** Manual contrast verification needed: `RoomLoadingScreen` loading label in `RoomPage.tsx` is rendered at `0.75rem` with `--text-muted`. The element has `aria-label` so screen readers receive the text, but the visible label may fail AA contrast at that size. If manual check fails, raise with `ux` to increase font size to `0.875rem` minimum or use `--text-primary`. (ally)

*Agents: add items with a short description, the date, which story is blocked, and which agent surfaced it.*
