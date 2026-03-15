# Meepliton — Platform Requirements & Architecture
**Version 0.6 | Living Document | 2026-03-13**
**Site:** meepliton.com | **Repo:** github.com/[username]/meepliton

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Goals & Non-Goals](#2-goals--non-goals)
3. [Architecture Decision Records](#3-architecture-decision-records)
4. [User Stories](#4-user-stories)
5. [Functional Requirements](#5-functional-requirements)
6. [Non-Functional Requirements](#6-non-functional-requirements)
7. [Technology Stack & Cost Analysis](#7-technology-stack--cost-analysis)
8. [Architecture](#8-architecture)
9. [Database & Migration Strategy](#9-database--migration-strategy)
10. [Game Module System](#10-game-module-system)
11. [API Design](#11-api-design)
12. [Infrastructure & Deployment](#12-infrastructure--deployment)
13. [GitHub Actions CI/CD](#13-github-actions-cicd)
14. [Repository Structure](#14-repository-structure)
15. [Game Scaffolding](#15-game-scaffolding)
16. [Claude AI Skill Files](#16-claude-ai-skill-files)
17. [Open Questions](#17-open-questions)
18. [Phased Roadmap](#18-phased-roadmap)

---

## 1. Executive Summary

Meepliton (meepliton.com) is a browser-based multiplayer board game platform for a small group of friends who want to build, prototype, and play custom board games together. It is not a marketplace — it is a private, invite-only space where anyone can spin up a room in seconds and play a game that someone in the group may have written last week.

The platform is built around two constraints that every architectural decision must satisfy:

1. **New games must be addable without touching the platform core.** A game is a self-contained module with its own schema, migrations, and UI.
2. **Hosting costs must stay low.** This is a hobby project. The target monthly bill is under $25.

---

## 2. Goals & Non-Goals

### Goals

- Authenticate with Google OAuth or email/password — users choose, both are first-class
- Create or join game rooms with a 6-character code, a shareable link, or a QR code at meepliton.com
- Play any installed game module in real-time with 2–8 players
- Add a new game by writing a self-contained module — nothing else in the codebase changes
- Each game manages its own database schema and migrations independently
- Work well on mobile and desktop with dark and light themes
- Deploy automatically from GitHub on every push to `main`
- Be easy for friends (including non-developers) to contribute games using Claude — `CLAUDE.md`, skill files, and a git workflow guide reduce the barrier to near zero

### Non-Goals (v1)

- Public matchmaking with strangers
- Native iOS/Android apps (responsive PWA is the target)
- Spectator or streaming modes
- Monetisation or subscriptions
- A visual drag-and-drop game builder UI

---

## 3. Architecture Decision Records

*ADRs record the reasoning behind significant decisions so future contributors understand the why, not just the what. Each ADR is permanent — superseded decisions are marked rather than deleted.*

---

### ADR-001: SignalR in-process instead of Azure SignalR Service

**Status:** Accepted

**Context:** The original design called for Azure SignalR Service to handle WebSocket fan-out at scale. Real-time communication is core to the platform.

**Decision:** Run ASP.NET Core SignalR in-process within the API container. Do not use Azure SignalR Service.

**Reasoning:** Azure SignalR Service Standard tier has a hard minimum cost of ~$50/month regardless of usage. The free tier caps at 20 concurrent connections, which is insufficient for even a single active 6-player game room with overhead. For a hobby project expecting at most 20 concurrent rooms, a single Container App instance handles SignalR fine without a shared backplane. The hub code is identical either way — if scaling ever requires multiple instances, adding a Redis backplane is one NuGet package (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`) and a connection string, not a rewrite.

**Consequences:** Cold starts after idle may delay the first SignalR connection. Horizontal scaling to multiple Container App instances requires adding the Redis backplane first.

---

### ADR-002: Azure Container Apps (Consumption) for API hosting

**Status:** Accepted

**Context:** The API needs to be hosted somewhere that runs .NET containers, supports SignalR WebSockets, and is cheap at low traffic.

**Decision:** Use Azure Container Apps on the Consumption plan with `minReplicas: 0`.

**Reasoning:** Container Apps scales to zero when idle — a hobbyist app that gets bursts of evening usage pays almost nothing during the day. The free tier covers 180,000 vCPU-seconds per month, which comfortably handles sporadic traffic. Azure App Service B1 would cost ~$55/month as a fixed charge regardless of usage. If cold-start delays (typically 5–15 seconds after extended idle) prove annoying in practice, setting `minReplicas: 1` adds roughly $5–8/month to keep one instance warm.

**Consequences:** First request after idle will be slow. Acceptable for a hobby project; revisit if it disrupts real sessions.

---

### ADR-003: Per-game database migrations, isolated schema

**Status:** Accepted

**Context:** Game modules must be addable without touching the platform core. If all migrations live in one EF Core context, adding a game means editing the platform's migration history, which violates the isolation principle.

**Decision:** The platform has a `PlatformDbContext` with its own migration history. Each game module has its own `DbContext` (e.g. `SkylineDbContext`) with its own migration history tracked in a separate `__EFMigrationsHistory_{gameId}` table. Game contexts are given read-only access to platform tables via a dedicated PostgreSQL role. All contexts connect to the same database but operate in isolation.

**Reasoning:** Adding a new game means adding a new migration folder and a new DbContext — zero changes to platform migrations. Game migrations can be added, run, and rolled back independently. The read-only role boundary prevents games from accidentally corrupting platform data.

**Consequences:** Startup migration runner must discover and apply all game `DbContext` instances in addition to the platform context. See §9 for the full implementation.

---

### ADR-004: Game state stored as JSONB in the platform schema

**Status:** Accepted

**Context:** Every game has a different state shape. The platform needs to store and retrieve it without knowing what it contains.

**Decision:** `rooms.game_state` is a JSONB column owned by the platform schema. Game modules serialize/deserialize their typed state to/from this column. Games do NOT get a separate table for their primary state blob.

**Reasoning:** The platform owns the room lifecycle and must be able to read `game_state` for reconnect (sending current state to a rejoining player) without loading a game-specific schema. JSONB in PostgreSQL is indexable and queryable if needed. Game-specific supplementary tables (leaderboards, match history, statistics) live in game-owned schemas managed by game migrations.

**Consequences:** Game state cannot be efficiently queried with SQL joins. Acceptable — the platform never needs to query inside game state. Game-specific analytics or supplementary data should use game-owned tables.

---

### ADR-005: Assembly scanning for game module discovery

**Status:** Accepted

**Context:** The platform must discover installed games at startup without requiring manual registration.

**Decision:** On startup, the API uses Scrutor to scan all assemblies for implementations of `IGameModule` and `IGameHandler`. The `games` catalogue table is upserted from discovered implementations on every startup.

**Reasoning:** Adding a game requires only a project reference to the solution and one line in the frontend registry. No changes to `Program.cs`, no configuration files, no separate database seeds.

**Consequences:** All game assemblies must be present in the published output. Remote/lazy game loading (a game as a separately deployed NuGet package) is out of scope for v1.

---

### ADR-006: GitHub Actions for CI/CD, no additional tooling

**Status:** Accepted

**Context:** Deployment should be automatic on push to `main` and must cost nothing beyond what GitHub provides free.

**Decision:** GitHub Actions with two first-party Azure actions: `Azure/static-web-apps-deploy` for the frontend and `az containerapp update` via `azure/login` for the API. No Azure DevOps, no third-party deployment tools.

**Reasoning:** GitHub Actions is free for public repositories. The two Azure actions are well-maintained by Microsoft and near-zero configuration once the Azure resources exist. Migrations run as a step in the backend job before the new container image is deployed, ensuring schema changes land before new code starts serving traffic.

**Consequences:** PRs get a build-and-test run but not a staging deployment in v1. Adding a new game requires adding one migration step to the CI pipeline.

---

### ADR-007: ASP.NET Core Identity for user management

**Status:** Accepted

**Context:** The original design used a hand-rolled `users` table with Google OAuth handled directly via OIDC middleware. Two questions arose: should the platform support username/password login in addition to Google? And should Microsoft Entra ID (Azure AD) be used instead of self-managed identity?

**Decision:** Use ASP.NET Core Identity backed by the existing PostgreSQL database via EF Core. Replace the hand-rolled `users` table with Identity's schema. Extend `IdentityUser` with `ApplicationUser` to add Meepliton-specific columns. Support both Google OAuth and email/password from day one.

**Reasoning:** Identity's schema already solves the problem cleanly — `PasswordHash` is nullable, so Google-only users and email/password users coexist in the same `users` table without any schema distinction. `user_logins` links external providers to accounts, enabling a single user to have multiple login methods. Enabling username/password required adding two endpoints and zero schema changes versus the Google-only design. Microsoft Entra ID was rejected — it is a cloud enterprise service for organisational identity, not appropriate for a friend-group hobby platform and would add cost and complexity. A fully hand-rolled approach was rejected because it would require reimplementing password hashing, account lockout, security stamps, token generation, and reset flows that Identity already provides securely and correctly.

**Consequences:** `PlatformDbContext` inherits from `IdentityDbContext<ApplicationUser>`. Identity table names are remapped to snake_case to match the existing PostgreSQL style. The role tables (`roles`, `user_roles`, `role_claims`) are created by migrations but unused in v1. Password reset and email confirmation require a transactional email service (see §5.1 and OQ-08).

---

### ADR-008: No shared game UI component library

**Status:** Accepted

**Context:** Early designs included a `@meepliton/ui` package containing game-specific components: `<Board>`, `<Card>`, `<Dice>`, `<Token>`, `<TileGrid>`. The intent was to help game authors by providing reusable rendering primitives.

**Decision:** Remove `@meepliton/ui` game components entirely. Each game module owns its own UI completely, with no platform-imposed rendering primitives. The platform provides only chrome components (lobby, room waiting screen, player presence indicators, join code display) and the design token CSS file.

**Reasoning:** The component library assumption only fits the simplest tile-and-card games. Ticket to Ride requires a vector map with route highlighting and train placement. Catan needs a hex grid with ports, harbours, settlement and road placement, the robber, and a resource trading UI. Pandemic needs a city network graph. A shared `<Board rows cols>` helps none of these. Worse, it creates a false expectation that games should fit a grid model, constraining what games authors will attempt. Each game should use whatever rendering approach fits — SVG, Canvas, Three.js, plain DOM, or any React library it wants. The platform has no opinion on this.

**Consequences:** Each game brings its own rendering dependencies. The `packages/ui` package is retained only for platform chrome (lobby, auth pages, room screens). The design token CSS (`tokens.css`) is still shared so games can match the platform's visual identity if they choose. The `@meepliton/ui` import in skill files and scaffold templates is removed.

---

### ADR-009: Game modules own their entire state model and history strategy

**Status:** Accepted

**Context:** The original design prescribed that the platform would own undo/redo via action log replay. Several requirements assumed games use a simple reducer pattern (pure function, action in → state out).

**Decision:** The platform owns the *transport* of state (storing it in `rooms.game_state`, broadcasting it via SignalR) and nothing else. How a game internally structures its state — whether as a flat record, an event-sourced log, a state machine with explicit phases, a snapshot plus delta list, or anything else — is entirely the game module's concern. Undo/redo, if supported, is implemented inside the game module's action handler, not by the platform.

**Reasoning:** Games vary enormously in their internal complexity. A simple game like Skyline is well-served by a pure reducer. Ticket to Ride needs route-claiming with multi-player negotiation, a card drawing phase, and end-game scoring across multiple criteria. Catan has trading, robber placement, knight cards, and resource management that doesn't fit a single-action-single-state-change model. Forcing all games through the same reducer pattern would require game authors to contort their logic to fit the platform's assumptions. The platform's job is to reliably store and broadcast whatever blob the game says is authoritative state. The game's job is to define what that means.

**Consequences:** `IGameReducer` is renamed to `IGameHandler` and its interface is widened slightly — it receives a `GameContext` (current state + action + metadata) and returns a `GameResult` (new state + optional side effects like notifications). Games are not required to be purely functional. The `action_log` is still written by the platform for every action as an audit trail, but replay from the log is the game's responsibility, not the platform's. Undo is a game-level capability: a game that supports it declares `SupportsUndo = true` and handles an `"Undo"` action type in its handler.

---

### ADR-010: Per-player state projection for hidden information

**Date:** 2026-03-15
**Status:** Accepted

**Context:** FR-MOD-10 previously placed all visibility responsibility on games as client-side UI filtering. The full state blob reached every connected client. On a friends-only platform this was acceptable, but as Liar's Dice demonstrated, it breaks the social contract even among friends — players can observe each other's hidden dice via DevTools.

**Decision:** Add an opt-in server-side projection mechanism. Games that have hidden information override `ProjectForPlayer` in their `ReducerGameModule` subclass. `GameDispatcher` detects this and fans out per-player projected states via `Clients.User` instead of `Clients.Group`. The authoritative full state remains in the database unchanged.

**Consequences:**
- Games without hidden information: zero behaviour change
- Games with hidden information: implement one method; client-side filtering can be removed
- Per-action cost: N additional serialise/deserialise cycles (one per player). Acceptable at 2–6 player hobby scale
- `rooms.game_state` always stores full state — no redaction at rest

---

## 4. User Stories

### Authentication

- As a new user, I can register with my email address and a password
- As a returning user, I can sign in with my email and password
- As a user, I can sign in with my Google account as an alternative to email/password
- As a user who registered with Google, I can add an email/password to my account later
- As a user who registered with email/password, I can link my Google account later
- As a user, I can request a password reset email if I forget my password
- As a user, I remain signed in across browser sessions until I explicitly sign out
- As a Google-authenticated user, my display name and avatar are pre-filled from my Google profile
- As an email-registered user, I can set my own display name and upload or choose an avatar

### Lobby

- As a user, I can see all active game rooms I am part of
- As a user, I can browse available games and read their descriptions before starting a room
- As a user, I can start a new room by selecting a game and optionally configuring options
- As a user, I can join a room by typing a six-character join code
- As a user, I can toggle dark and light themes and have my preference remembered

### Joining

- As a user, I can join a room by visiting a shared URL on any device
- As a user, I can join a room by scanning a QR code with my phone
- As a user, if I disconnect and reconnect, I return to my player seat automatically
- As a user, I can see which other players are currently connected

### Game Rooms

- As a host, I see a waiting screen showing connected players and the join code prominently
- As a host, I can start the game once the minimum number of players have joined
- As a host, I can remove a player before the game starts
- As a player, I receive game state updates in real time without refreshing
- As a player, I can see clearly whose turn it is

### Games

- As a player, I interact with a game-specific board rendered by the game module
- As a player on mobile, I get a touch-friendly interface appropriate to the game
- As a player, any invalid action I attempt is rejected immediately with a clear message

---

## 5. Functional Requirements

### 5.1 Authentication

#### Identity System

| ID | Requirement |
|---|---|
| FR-AUTH-01 | User management is implemented using ASP.NET Core Identity with `ApplicationUser` extending `IdentityUser` (see ADR-007) |
| FR-AUTH-02 | `PlatformDbContext` inherits from `IdentityDbContext<ApplicationUser>`; Identity tables are snake_case-renamed via EF Core fluent configuration |
| FR-AUTH-03 | All state-mutating API endpoints and SignalR hub methods require a valid authenticated session via `[Authorize]` |
| FR-AUTH-04 | Session tokens are JWT bearer tokens stored in an HttpOnly cookie; the frontend never holds credentials directly |
| FR-AUTH-05 | Account lockout is enforced by Identity: 5 failed attempts triggers a 15-minute lockout (`lockoutOnFailure: true` on all sign-in calls) |

#### Google OAuth

| ID | Requirement |
|---|---|
| FR-AUTH-06 | Users can sign in with Google via ASP.NET Core's `.AddGoogle()` external provider |
| FR-AUTH-07 | On first Google sign-in, an `ApplicationUser` is created and a row added to `user_logins` linking the Google subject ID |
| FR-AUTH-08 | On subsequent Google sign-ins, the existing account is found via `user_logins` and signed in; no duplicate users are created |
| FR-AUTH-09 | Display name and avatar URL are populated from the Google profile on first login and refreshable by the user later |

#### Email / Password

| ID | Requirement |
|---|---|
| FR-AUTH-10 | Users can register with an email address and password via `POST /api/auth/register` |
| FR-AUTH-11 | Passwords are validated: minimum 8 characters, at least one uppercase letter, one digit; no other requirements |
| FR-AUTH-12 | Passwords are hashed by Identity's default `PasswordHasher<ApplicationUser>` (bcrypt-derived PBKDF2); no raw passwords are stored |
| FR-AUTH-13 | Users can sign in with email/password via `POST /api/auth/login`; lockout applies on failure |
| FR-AUTH-14 | Email addresses must be unique across all accounts regardless of login method |
| FR-AUTH-15 | Users can request a password reset email via `POST /api/auth/forgot-password`; a time-limited token is sent to their address |
| FR-AUTH-16 | Users complete the reset via `POST /api/auth/reset-password` with the token from the email |
| FR-AUTH-17 | Email confirmation is required before an email/password account can sign in (confirmation link sent on registration) |

#### Account Linking

| ID | Requirement |
|---|---|
| FR-AUTH-18 | A signed-in user can link a Google account to their existing profile via `POST /api/auth/link/google` |
| FR-AUTH-19 | A signed-in user can add an email/password to a Google-only account via `POST /api/auth/add-password` |
| FR-AUTH-20 | A user cannot unlink their last login method (must always have at least one way to sign in) |

### 5.2 Lobby

| ID | Requirement |
|---|---|
| FR-LOB-01 | The lobby lists all rooms where the current user appears in `room_players` and status is not `closed` |
| FR-LOB-02 | The game catalogue is derived from registered `IGameModule` implementations discovered at startup |
| FR-LOB-03 | Creating a room requires selecting a game; optional per-game configuration is rendered by the module |
| FR-LOB-04 | Room status is one of: `waiting`, `in_progress`, `finished`, `closed` |
| FR-LOB-05 | Theme preference (dark/light) is stored in the user's profile row and synced across devices |

### 5.3 Room & Joining

| ID | Requirement |
|---|---|
| FR-ROOM-01 | Every room is assigned a unique six-character alphanumeric join code on creation (chars: A-Z, 2-9, excludes 0/O/I/1) |
| FR-ROOM-02 | The join URL is `https://meepliton.com/join/{code}` and resolves in any browser without authentication |
| FR-ROOM-03 | A QR code encoding the join URL is generated client-side and displayed on the room waiting screen |
| FR-ROOM-04 | The join code is displayed in large readable type on the waiting screen at all times |
| FR-ROOM-05 | A player joining a room in progress is placed in spectator status unless the game module sets `AllowLateJoin = true` |
| FR-ROOM-06 | A disconnected player's seat is held; `connected` is set to false via `OnDisconnectedAsync` |
| FR-ROOM-07 | A player who reconnects with the same identity is assigned their existing seat and receives the current full state |
| FR-ROOM-08 | The host can transfer host status to another connected player |

### 5.4 Real-Time Sync

| ID | Requirement |
|---|---|
| FR-SYNC-01 | Real-time communication uses ASP.NET Core SignalR running in-process (see ADR-001) |
| FR-SYNC-02 | All game actions are sent to the server via SignalR; state is never mutated client-side directly |
| FR-SYNC-03 | After a valid action, the full new state is broadcast to all connections in the room group |
| FR-SYNC-04 | Invalid actions return a typed `ActionError` to the submitting client only |
| FR-SYNC-05 | Player connect/disconnect events are broadcast to all room members |
| FR-SYNC-06 | On reconnect, the server pushes the current full state to the reconnecting client |

### 5.5 Game Module System

| ID | Requirement |
|---|---|
| FR-MOD-01 | Each game is a C# class library project within the solution |
| FR-MOD-02 | Each game implements `IGameModule` and `IGameHandler` from `Meepliton.Contracts`; simple games may use `ReducerGameModule<,,>` as a convenience base |
| FR-MOD-03 | Game modules are discovered at startup via assembly scanning (see ADR-005) |
| FR-MOD-04 | Each game has its own `DbContext` and EF Core migration history (see ADR-003 and §9) |
| FR-MOD-05 | Game contexts have read-only access to platform tables via a dedicated PostgreSQL role |
| FR-MOD-10 | Games with hidden information may implement server-side state projection by overriding `ProjectForPlayer` in their `ReducerGameModule` subclass. When a game opts in (`HasStateProjection == true`), the platform projects the state per player before broadcasting — each player receives only the information their perspective allows. Games that do not opt in receive the full state broadcast as before. See ADR-010. |
| FR-MOD-06 | Each game has a corresponding frontend module exporting a `GameModule` TypeScript type |
| FR-MOD-07 | Frontend game modules are code-split via Vite dynamic imports and loaded only when needed |
| FR-MOD-08 | The platform provides `@meepliton/ui` for room chrome only (waiting screen, player presence, join code, action rejected toast) — no game rendering primitives (see ADR-008) |
| FR-MOD-09 | The platform provides a `GameContext<TState>` prop to game components containing state, players, myPlayerId, roomId, and dispatch — no assumptions about turn structure |

---

## 6. Non-Functional Requirements

| Category | Requirement |
|---|---|
| **Performance** | Lobby initial load under 3 seconds on 4G mobile |
| **Performance** | Game state update round-trip (action sent → broadcast received) under 200ms on a normal connection |
| **Cost** | Monthly Azure bill under $25 for typical hobby usage (see §7.5) |
| **Scalability** | Support 20 concurrent active rooms on a single Container App instance |
| **Availability** | Best-effort uptime; brief cold-start delays on first request after idle are acceptable (see ADR-002) |
| **Security** | Auth required for all mutations; rooms are private by default; join codes do not expose user data |
| **Accessibility** | WCAG 2.1 AA for lobby and platform chrome |
| **Mobile** | Full lobby and room management on 375px viewport; minimum 44px touch targets |
| **Theming** | Dark and light themes via CSS custom properties; system preference detected on first visit |
| **Maintainability** | Adding a new game must not require changes to the API, platform migrations, or SignalR hub |

---

## 7. Technology Stack & Cost Analysis

### 7.1 Frontend

| Concern | Choice | Rationale |
|---|---|---|
| Framework | React 18 + TypeScript | Component model suits game UI; TypeScript enforces the module contract |
| Build | Vite | Fast HMR; native `import()` code-splitting for game modules |
| Client state | Zustand | Lightweight; easy to scope per game module |
| Real-time client | `@microsoft/signalr` | Matches backend hub; handles reconnect and transport fallback |
| Routing | React Router v6 | Nested routes suit platform shell + game view layout |
| Styling | CSS Modules + CSS custom properties | Zero runtime; theming via `data-theme` on `:root` |
| QR Code | `qrcode.react` | SVG QR from join URL, fully client-side |
| Auth | Cookie-based JWT from API | HttpOnly cookie set by API after any login method; frontend is auth-method-agnostic |

### 7.2 Backend

| Concern | Choice | Rationale |
|---|---|---|
| Runtime | .NET 10 / ASP.NET Core | Familiar; excellent SignalR and EF Core integration |
| Real-time | ASP.NET Core SignalR (in-process) | Eliminates $50/month Azure SignalR Service cost (ADR-001) |
| ORM | Entity Framework Core 10 + Npgsql | Strong migrations; JSONB support; familiar query model |
| Identity | ASP.NET Core Identity + `ApplicationUser` | User management, password hashing, lockout, token generation (ADR-007) |
| Auth — Google | `Microsoft.AspNetCore.Authentication.Google` | External provider; links to `ApplicationUser` via `user_logins` |
| Auth — Email | ASP.NET Core Identity `UserManager` / `SignInManager` | Password sign-in, registration, reset, email confirmation |
| Email delivery | `IEmailSender` + SendGrid (or SMTP) | Required for email confirmation and password reset |
| API | Minimal API + SignalR Hubs | REST for lobby/room CRUD; SignalR hub for game actions and state |
| Validation | FluentValidation | Typed rules decoupled from endpoints |
| DI scanning | Scrutor | Assembly scanning for `IGameModule` / `IGameHandler` / `IGameDbContext` discovery |

### 7.3 Database

| Concern | Choice | Rationale |
|---|---|---|
| Database | PostgreSQL 16 | JSONB for game state; ACID for metadata; supports multiple DbContexts cleanly |
| Hosting | Azure Database for PostgreSQL Flexible Server — Burstable B1ms | Cheapest managed tier (~$13/month); supports EF Core migrations |
| Schema isolation | Per-game `__EFMigrationsHistory_{gameId}` tables | Games manage their own schema evolution independently (ADR-003) |

### 7.4 Infrastructure

| Concern | Choice | Rationale |
|---|---|---|
| Local orchestration | .NET Aspire | Typed service wiring; developer dashboard; same manifest drives Azure provisioning |
| API hosting | Azure Container Apps — Consumption, `minReplicas: 0` | Scales to zero; free tier handles hobby traffic (ADR-002) |
| Frontend hosting | Azure Static Web Apps — Free tier | Free for personal projects; custom domain + SSL included |
| Container registry | Azure Container Registry — Basic | ~$5/month; required for Container Apps deployments |
| CI/CD | GitHub Actions | Free for public repos; two first-party Azure actions (ADR-006) |
| Secrets | GitHub Actions Secrets (v1) | Free; sufficient for a single-environment hobby project |

### 7.5 Monthly Cost Estimate

| Resource | Tier | Estimated Monthly Cost |
|---|---|---|
| Azure Static Web Apps (frontend) | Free | $0 |
| Azure Container Apps (API) | Consumption — scales to zero | ~$0–3 |
| Azure Database for PostgreSQL | Burstable B1ms (1 vCPU, 2 GB RAM) | ~$13–15 |
| Azure Container Registry | Basic | ~$5 |
| Azure Monitor / App Insights | Free tier (5 GB logs/month) | ~$0 |
| **Total** | | **~$18–23/month** |

> **Cost lever — database:** The Flexible Server can be paused manually during extended idle periods. Pausing stops compute billing; storage (~$0.10/GB/month) continues.

> **Cost lever — warm instance:** `minReplicas: 0` means cold starts after idle (5–15 seconds). Setting `minReplicas: 1` adds ~$5–8/month to keep one instance warm.

---

## 8. Architecture

### 8.1 System Diagram

```
┌────────────────────────────────────────────────────────────┐
│                  Browser (React SPA @ meepliton.com)            │
│                                                             │
│  ┌─────────────────────┐    ┌──────────────────────────┐  │
│  │   Platform Shell     │    │   Game Module (lazy)      │  │
│  │  • Auth / session    │    │  • BoardComponent         │  │
│  │  • Lobby             │    │  • useGameState()         │  │
│  │  • Room chrome       │    │  • useDispatch()          │  │
│  │  • Theme             │    │  • game's own components │  │
│  └──────────┬──────────┘    └───────────┬───────────────┘  │
│             │  REST (fetch)             │ SignalR WS        │
└─────────────┼───────────────────────────┼──────────────────┘
              │                           │
              ▼                           ▼
┌────────────────────────────────────────────────────────────┐
│           ASP.NET Core API (Azure Container Apps)           │
│                                                             │
│  ┌─────────────────┐    ┌───────────────────────────────┐  │
│  │  Minimal API     │    │       GameHub (SignalR)        │  │
│  │  • /rooms        │    │  • JoinRoom(roomId)            │  │
│  │  • /lobby        │    │  • SendAction(roomId, action)  │  │
│  │  • /auth         │    │  • OnConnected/Disconnected    │  │
│  └────────┬─────────┘    └────────────┬──────────────────┘  │
│           └──────────────┬────────────┘                     │
│                          ▼                                  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              GameDispatcher                           │  │
│  │  Discovers IGameModule / IGameHandler via Scrutor     │  │
│  │  Calls Validate() → Apply() → persists state         │  │
│  │  Broadcasts StateUpdated to room group               │  │
│  └────────────────────────┬─────────────────────────────┘  │
└───────────────────────────┼────────────────────────────────┘
                            │
                            ▼
┌────────────────────────────────────────────────────────────┐
│              Azure PostgreSQL (single database)             │
│                                                             │
│  Platform schema               │  Game schemas (per game)  │
│  ────────────────────────────  │  ──────────────────────── │
│  users (+ Identity columns)    │  skyline_game_results      │
│  user_logins / user_tokens     │  skyline_player_stats      │
│  rooms  (game_state: JSONB)    │                            │
│  room_players                  │  __EFMigrationsHistory     │
│  action_log                    │    _skyline                │
│  games                         │                            │
│  __EFMigrationsHistory         │  [next_game]_* tables      │
│                                │  __EFMigrationsHistory     │
│  Read-only views to games:     │    _[next_game]            │
│  • users (safe cols only —     │                            │
│    no password/security cols)  │                            │
│  • rooms (no game_state)       │                            │
│  • room_players                │                            │
└────────────────────────────────────────────────────────────┘
```

### 8.2 .NET Aspire AppHost

```csharp
// src/Meepliton.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("meepliton");

var api = builder.AddProject<Projects.Meepliton_Api>("api")
    .WithReference(postgres);

builder.AddNpmApp("frontend", "../apps/frontend")
    .WithReference(api)
    .WithHttpEndpoint(port: 5173);

builder.Build().Run();
```

`dotnet run --project src/Meepliton.AppHost` starts PostgreSQL in Docker, the API (which runs all migrations on startup), and the Vite dev server. The Aspire dashboard at `http://localhost:15888` shows structured logs and traces across all services. No extra configuration is needed for SignalR — it runs in-process and just works locally.

### 8.3 Key Request Flows

**Join by code:**
```
User types "TIGER3"
  → POST /api/rooms/join { code: "TIGER3" }
  → SELECT * FROM rooms WHERE join_code = 'TIGER3'
  → Upsert room_players (userId, roomId, connected = true)
  → Return { roomId, gameId, players[] }
  → Frontend navigates to /room/{roomId}
  → hub.invoke("JoinRoom", roomId)
  → Hub adds connection to SignalR group
  → Hub sends StateUpdated with current state to the new connection
```

**Submit game action:**
```
Player interacts with the board
  → hub.invoke("SendAction", roomId, { type: "PlaceTile", tileId: 42 })
  → GameHub → GameDispatcher.HandleAction(roomId, userId, action)
  → Load rooms.game_state from DB
  → IGameHandler.Validate(state, action, userId) → bool
  → If valid:
      newState = IGameHandler.Apply(state, action)
      UPDATE rooms SET game_state = newState, state_version += 1
      INSERT INTO action_log (roomId, userId, action, state_version)
      hub.Clients.Group(roomId).SendAsync("StateUpdated", newState)
  → If invalid:
      hub.Clients.Caller.SendAsync("ActionRejected", { reason })
```

---

## 9. Database & Migration Strategy

### 9.1 Design Principles

The database is a single PostgreSQL instance containing all tables, with clear ownership boundaries enforced by PostgreSQL roles and separate EF Core migration histories:

- **Platform migrations** manage the tables the platform owns: all Identity tables (`users`, `user_logins`, `user_tokens`, `user_claims`, `roles`, `user_roles`, `role_claims`), plus `rooms`, `room_players`, `action_log`, `games`. These never change when a new game is added.
- **Game migrations** manage tables a specific game owns: score history, leaderboards, extended per-room data. Each game's migrations are tracked in their own `__EFMigrationsHistory_{gameId}` table, completely independent of the platform history.
- **Read-only access** is granted to games via a dedicated PostgreSQL role (`meepliton_game_reader`) that can SELECT on specific platform tables. Games see a safe, stable view of the data they legitimately need (display name, avatar, room membership) without access to any authentication columns (`password_hash`, `security_stamp`, `email`, lockout state) and cannot write anything.

### 9.2 PostgreSQL Roles

```sql
-- Platform role: full access to platform-owned tables
CREATE ROLE meepliton_platform LOGIN PASSWORD '...' ;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO meepliton_platform;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO meepliton_platform;

-- Game reader role: read-only access to safe platform tables
-- All game roles inherit from this
CREATE ROLE meepliton_game_reader NOLOGIN;
-- Games can read non-sensitive user columns (display_name, avatar_url, theme)
-- They CANNOT read: password_hash, security_stamp, email, phone_number,
--   lockout_end, access_failed_count, or any other auth/security column.
-- This is enforced by the UserView (see §9.5) which exposes only safe columns.
GRANT SELECT ON TABLE users TO meepliton_game_reader;
GRANT SELECT ON TABLE room_players TO meepliton_game_reader;
GRANT SELECT ON TABLE rooms TO meepliton_game_reader;
-- Games do NOT get access to action_log, user_logins, user_tokens, user_claims
-- They receive game state only through the IGameReducer contract

-- Per-game role: inherits reader, can manage its own tables
CREATE ROLE meepliton_game_skyline LOGIN PASSWORD '...' IN ROLE meepliton_game_reader;
GRANT CREATE ON SCHEMA public TO meepliton_game_skyline;
ALTER DEFAULT PRIVILEGES FOR ROLE meepliton_game_skyline IN SCHEMA public
    GRANT ALL ON TABLES TO meepliton_game_skyline;
```

### 9.3 Platform Schema

The platform schema is generated by EF Core migrations from `PlatformDbContext`, which inherits from `IdentityDbContext<ApplicationUser>`. The tables below are grouped by owner: **Identity tables** (managed by the framework), **Meepliton extension** (extra columns on the user), and **Platform tables** (owned by Meepliton).

All Identity table names are remapped to snake_case in `OnModelCreating` to match the project's PostgreSQL conventions.

#### Identity Tables (framework-generated, do not edit manually)

```sql
-- Applied by PlatformDbContext migrations via IdentityDbContext<ApplicationUser>
-- Migration history: __EFMigrationsHistory

-- Core user table — ApplicationUser extends this
-- PasswordHash is nullable: NULL for Google-only users, bcrypt hash for email/password users
CREATE TABLE users (
    id                     TEXT PRIMARY KEY,          -- Identity default: string GUID
    user_name              VARCHAR(256),
    normalized_user_name   VARCHAR(256) UNIQUE,
    email                  VARCHAR(256),
    normalized_email       VARCHAR(256),
    email_confirmed        BOOLEAN NOT NULL DEFAULT false,
    password_hash          TEXT,                      -- NULL for social-only accounts
    security_stamp         TEXT,                      -- rotated on credential change
    concurrency_stamp      TEXT,
    phone_number           TEXT,
    phone_number_confirmed BOOLEAN NOT NULL DEFAULT false,
    two_factor_enabled     BOOLEAN NOT NULL DEFAULT false,
    lockout_end            TIMESTAMPTZ,               -- set during lockout window
    lockout_enabled        BOOLEAN NOT NULL DEFAULT true,
    access_failed_count    INT NOT NULL DEFAULT 0,
    -- Meepliton-specific columns (from ApplicationUser):
    display_name           TEXT NOT NULL DEFAULT '',
    avatar_url             TEXT,
    theme                  TEXT NOT NULL DEFAULT 'system',
    created_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen_at           TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX ix_users_normalized_email    ON users(normalized_email);
CREATE INDEX ix_users_normalized_username ON users(normalized_user_name);

-- External login provider links (one row per provider per user)
-- LoginProvider: e.g. "Google", "Microsoft"
-- ProviderKey:   the provider's unique ID for this user (Google subject ID)
CREATE TABLE user_logins (
    login_provider        TEXT NOT NULL,
    provider_key          TEXT NOT NULL,
    provider_display_name TEXT,
    user_id               TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    PRIMARY KEY (login_provider, provider_key)
);

CREATE INDEX ix_user_logins_user ON user_logins(user_id);

-- Tokens: password reset tokens, email confirmation tokens, 2FA recovery codes
CREATE TABLE user_tokens (
    user_id        TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    login_provider TEXT NOT NULL,
    name           TEXT NOT NULL,
    value          TEXT,
    PRIMARY KEY (user_id, login_provider, name)
);

-- Extra claims stored against a user
CREATE TABLE user_claims (
    id          SERIAL PRIMARY KEY,
    user_id     TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    claim_type  TEXT,
    claim_value TEXT
);

CREATE INDEX ix_user_claims_user ON user_claims(user_id);

-- Role system (created by migrations, unused in v1)
CREATE TABLE roles (
    id                 TEXT PRIMARY KEY,
    name               VARCHAR(256),
    normalized_name    VARCHAR(256) UNIQUE,
    concurrency_stamp  TEXT
);

CREATE TABLE user_roles (
    user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_id TEXT NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, role_id)
);

CREATE TABLE role_claims (
    id          SERIAL PRIMARY KEY,
    role_id     TEXT NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    claim_type  TEXT,
    claim_value TEXT
);
```

#### Platform Tables (Meepliton-owned)

```sql
-- Game catalogue — upserted from IGameModule assembly scan on startup
CREATE TABLE games (
    id              TEXT PRIMARY KEY,               -- e.g. "skyline"
    name            TEXT NOT NULL,
    description     TEXT,
    min_players     INT NOT NULL,
    max_players     INT NOT NULL,
    thumbnail_url   TEXT,
    allow_late_join BOOLEAN NOT NULL DEFAULT false,
    supports_async  BOOLEAN NOT NULL DEFAULT false,
    registered_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE rooms (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    join_code      CHAR(6) UNIQUE NOT NULL,
    game_id        TEXT NOT NULL REFERENCES games(id),
    host_id        TEXT NOT NULL REFERENCES users(id),  -- TEXT to match Identity's user ID type
    status         TEXT NOT NULL DEFAULT 'waiting',     -- waiting | in_progress | finished | closed
    game_state     JSONB,
    game_options   JSONB,
    state_version  INT NOT NULL DEFAULT 0,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at     TIMESTAMPTZ
);

CREATE INDEX ix_rooms_join_code  ON rooms(join_code);
CREATE INDEX ix_rooms_status     ON rooms(status);
CREATE INDEX ix_rooms_expires_at ON rooms(expires_at);
CREATE INDEX ix_rooms_game_id    ON rooms(game_id);

CREATE TABLE room_players (
    room_id     UUID NOT NULL REFERENCES rooms(id) ON DELETE CASCADE,
    user_id     TEXT NOT NULL REFERENCES users(id),    -- TEXT to match Identity's user ID type
    seat_index  INT,
    connected   BOOLEAN NOT NULL DEFAULT true,
    joined_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (room_id, user_id)
);

CREATE INDEX ix_room_players_user ON room_players(user_id);

-- Append-only action history — enables replay, debugging, and future undo
CREATE TABLE action_log (
    id             BIGSERIAL PRIMARY KEY,
    room_id        UUID NOT NULL REFERENCES rooms(id) ON DELETE CASCADE,
    user_id        TEXT NOT NULL REFERENCES users(id), -- TEXT to match Identity's user ID type
    action         JSONB NOT NULL,
    state_version  INT NOT NULL,
    applied_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX ix_action_log_room ON action_log(room_id, state_version);
```

> **User ID type note:** ASP.NET Core Identity uses `string` (GUID as text) for user IDs by default. All platform tables that reference users use `TEXT` to match. This is the standard Identity pattern and works correctly with EF Core's foreign key tracking. If you prefer `UUID`, you can use `IdentityUser<Guid>` as the base class — this requires replacing `string` with `Guid` throughout and is a one-time setup decision that cannot easily be changed later.

### 9.4 Game Schema Example: Skyline

```sql
-- Applied by SkylineDbContext migrations
-- Migration history: __EFMigrationsHistory_skyline
-- Connection role: meepliton_game_skyline

-- Supplementary data: primary game state lives in rooms.game_state (see ADR-004)

CREATE TABLE skyline_game_results (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    room_id        UUID NOT NULL,                   -- references platform rooms; app-enforced, not FK
    final_scores   JSONB NOT NULL,                  -- { playerId: score }
    winning_chains JSONB NOT NULL,
    completed_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX ix_skyline_results_room ON skyline_game_results(room_id);

CREATE TABLE skyline_player_stats (
    user_id        UUID NOT NULL PRIMARY KEY,        -- references platform users; app-enforced
    games_played   INT NOT NULL DEFAULT 0,
    games_won      INT NOT NULL DEFAULT 0,
    total_score    BIGINT NOT NULL DEFAULT 0,
    last_played_at TIMESTAMPTZ
);
```

> **Cross-schema FK policy:** Foreign keys between game tables and platform tables (e.g. `skyline_game_results.room_id → rooms.id`) are enforced at the application layer only, not as database-level constraints. This keeps game migrations independent of the platform schema — a game migration cannot fail because a platform table hasn't been created yet.

### 9.5 EF Core DbContext Setup

```csharp
// src/Meepliton.Api/Identity/ApplicationUser.cs
// Extends IdentityUser with Meepliton-specific profile columns.
// All Identity columns (email, password hash, lockout, etc.) come from IdentityUser.
public class ApplicationUser : IdentityUser
{
    public string  DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl   { get; set; }
    public string  Theme       { get; set; } = "system"; // "light" | "dark" | "system"
    public DateTimeOffset CreatedAt   { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt  { get; set; } = DateTimeOffset.UtcNow;
}

// src/Meepliton.Api/Data/PlatformDbContext.cs
// Inherits from IdentityDbContext<ApplicationUser> — this generates all Identity tables.
// Also owns the platform game/room tables.
public class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Room>       Rooms       => Set<Room>();
    public DbSet<RoomPlayer> RoomPlayers => Set<RoomPlayer>();
    public DbSet<ActionLog>  ActionLog   => Set<ActionLog>();
    public DbSet<Game>       Games       => Set<Game>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // MUST be called first — sets up all Identity tables

        // Rename Identity tables to snake_case to match project conventions
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<string>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("role_claims");

        // Platform table configuration
        builder.Entity<Room>(e => {
            e.ToTable("rooms");
            e.Property(r => r.GameState).HasColumnType("jsonb");
            e.Property(r => r.GameOptions).HasColumnType("jsonb");
        });
        builder.Entity<RoomPlayer>().ToTable("room_players");
        builder.Entity<ActionLog>(e => {
            e.ToTable("action_log");
            e.Property(a => a.Action).HasColumnType("jsonb");
        });
        builder.Entity<Game>().ToTable("games");
    }
}

// src/Meepliton.Api/Program.cs — wiring Identity and auth providers
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.SignIn.RequireConfirmedEmail = true; // email/password accounts require confirmation
    })
    .AddEntityFrameworkStores<PlatformDbContext>()
    .AddDefaultTokenProviders(); // enables password reset and email confirmation tokens

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme          = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options => { /* configure token validation */ })
    .AddGoogle(options =>
    {
        options.ClientId     = builder.Configuration["Auth:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Auth:Google:ClientSecret"]!;
        options.ClaimActions.MapJsonKey("picture", "picture"); // maps avatar URL claim
    });

// src/Meepliton.Contracts/IGameDbContext.cs
public interface IGameDbContext
{
    string GameId { get; }
    Task MigrateAsync(CancellationToken ct = default);
}

// src/games/Meepliton.Games.Skyline/SkylineDbContext.cs
public class SkylineDbContext(DbContextOptions<SkylineDbContext> options)
    : DbContext(options), IGameDbContext
{
    public string GameId => "skyline";

    // Game-owned tables
    public DbSet<SkylineGameResult>  GameResults  => Set<SkylineGameResult>();
    public DbSet<SkylinePlayerStats> PlayerStats  => Set<SkylinePlayerStats>();

    // Read-only access to platform tables via the meepliton_game_reader role.
    // Keyless entities — EF Core generates no migrations for these.
    // Note: UserView exposes only safe columns — no password_hash, no security_stamp.
    public DbSet<RoomView>       Rooms       => Set<RoomView>();
    public DbSet<RoomPlayerView> RoomPlayers => Set<RoomPlayerView>();
    public DbSet<UserView>       Users       => Set<UserView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SkylineGameResult>().ToTable("skyline_game_results");
        modelBuilder.Entity<SkylinePlayerStats>().ToTable("skyline_player_stats");

        modelBuilder.Entity<RoomView>().ToTable("rooms").HasNoKey();
        modelBuilder.Entity<RoomPlayerView>().ToTable("room_players").HasNoKey();
        modelBuilder.Entity<UserView>().ToTable("users").HasNoKey();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_skyline"));
    }

    public async Task MigrateAsync(CancellationToken ct = default)
        => await Database.MigrateAsync(ct);
}
```

### 9.6 Startup Migration Runner

```csharp
// src/Meepliton.Api/Services/MigrationRunner.cs
public class MigrationRunner(
    PlatformDbContext platformContext,
    IEnumerable<IGameDbContext> gameContexts,
    ILogger<MigrationRunner> logger)
{
    public async Task RunAllAsync(CancellationToken ct = default)
    {
        // 1. Platform migrations always run first
        logger.LogInformation("Applying platform migrations...");
        await platformContext.Database.MigrateAsync(ct);
        logger.LogInformation("Platform migrations complete.");

        // 2. Game migrations run in GameId order (deterministic)
        foreach (var ctx in gameContexts.OrderBy(g => g.GameId))
        {
            logger.LogInformation("Applying migrations for {GameId}...", ctx.GameId);
            await ctx.MigrateAsync(ct);
            logger.LogInformation("Migrations complete for {GameId}.", ctx.GameId);
        }
    }
}
```

### 9.7 Running Migrations

**Platform migration:**
```bash
dotnet ef migrations add AddRoomExpiry \
  --project src/Meepliton.Api \
  --context PlatformDbContext
```

**Game migration:**
```bash
dotnet ef migrations add AddLeaderboard \
  --project src/games/Meepliton.Games.Skyline \
  --context SkylineDbContext
```

Each runs independently. Platform and game migration histories never interfere with each other.

---

## 10. Game Module System

### 10.1 Design Philosophy

The platform is a **thin host**, not a game framework. It handles identity, rooms, real-time transport, and persistence of an opaque state blob. Everything else — rules, state shape, UI rendering, history management, undo/redo — belongs to the game module. See ADR-008 and ADR-009.

The two things the platform needs from a game module:

1. **An initial state** when a room starts
2. **A handler** that takes the current state and an incoming action, validates it, and returns the new state

Everything else is the game's business.

### 10.2 Backend Contracts (`Meepliton.Contracts`)

The contract surface is intentionally minimal. It defines the boundary between the platform and a game, not the internals of the game.

```csharp
// The metadata the platform needs to list and start a game
public interface IGameModule
{
    string  GameId      { get; }
    string  Name        { get; }
    string  Description { get; }
    int     MinPlayers  { get; }
    int     MaxPlayers  { get; }
    bool    AllowLateJoin { get; }
    bool    SupportsAsync { get; }
    bool    SupportsUndo  { get; }  // declares whether the game handles "Undo" actions
    string? ThumbnailUrl  { get; }

    // Called once when the host starts the game.
    // Returns whatever JSON blob this game uses as its initial state.
    // The platform stores this in rooms.game_state and knows nothing about its structure.
    JsonDocument CreateInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options);
}

// The handler the platform calls for every incoming action.
// Replaces the old IGameReducer — the name reflects that games are not required to be
// purely functional reducers; they can maintain any internal logic they need.
public interface IGameHandler
{
    string GameId { get; }

    // Receives the full context: current state, the incoming action, and who sent it.
    // Returns the result: new state, an optional rejection reason, and optional side effects.
    // The platform calls this for every action received via SignalR.
    GameResult Handle(GameContext context);
}

public record PlayerInfo(string Id, string DisplayName, string? AvatarUrl, int SeatIndex);

public record GameContext(
    JsonDocument CurrentState,
    JsonDocument Action,
    string       PlayerId,      // the Identity user ID of the player taking the action
    string       RoomId,
    int          StateVersion
);

public record GameResult(
    JsonDocument NewState,       // persisted to rooms.game_state and broadcast to all players
    string?      RejectionReason = null,  // non-null = action rejected, state unchanged
    GameEffect[] Effects = []    // optional: notifications, end-game signal, etc.
);

// Side effects the platform can act on after a successful action
public abstract record GameEffect;
public record GameOverEffect(string? WinnerId) : GameEffect;
public record NotifyEffect(string PlayerId, string Message) : GameEffect;
```

**The platform calls `Handle()` and trusts the result.** The game decides internally how to validate, whether to use a state machine, event sourcing, a plain reducer, or anything else. If `RejectionReason` is non-null, the platform rejects the action and sends an `ActionRejected` message to the caller. Otherwise it persists `NewState` and broadcasts it.

**Typed convenience base** — optional, for games that suit a pure reducer style:

```csharp
// Games that are naturally action→state reducers can use this base class.
// Games with complex internal logic should implement IGameModule + IGameHandler directly.
public abstract class ReducerGameModule<TState, TAction, TOptions>
    : IGameModule, IGameHandler
    where TState   : class
    where TAction  : class
    where TOptions : class
{
    // Metadata — implement these
    public abstract string GameId { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract int MinPlayers { get; }
    public abstract int MaxPlayers { get; }
    public virtual bool AllowLateJoin => false;
    public virtual bool SupportsAsync => false;
    public virtual bool SupportsUndo  => false;
    public virtual string? ThumbnailUrl => null;

    // Game logic — implement these
    public abstract TState CreateInitialState(IReadOnlyList<PlayerInfo> players, TOptions? options);
    public abstract string? Validate(TState state, TAction action, string playerId);  // null = valid
    public abstract TState  Apply(TState state, TAction action);

    // IGameHandler bridge — games don't call this directly
    public GameResult Handle(GameContext ctx)
    {
        var state  = Deserialize<TState>(ctx.CurrentState);
        var action = Deserialize<TAction>(ctx.Action);
        var error  = Validate(state, action, ctx.PlayerId);
        if (error is not null) return new GameResult(ctx.CurrentState, error);
        var newState = Apply(state, action);
        return new GameResult(Serialize(newState));
    }

    // JSON helpers
    protected static T Deserialize<T>(JsonDocument doc) => JsonSerializer.Deserialize<T>(doc)!;
    protected static JsonDocument Serialize<T>(T obj) => JsonDocument.Parse(JsonSerializer.Serialize(obj));

    // IGameModule bridge
    JsonDocument IGameModule.CreateInitialState(IReadOnlyList<PlayerInfo> p, JsonDocument? o)
        => Serialize(CreateInitialState(p, o is null ? null : Deserialize<TOptions>(o)));
}
```

### 10.3 Frontend Contract (`@meepliton/contracts`)

The frontend contract is equally minimal. A game module is a React component that receives the current state and a dispatch function. How it renders is entirely up to the game.

```typescript
// packages/contracts/src/GameModule.ts

export interface PlayerInfo {
  id: string;
  displayName: string;
  avatarUrl: string | null;
  seatIndex: number;
  connected: boolean;
}

// Everything a game component receives from the platform
export interface GameContext<TState> {
  state:       TState;
  players:     PlayerInfo[];
  myPlayerId:  string;
  roomId:      string;
  dispatch:    (action: unknown) => void;  // sends to SignalR hub
}

// A game module is a named React component that renders itself
export interface GameModule<TState = unknown> {
  gameId:    string;
  Component: React.FC<GameContext<TState>>;
}
```

**No `isMyTurn`, no `HandComponent`, no `SidebarComponent`.** These were convenience props that assumed all games are turn-based card games. The game knows whether it's the current player's turn — that's part of `state`. The game decides its own layout. A map-based game, a real-time negotiation game, or a simultaneous-action game all have different notions of "whose turn is it".

**Frontend registry** — the only file changed when adding a new game:

```typescript
// apps/frontend/src/games/registry.ts
export const gameRegistry: Record<string, () => Promise<{ default: GameModule }>> = {
  skyline: () => import("./skyline"),
  // Add new games here ↓
};
```

### 10.4 Platform Chrome vs Game UI

The platform provides only what every game room needs regardless of the game being played. Everything else is the game's concern.

**Platform provides (in `@meepliton/ui`):**

| Component | Purpose |
|---|---|
| `<PlayerPresence />` | Shows connected/disconnected status for each player in the room |
| `<JoinCodeDisplay />` | Large readable join code with copy button — shown on waiting screen |
| `<QRCode />` | QR code from join URL |
| `<RoomWaitingScreen />` | Pre-game lobby: player list, join code, start button for host |
| `<ActionRejectedToast />` | Displayed when the server rejects an action |
| Design token CSS | `tokens.css` — colour, typography, spacing tokens. Games may use or ignore. |

**Games are responsible for:**
- All game board rendering (SVG, Canvas, Three.js, plain HTML/CSS — any approach)
- All game-specific controls and interactions
- Layout on mobile and desktop
- Any game-specific animations
- Undo/redo UI if the game supports it
- Any game-specific overlays, modals, or panels

**Games may use any npm package they need.** There is no restriction on what a game module can import. A game that needs a hex grid library, a physics engine, or a WebGL renderer can bring it in as a dependency of its own package.

### 10.5 State, History, and Undo

The platform stores one thing per room: the current authoritative state (`rooms.game_state`, JSONB). The platform also writes an append-only `action_log` row for every accepted action.

How a game uses these is up to the game:

**Option A — Snapshot only (simplest).** The game's `Handle()` returns a complete new state. The platform stores it. History is available in `action_log` but the game doesn't use it. This works for most straightforward games.

**Option B — State includes history.** The game embeds its own history inside the state blob — e.g. `{ currentBoard: ..., moveHistory: [...] }`. Undo is implemented by `Handle()` responding to an `"Undo"` action by popping from `moveHistory` and returning the rolled-back board. The platform sees this as a normal state update and broadcasts it to all players. This is the recommended approach for games that want undo: keep the logic in the game, keep the platform simple.

**Option C — Replay from action log.** A game that needs deterministic replay (for spectating, for debugging, or for complex branching history) can read its own `action_log` entries. This requires the game to have a `DbContext` with read access to `action_log`. This is more complex and should only be used when Option B is insufficient.

In all cases, **undo is a game-level concern**. The platform does not know or care whether a state update is a forward move or a rollback. It stores and broadcasts whatever the game returns.

---

## 11. API Design

### 11.1 REST Endpoints

```
GET  /api/health                           → 200 OK (Container Apps health probe)

─── Email / Password ──────────────────────────────────────────────────────────
POST /api/auth/register                    → { email, password, displayName } → 201
                                              Creates account, sends confirmation email
POST /api/auth/confirm-email               → { userId, token } → 204
POST /api/auth/login                       → { email, password } → 200 + sets cookie
POST /api/auth/logout                      → clears cookie → 204
POST /api/auth/forgot-password             → { email } → 204 (always; no email enumeration)
POST /api/auth/reset-password              → { userId, token, newPassword } → 204
POST /api/auth/add-password                → { newPassword } [auth required] → 204
                                              Adds password to a Google-only account

─── Google OAuth ──────────────────────────────────────────────────────────────
GET  /api/auth/google                      → redirects to Google consent screen
GET  /api/auth/google/callback             → handles OAuth return, sets cookie → redirect /lobby
POST /api/auth/link/google                 → [auth required] links Google to existing account

─── Account ───────────────────────────────────────────────────────────────────
GET  /api/auth/me                          → UserDto (id, displayName, avatarUrl, theme, loginMethods[])
PUT  /api/auth/me                          → { displayName?, avatarUrl?, theme? } → 204
GET  /api/auth/me/login-methods            → LoginMethodDto[] (which providers are linked)

─── Lobby & Rooms ─────────────────────────────────────────────────────────────
GET  /api/lobby                            → LobbyDto  (my rooms + game catalogue)
GET  /api/games                            → GameInfoDto[]
POST /api/rooms                            → CreateRoomRequest → RoomDto
GET  /api/rooms/{roomId}                   → RoomDto
POST /api/rooms/join                       → { code: string } → RoomDto
POST /api/rooms/{roomId}/start             → host only → 204
DELETE /api/rooms/{roomId}                 → host only → 204
POST /api/rooms/{roomId}/transfer-host     → { newHostId: string } → 204
```

#### Key implementation notes

**No email enumeration on forgot-password.** `POST /api/auth/forgot-password` always returns `204` regardless of whether the email exists in the database. This prevents user discovery via the reset flow.

**RequireConfirmedEmail applies only to email/password accounts.** Google-authenticated users do not need to confirm their email — Google has already verified it. This is implemented by checking `info.Principal.FindFirstValue(ClaimTypes.Email)` and calling `userManager.SetEmailConfirmedAsync(user, true)` after creating an account from Google OAuth.

**Cookie vs Bearer token.** The JWT is stored in an HttpOnly, SameSite=Strict cookie. The frontend never sees the token — it simply makes requests and the browser attaches the cookie automatically. This is appropriate for a same-origin SPA. SignalR connections use the cookie for authentication via `options.Events.OnMessageReceived`.

### 11.2 SignalR Hub Methods

```csharp
// Client → Server
Task JoinRoom(string roomId);
Task SendAction(string roomId, JsonDocument action);
Task LeaveRoom(string roomId);

// Server → Client
Task StateUpdated(JsonDocument newState);
Task PlayerJoined(PlayerInfo player);
Task PlayerLeft(string playerId);
Task PlayerConnected(string playerId);
Task PlayerDisconnected(string playerId);
Task ActionRejected(ActionError error);
Task GameStarted(JsonDocument initialState);
Task GameFinished(GameResult result);
```

---

## 12. Infrastructure & Deployment

### 12.1 Environments

| | Local (Aspire) | Production |
|---|---|---|
| API | `dotnet run` via Aspire | Azure Container Apps — Consumption |
| Database | PostgreSQL Docker container | Azure PostgreSQL Flexible — B1ms |
| Frontend | `vite dev` via Aspire | Azure Static Web Apps — Free |
| SignalR | In-process, no config needed | In-process, same code, no change |
| Domain | `localhost:5173` / `:5000` | `meepliton.com` / `api.meepliton.com` |

### 12.2 Azure Resources

```
Resource Group: rg-meepliton-prod
│
├── Azure Container Registry: meeplitonacr  (Basic ~$5/month)
├── Azure Database for PostgreSQL Flexible: meepliton-db  (B1ms ~$13/month)
│     └── Database: meepliton
├── Azure Container Apps Environment: meepliton-env  (Consumption)
│     └── Container App: meepliton-api
│           ├── Custom domain: api.meepliton.com
│           ├── minReplicas: 0  (scale to zero)
│           └── Health probe: GET /api/health
└── Azure Static Web Apps: meepliton-frontend  (Free)
      └── Custom domain: meepliton.com
```

### 12.3 Container App Scale Settings

```yaml
scale:
  minReplicas: 0
  maxReplicas: 3
  rules:
    - name: http-scaling
      http:
        metadata:
          concurrentRequests: "20"
```

### 12.4 API Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/", "."]
RUN dotnet restore "Meepliton.Api/Meepliton.Api.csproj"
RUN dotnet build  "Meepliton.Api/Meepliton.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Meepliton.Api/Meepliton.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Meepliton.Api.dll"]
```

---

## 13. GitHub Actions CI/CD

### 13.1 Repository Secrets

```
AZURE_CREDENTIALS                    # az ad sp create-for-rbac output (JSON)
ACR_LOGIN_SERVER                     # meeplitonacr.azurecr.io
ACR_USERNAME
ACR_PASSWORD
AZURE_CONTAINER_APP_NAME             # meepliton-api
AZURE_RESOURCE_GROUP                 # rg-meepliton-prod
DATABASE_PLATFORM_CONN_STR           # privileged connection for platform migrations
DATABASE_GAME_MIGRATION_CONN_STR     # privileged connection for game migrations
SWA_DEPLOYMENT_TOKEN                 # from Azure Static Web Apps resource
GOOGLE_CLIENT_ID
GOOGLE_CLIENT_SECRET
JWT_SECRET                           # random 256-bit string
EMAIL_SENDER_API_KEY                 # SendGrid API key (or SMTP credentials)
EMAIL_FROM_ADDRESS                   # e.g. noreply@meepliton.com
```

> Two migration connection strings are used. Both point at the same database but use a privileged role that has `CREATE TABLE` rights. The application itself uses `meepliton_platform` and per-game `meepliton_game_{id}` roles at runtime, which have only the access they need.

### 13.2 Pipeline

```yaml
# .github/workflows/deploy.yml
name: Build and Deploy

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Build
        run: dotnet build src/Meepliton.sln --configuration Release

      - name: Test
        run: dotnet test src/Meepliton.sln --configuration Release --no-build

      # Migrations run BEFORE deploying the new image so schema is ready when the container starts
      - name: Apply Platform Migrations
        if: github.ref == 'refs/heads/main'
        run: |
          dotnet tool install --global dotnet-ef
          dotnet ef database update \
            --project src/Meepliton.Api \
            --context PlatformDbContext \
            --connection "${{ secrets.DATABASE_PLATFORM_CONN_STR }}"

      - name: Apply Game Migrations
        if: github.ref == 'refs/heads/main'
        run: |
          dotnet ef database update \
            --project src/games/Meepliton.Games.Skyline \
            --context SkylineDbContext \
            --connection "${{ secrets.DATABASE_GAME_MIGRATION_CONN_STR }}"
          # Add one line here for each new game added to the project

      - uses: docker/login-action@v3
        if: github.ref == 'refs/heads/main'
        with:
          registry: ${{ secrets.ACR_LOGIN_SERVER }}
          username: ${{ secrets.ACR_USERNAME }}
          password: ${{ secrets.ACR_PASSWORD }}

      - uses: docker/build-push-action@v5
        if: github.ref == 'refs/heads/main'
        with:
          context: .
          file:    src/Meepliton.Api/Dockerfile
          push:    true
          tags: |
            ${{ secrets.ACR_LOGIN_SERVER }}/meepliton-api:latest
            ${{ secrets.ACR_LOGIN_SERVER }}/meepliton-api:${{ github.sha }}

      - uses: azure/login@v2
        if: github.ref == 'refs/heads/main'
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Deploy to Container Apps
        if: github.ref == 'refs/heads/main'
        run: |
          az containerapp update \
            --name ${{ secrets.AZURE_CONTAINER_APP_NAME }} \
            --resource-group ${{ secrets.AZURE_RESOURCE_GROUP }} \
            --image ${{ secrets.ACR_LOGIN_SERVER }}/meepliton-api:${{ github.sha }}

  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: apps/frontend/package-lock.json

      - name: Build
        working-directory: apps/frontend
        run: |
          npm ci
          npm run build
        env:
          VITE_API_URL: https://api.meepliton.com

      - uses: Azure/static-web-apps-deploy@v1
        if: github.ref == 'refs/heads/main'
        with:
          azure_static_web_apps_api_token: ${{ secrets.SWA_DEPLOYMENT_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: upload
          app_location: apps/frontend/dist
          skip_app_build: true
```

> **Adding a new game to CI:** When a new game module is added, add one `dotnet ef database update` call to the "Apply Game Migrations" step. This is the only CI change required for a new game.

---

## 14. Repository Structure

```
meepliton/
├── .github/
│   └── workflows/
│       └── deploy.yml
│
├── src/
│   ├── Meepliton.sln
│   ├── Meepliton.AppHost/                     # .NET Aspire (local dev orchestration)
│   ├── Meepliton.Api/                         # ASP.NET Core API + SignalR
│   │   ├── Hubs/GameHub.cs
│   │   ├── Endpoints/                      # Minimal API endpoint files
│   │   ├── Services/
│   │   │   ├── GameDispatcher.cs
│   │   │   └── MigrationRunner.cs
│   │   ├── Data/PlatformDbContext.cs
│   │   ├── Migrations/                     # Platform migration history
│   │   └── Dockerfile
│   ├── Meepliton.Contracts/                   # IGameModule, IGameReducer, GameModule<,,>, IGameDbContext
│   ├── Meepliton.Tests/
│   └── games/
│       ├── Meepliton.Games.Skyline/
│       │   ├── SkylineModule.cs            # IGameModule + IGameReducer
│       │   ├── SkylineDbContext.cs         # Game-owned EF Core context
│       │   ├── Migrations/                 # Skyline migration history
│       │   └── Models/
│       └── Meepliton.Games.[NextGame]/        # Same structure for every new game
│
├── apps/
│   └── frontend/
│       ├── src/
│       │   ├── platform/                  # Auth, lobby, room chrome, theme
│       │   ├── games/
│       │   │   ├── registry.ts            # ← only file edited when adding a game
│       │   │   ├── skyline/
│       │   │   └── [next-game]/
│       │   └── main.tsx
│       └── package.json
│
├── packages/
│   ├── contracts/                         # @meepliton/contracts (TypeScript types)
│   └── ui/                                # @meepliton/ui (shared React components)
│       └── src/styles/
│           └── tokens.css                 # Design token CSS — all CSS vars, base styles
│
├── scripts/
│   └── new-game.ps1                       # Game scaffolder — run this to create a new game
│
├── docs/
│   └── requirements.md                    # ← this file
│
├── .claude/                               # Claude AI project context (committed to git)
│   ├── settings.json                      # Shared project permissions + hooks (all contributors)
│   ├── skills/                            # Skill files — attach to Claude conversations
│   │   ├── GIT-WORKFLOW.md                # ← attach for git/GitHub help (non-developers especially)
│   │   ├── NEW-GAME.md                    # ← attach when building a new game
│   │   ├── THEME.md                       # ← attach when building any Meepliton UI
│   │   ├── PLATFORM.md                    # ← attach when debugging platform/auth
│   │   └── GAME-MODULE.md                 # ← attach when implementing game logic
│   └── commands/                          # Claude Code slash commands
│       └── scaffold-game.md               # /scaffold-game — guided new game walkthrough
│
├── .claude/settings.local.json            # ← gitignored: personal Claude settings + overrides
│
├── CLAUDE.md                              # Project overview — auto-read by Claude Code
│
└── README.md
```

> **`.gitignore` additions:** Add `.claude/settings.local.json` to `.gitignore`. This file holds personal Claude preferences (custom API keys, local model overrides) that should not be shared. Everything else in `.claude/` is committed — shared permissions, hooks, skill files, and slash commands all benefit every contributor.

---

## 15. Game Scaffolding

### 15.1 Overview

Adding a new game should take minutes, not hours. The scaffolding system has two parts that work together:

- **`scripts/new-game.ps1`** — a PowerShell script that creates all files and wires everything up
- **`docs/skills/NEW-GAME.md`** — a Claude skill file that helps design and implement the actual game logic

The script handles structure; the skill handles thinking. Together they take a game from idea to running code.

### 15.2 Scaffold Script

```powershell
# From the repository root:
./scripts/new-game.ps1 -GameId sushigo -GameName "Sushi Go" -MinPlayers 2 -MaxPlayers 5
```

**Parameters:**

| Parameter | Required | Description |
|---|---|---|
| `-GameId` | Yes | Lowercase letters and numbers only. e.g. `sushigo`, `coinflip`, `skyline` |
| `-GameName` | Yes | Human-readable display name. e.g. `"Sushi Go"` |
| `-Description` | No | Short description shown in the game catalogue |
| `-MinPlayers` | No | Default: 2 |
| `-MaxPlayers` | No | Default: 6 |

**What the script does:**

1. Creates `src/games/Meepliton.Games.{Pascal}/` with a complete C# project: module class with `TODO` stubs, state model records, DbContext with read-only platform views, project file with correct package references, empty Migrations folder, and a game-specific README.

2. Creates `apps/frontend/src/games/{gameId}/` with: module entry point, TypeScript state type stubs, a bare `Game.tsx` component receiving `GameContext`, and README. No assumptions about rendering approach.

3. Adds the C# project to `Meepliton.sln` under a `Games` solution folder.

4. Inserts one import line into `apps/frontend/src/games/registry.ts`.

**After running the script, two things remain:**
- Implement the `TODO` sections (use Claude with `docs/skills/NEW-GAME.md`)
- Add a migration step to `.github/workflows/deploy.yml` if the game uses a `DbContext`

### 15.3 Typical Workflow

```
1. Think through your game concept (or ask Claude to help design it first)

2. Run the scaffold:
   ./scripts/new-game.ps1 -GameId mygame -GameName "My Game"

3. Open a Claude conversation, attach `.claude/skills/NEW-GAME.md`

4. Describe your game — be specific about complexity:
   "I've scaffolded a game called My Game. Here's how it works:
    [rules, player count, win condition, key mechanics, rendering approach]
    It needs [undo/simultaneous actions/hidden information/etc.]
    Fill in all the TODO sections."

5. Claude generates complete implementations for all TODO sections.

6. Paste the generated code into the scaffolded files.

7. Run locally: dotnet run --project src/Meepliton.AppHost

8. If the game needs supplementary tables, add EF migrations and a CI step (see §15.5).
```

### 15.4 Generated File Map

After `./scripts/new-game.ps1 -GameId sushigo -GameName "Sushi Go"`:

```
src/games/Meepliton.Games.Sushigo/
├── Meepliton.Games.Sushigo.csproj
├── SushigoModule.cs              ← implement: CreateInitialState + Handle (or Validate + Apply if using ReducerGameModule)
├── SushigoDbContext.cs           ← optional: add game-owned tables here
├── Migrations/                   ← EF migrations land here
├── Models/
│   └── SushigoModels.cs          ← define your state types (no prescribed shape — design for your game)
└── README.md

apps/frontend/src/games/sushigo/
├── index.tsx                     ← module entry point (already wired to registry)
├── types.ts                      ← mirror C# records here
├── styles.module.css
├── components/
│   └── Game.tsx                  ← implement your game UI here (any rendering approach)
└── README.md
```

### 15.5 Adding the Migration Step to CI

When a game uses `DbContext` (supplementary tables), add one step to `.github/workflows/deploy.yml` in the "Apply Game Migrations" section:

```yaml
    - name: Apply Game Migrations
      if: github.ref == 'refs/heads/main'
      run: |
        dotnet ef database update \
          --project src/games/Meepliton.Games.Skyline \
          --context SkylineDbContext \
          --connection "${{ secrets.DATABASE_GAME_MIGRATION_CONN_STR }}"
        # Add new game migration steps below this line:
        dotnet ef database update \
          --project src/games/Meepliton.Games.Sushigo \
          --context SushigoDbContext \
          --connection "${{ secrets.DATABASE_GAME_MIGRATION_CONN_STR }}"
```

This is the **only** change to the CI pipeline when adding a new game that uses supplementary tables.

---

## 16. Claude AI Context Files

Meepliton uses the standard `.claude/` project layout. Claude Code reads `CLAUDE.md` automatically on project open. Skill files give Claude deep context for specific tasks — attach them to conversations as needed.

### The Files

**`CLAUDE.md`** (repo root) — auto-read by Claude Code and Claude.ai. Contains the stack, key paths, how to add a game, and a skill file reference table. Kept short deliberately — it orients Claude, the skill files do the heavy lifting.

**`.claude/settings.json`** — shared project settings committed to git. Defines which shell commands Claude is allowed to run (git, gh, dotnet, npm, pwsh) and which are blocked (direct pushes to main, rm -rf, curl). Also defines two hooks:

- `SessionStart` hook: prints current git branch, status, and unpushed commits so Claude always knows where it is when a session opens
- `PostToolUse` hook on Bash: prints updated git state after any git command, giving non-developer contributors continuous confirmation that each step worked

**`.claude/settings.local.json`** — gitignored. Each contributor's personal settings: API keys, preferred model overrides, personal permission expansions.

### Skill Files

| File | When to attach | Audience |
|---|---|---|
| `GIT-WORKFLOW.md` | Any git or GitHub task | Non-developer contributors especially |
| `NEW-GAME.md` | Designing and building a new game | Anyone adding a game |
| `GAME-MODULE.md` | Implementing game rules and frontend | Anyone adding a game |
| `PLATFORM.md` | Platform architecture, auth, SignalR, database | Debugging or extending the platform |
| `THEME.md` | Building any UI screen or component | Frontend work |

### Slash Commands

**`/scaffold-game`** (`.claude/commands/scaffold-game.md`) — guided walkthrough for new game contributors. Claude asks for the game name, player count, and description, verifies the contributor is on a clean branch, runs `new-game.ps1`, explains the generated files, and optionally proceeds directly to implementation. Designed for non-developer friends who have a game idea and want to get from zero to running code without knowing the toolchain.

### For Non-Developer Contributors

The typical journey for someone who has never used git:

1. Install prerequisites: .NET 10 SDK, Node 20, Docker Desktop, GitHub CLI (`gh`)
2. Clone the repo once: `gh repo clone [username]/meepliton && cd meepliton`
3. Open Claude Code: `claude` in the meepliton folder
4. Type `/scaffold-game` — Claude handles the entire workflow interactively
5. For git help at any point, say "I need to commit my work" or "how do I open a PR"

Claude Code reads the `SessionStart` hook output and always knows the current branch and git state. Attaching `GIT-WORKFLOW.md` gives Claude the full vocabulary to explain every step in plain language — what staging is, why branches exist, how to recover from mistakes. The skill file covers the mental model, the daily commit loop, conflict resolution, and every `gh` CLI command a contributor is likely to need.

### `.gitignore` entries

```gitignore
# Claude personal settings — never commit
.claude/settings.local.json
.claude/settings.local.yml
```

---

### Inline Skill File Content

The PLATFORM.md and GAME-MODULE.md skills are documented inline in this file for reference. The authoritative committed versions live in `.claude/skills/`.


## 17. Open Questions

| # | Question | Impact | Current Position |
|---|---|---|---|
| OQ-01 | Should rooms expire after 48h inactivity or be optionally persistent? | DB size, UX | Default 48h via `expires_at`; nullable for persistent rooms later |
| OQ-02 | In-room text chat? | Hub scope, schema | Deferred; needed only if async play ships |
| OQ-03 | Undo/redo capability per game? | Game module design | Undo is a game responsibility (ADR-009). Games declare `SupportsUndo = true` and handle an `"Undo"` action internally. `action_log` provides the raw material if a game wants replay-based undo. |
| OQ-04 | `minReplicas: 0` or `1`? | ~$5–8/month | Start at 0; upgrade if cold starts disrupt real sessions |
| OQ-05 | GitHub Secrets vs Azure Key Vault? | Ops complexity | GitHub Secrets for v1; Key Vault if secrets need rotation |
| OQ-06 | Auto-discover game migration steps in CI, or keep manual? | CI maintenance | Manual steps for v1; easy to script from solution file later |
| OQ-07 | Player cap above 8? | UI layout | `max_players` on `IGameModule` controls it; platform is uncapped |
| OQ-08 | Which email service for confirmation/reset emails? | Auth flow completeness | SendGrid free tier (100 emails/day) is sufficient for a hobby project; configure via `IEmailSender` abstraction so it can be swapped. SMTP (e.g. Gmail relay) is a free alternative for low volume. |
| OQ-09 | Should Google-authenticated users be required to set a display name on first login, or is the Google profile name always accepted? | Onboarding UX | Accept Google name automatically; allow override in account settings. No friction on first login. |
| OQ-11 | Should there be a video walkthrough or README guide for non-developer contributors to complement the git skill file? | Contributor onboarding | A short screen recording of the /scaffold-game flow would be higher ROI than more written docs. Park until first non-developer contributor tries it. |
| OQ-10 | Should the platform support account deletion? Identity provides `UserManager.DeleteAsync()`. | GDPR / user expectations | Soft-delete (anonymise data) is safer than hard-delete for referential integrity with room history. Park for v2. |

---

## 18. Phased Roadmap

### Phase 1 — Foundation

- [ ] Repository scaffold: solution, Aspire AppHost, Vite frontend, monorepo package structure
- [ ] `Meepliton.Contracts`: `IGameModule`, `IGameReducer`, `GameModule<,,>` base class, `IGameDbContext`
- [ ] PostgreSQL role setup: `meepliton_platform`, `meepliton_game_reader`
- [ ] `PlatformDbContext` + initial migrations: users, games, rooms, room_players, action_log
- [ ] `MigrationRunner`: discovers and applies platform + all game contexts on startup
- [ ] ASP.NET Core Identity wired with `ApplicationUser` and `IdentityDbContext`
- [ ] Email/password registration with email confirmation (`POST /api/auth/register`)
- [ ] Email/password sign-in with lockout (`POST /api/auth/login`)
- [ ] Password reset flow (`POST /api/auth/forgot-password` + `POST /api/auth/reset-password`)
- [ ] Google OAuth via `.AddGoogle()`; auto-create account on first sign-in
- [ ] Account linking: add password to Google account; link Google to email account
- [ ] `GET /api/auth/me` returning login methods alongside profile
- [ ] Transactional email via `IEmailSender` (SendGrid or SMTP)
- [ ] Lobby API: `GET /api/lobby`, `POST /api/rooms`, `POST /api/rooms/join`
- [ ] `GameDispatcher`: load state → validate → reduce → persist → broadcast
- [ ] SignalR `GameHub`: `JoinRoom`, `SendAction`, reconnect with full state push
- [ ] React shell: auth flow, lobby page, room waiting screen, dark/light theme
- [ ] Join code generation, shareable URL (`meepliton.com/join/CODE`), client-side QR code
- [ ] `@meepliton/ui`: platform chrome only — `<RoomWaitingScreen>`, `<PlayerPresence>`, `<JoinCodeDisplay>`, `<QRCode>`, `<ActionRejectedToast>`
- [ ] First game module: **Skyline** (C# module + `SkylineDbContext` + React board)
- [ ] GitHub Actions CI/CD: build → test → migrate → push → deploy
- [ ] Custom domains: `meepliton.com` + `api.meepliton.com`
- [ ] Scaffold script: `scripts/new-game.ps1` tested end-to-end with Skyline as the first game
- [ ] `CLAUDE.md` at repo root with stack, key paths, skill file table
- [ ] `.claude/settings.json` with contributor permissions and SessionStart/PostToolUse git hooks
- [ ] `.claude/settings.local.json` added to `.gitignore`
- [ ] `.claude/skills/`: `GIT-WORKFLOW.md`, `NEW-GAME.md`, `GAME-MODULE.md`, `PLATFORM.md`, `THEME.md`
- [ ] `.claude/commands/scaffold-game.md` — `/scaffold-game` slash command

### Phase 2 — Polish & Second Game

- [ ] Reconnect: full state push on `JoinRoom` for existing seat holders
- [ ] Presence indicators (connected/disconnected dots) in room chrome
- [ ] Action log viewer in room (debug tool for game authors)
- [ ] Mobile optimisation pass across all modules
- [ ] Second game module (validates module system for a genuinely different game type — e.g. map-based or simultaneous-action, not just another tile game)
- [ ] Application Insights: errors + response times
- [ ] Host-only action log rewind (undo to N-1 state)

### Phase 3 — Async & Tooling

- [ ] Async room persistence (rooms survive browser close; opt-in via `SupportsAsync`)
- [ ] Turn notifications (Web Push API)
- [ ] In-room text chat (contingent on async play)
- [ ] Local game dev harness: simulate a room with N bot players without a server
- [ ] `@meepliton/ui` Storybook for platform chrome components
- [ ] Document game rendering patterns (SVG, Canvas, Three.js examples) in `.claude/skills/`

---

*Maintained in `docs/requirements.md` in the meepliton GitHub repository.*
*Architecture decisions recorded in §3 (Architecture Decision Records).*
*Claude skill files for game development are in `docs/skills/`.*
*Last updated: 2026-03-13*
