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

*Agents: add items with a short description, the date, which story is blocked, and which agent surfaced it.*
