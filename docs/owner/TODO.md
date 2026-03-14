# Owner TODO

Actions that only you can take. Agents add items here when they are blocked or need a decision. Delete or check items off when done.

---

## Urgent

_Nothing urgent yet._

## Needs your decision

- [ ] **2026-03-14** Choose avatar storage strategy for v1: (a) URL-only — user pastes any image URL, (b) Gravatar — derive from email hash, no upload needed, (c) file upload — requires blob storage. Blocks story-007. (analyst)

- [ ] **2026-03-14** Decide what the second game module should be. Requirements say it must be structurally different from Skyline (tile placement) — e.g. map-based, card-based, or simultaneous-action. Blocks story-022. (analyst)

- [ ] **2026-03-14** `currentPlayerId` surfacing for the turn indicator: should the platform copy `currentPlayerId` into the `rooms` table on every state update (enables server-side queries and platform-level display), or should the frontend extract it from the game state JSON client-side (simpler but couples the platform to a state shape convention)? Blocks story-018. (analyst)

## Setup / credentials

- [ ] **2026-03-14** Configure an email provider for transactional email (registration confirmation, password reset). Options: SendGrid free tier (100/day) or Gmail SMTP relay. Add credentials to GitHub Secrets as `EMAIL_SMTP_*` or `SENDGRID_API_KEY`. Blocks stories 001, 003. (analyst)

- [ ] **2026-03-14** Create a Google OAuth 2.0 client in Google Cloud Console. Add `Client ID` and `Client Secret` to Azure Container App environment variables (or GitHub Secrets for CI). Blocks story-004. (analyst)

- [ ] **2026-03-14** Provision an Azure Application Insights resource and add `APPLICATIONINSIGHTS_CONNECTION_STRING` to GitHub Secrets and the Container App environment. Blocks story-023. (devops)

---

*Agents: add items with a short description, the date, which story is blocked, and which agent surfaced it.*
