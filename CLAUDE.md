# Meepliton

Browser-based multiplayer board game platform. Live at meepliton.com.
A small group of friends build and play custom board games here.

## Stack

| Layer | Technology |
|---|---|
| Backend | .NET 9 / ASP.NET Core, SignalR (in-process), EF Core 9 + Npgsql |
| Frontend | React 18 + TypeScript, Vite |
| Database | PostgreSQL 16 (Azure Flexible Server) |
| Auth | ASP.NET Core Identity — email/password + Google OAuth |
| Real-time | ASP.NET Core SignalR, in-process (no Azure SignalR Service) |
| Hosting | Azure Container Apps (API) + Azure Static Web Apps (frontend) |
| Local dev | .NET Aspire |

## Running locally

Prerequisites: .NET 9 SDK, Node 20, Docker Desktop

```bash
dotnet run --project src/Meepliton.AppHost
```

- Frontend: http://localhost:5173
- API: http://localhost:5000
- Aspire dashboard: http://localhost:15888

## Key paths

```
src/Meepliton.Api/              API + SignalR hub + auth endpoints
src/Meepliton.Contracts/        IGameModule, IGameHandler, GameContext, GameResult
src/games/                      One C# project per game
apps/frontend/src/games/        One React module per game
apps/frontend/src/games/registry.ts   ← only file edited when adding a game
packages/ui/src/                Platform chrome components (lobby, room screens)
packages/contracts/src/         TypeScript interfaces (GameModule, GameContext)
scripts/new-game.ps1            Scaffold script for new games
docs/requirements.md            Full architecture, ADRs, and roadmap (entry point)
docs/requirements/              Additional requirement documents (split from requirements.md when topics get large)
docs/stories/                   Active story files — backlog → refined → in-progress
docs/stories/archive/           Completed stories (status: done) — moved here after merging
docs/specs/                     Detailed specs for non-trivial stories (written by analyst)
docs/ui-plans/                  UI implementation plans (written during /ui-design)
docs/owner/TODO.md              Actions only you can take — check this when starting a session
```

## Owner TODO

**`docs/owner/TODO.md`** is the one file you should check regularly. Agents write here when they are blocked by something only a human can resolve — Azure credentials, product decisions, conflict resolution. If this file has items, address them before starting new work.

## Adding a new game

```bash
# 1. Create a branch
git checkout main && git pull && git checkout -b add-{gameid}-game

# 2. Scaffold
./scripts/new-game.ps1 -GameId {gameid} -GameName "{Name}"

# 3. Implement (attach .claude/skills/NEW-GAME.md to a Claude conversation)

# 4. Run locally to test
dotnet run --project src/Meepliton.AppHost

# 5. Commit and open a PR
git add src/games/Meepliton.Games.{Name}/ apps/frontend/src/games/{gameid}/
git commit -m "Add {Name} game module"
gh pr create --title "Add {Name}" --base main --web
```

## Skills

Context skills load automatically when relevant. Collaborative workflow skills are invoked explicitly and orchestrate agents debating before producing output.

### Context skills (auto-load)

| Skill | Loads when… |
|---|---|
| `game-module` | Implementing or reviewing game logic (backend + frontend) |
| `new-game` | Designing a game from scratch |
| `git-workflow` | Git or GitHub tasks |

### Collaborative workflow skills

These skills run **multi-agent debate workflows** before producing any output. Use them before writing code to catch problems early.

| Skill | When to use | Who debates |
|---|---|---|
| `/spec-design <feature>` | Before implementing any new feature or game | `analyst` ↔ `architect` — 2–4 rounds until consensus |
| `/ui-design <screen>` | Before building any new screen or extracting a component | `ux` ↔ `frontend` — design intent vs feasibility |
| `/story-review <spec>` | After stories are written, before implementation begins | `analyst` (adversarial) + `tester` — challenge every assumption |

**How these work:** each workflow runs the named agents in alternating rounds. Agents challenge each other's output directly. A human is only needed if they cannot reach consensus after the maximum rounds. The final output is a document in `docs/specs/` or `docs/ui-plans/` that becomes the implementation source of truth.

## Slash commands

| Command | What it does |
|---|---|
| `/scaffold-game [game-id]` | Guided walkthrough to scaffold a new game module |
| `/spec-design <feature>` | Analyst + architect debate a spec before any code |
| `/ui-design <screen>` | UX + frontend debate a UI plan before building |
| `/story-review <spec>` | Devil's advocate + tester harden stories before implementation |

## Agents

Specialist agents run in isolated contexts with focused tool access. Claude delegates to them automatically, or invoke directly.

| Agent | Role | Triggers automatically when… |
|---|---|---|
| `architect` | Software architect | Structural changes, new game modules, contract reviews |
| `analyst` | Product analyst | Turning ideas into specs or updating roadmap |
| `pm` | Project manager | Planning, status, GitHub issues |
| `backend` | .NET developer | Adding/modifying C# code |
| `frontend` | React developer | Adding/modifying TypeScript/React code |
| `ux` | UX designer | Building UI, reviewing design consistency, extracting components |
| `tester` | QA engineer | After implementing game logic or fixing bugs |
| `devops` | DevOps engineer | CI/CD, migrations, Azure infrastructure |
| `trainer` | Continuous improvement | After stories complete or to review in-progress work, when agents produce poor output, or on a periodic sweep |
| `docs` | Documentation and copy | After any feature lands, when docs feel stale, or before user-facing text ships |
| `ally` | Inclusivity and accessibility | Before any UI ships, when adding new copy, or on a periodic accessibility sweep |

**Recommended workflow for a new feature:**

```
1. /spec-design     → analyst + architect debate → docs/specs/{feature}.md
2. /story-review    → adversarial analyst + tester harden the spec
3. backend + frontend implement (in parallel where possible)
4. /ui-design       → ux + frontend agree on any new screens
5. tester           → write xUnit tests
6. devops           → update CI if migrations added
7. ally             → inclusivity and accessibility review before merge
8. docs             → verify user-facing copy and docs are up to date
```

For small changes (bug fix, minor text change): skip to step 3.

**Periodic maintenance (run after every few stories or on a schedule):**

```
trainer  → post-mortem on recent work; improve agent and skill definitions
docs     → full docs sweep for accuracy and consistency
ally     → full accessibility and language sweep
```

## Branch conventions

- `add-{gameid}-game` — new game module
- `fix-{description}` — bug fix
- `update-{description}` — improvement to existing feature
- `docs-{description}` — documentation only

**Never push directly to `main`.** Always work on a branch and open a PR.
CI must pass before merging.

## Architecture notes

- Each game module owns its own UI — the platform imposes no rendering primitives
- Games implement `IGameModule` + `IGameHandler` (or use `ReducerGameModule<,,>` for simple games)
- Platform stores an opaque JSONB blob per room; games decide their own state shape
- Undo/redo is a game responsibility — games that support it handle an `"Undo"` action
- Identity uses string (GUID as text) user IDs — all FK references to users use TEXT
- See docs/requirements.md for full details and all ADRs
