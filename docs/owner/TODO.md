# Owner TODO

Actions that only you can take. Agents add items here when they are blocked or need a decision. Delete or check items off when done.

---

## Urgent

_Nothing urgent yet._

## Needs your decision

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

*Agents: add items with a short description, the date, which story is blocked, and which agent surfaced it.*
