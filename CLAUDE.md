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
src/games/                   One C# project per game
apps/frontend/src/games/     One React module per game
apps/frontend/src/games/registry.ts   ← only file edited when adding a game
packages/ui/src/             Platform chrome components (lobby, room screens)
packages/contracts/src/      TypeScript interfaces (GameModule, GameContext)
scripts/new-game.ps1         Scaffold script for new games
docs/requirements.md         Full architecture and requirements
```

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

## Skill files (attach to Claude conversations)

| File | When to use |
|---|---|
| `.claude/skills/NEW-GAME.md` | Designing and building a complete new game |
| `.claude/skills/GAME-MODULE.md` | Implementing game rules and frontend |
| `.claude/skills/PLATFORM.md` | Platform architecture, auth, SignalR, database |
| `.claude/skills/THEME.md` | Design tokens, CSS patterns, Skyline aesthetic |
| `.claude/skills/GIT-WORKFLOW.md` | Git and GitHub — especially for non-developer contributors |

## Slash commands

### Game development
| Command | What it does |
|---|---|
| `/scaffold-game` | Guided walkthrough to create a new game from scratch |

### Specialist agents
Invoke these to get focused help from a specific role. Each agent reads the codebase, acts on your request, commits, and pushes.

| Command | Role | When to use |
|---|---|---|
| `/architect` | Software architect | Review code quality, enforce platform/game boundary, propose ADRs |
| `/analyst` | Product analyst | Clarify requirements, write feature specs, update roadmap |
| `/pm` | Project manager | Status reports, plan next tasks, write GitHub issues |
| `/backend` | .NET developer | Implement API endpoints, services, game module logic |
| `/frontend` | React developer | Implement platform UI and game components |
| `/tester` | QA engineer | Write and run xUnit tests for game modules and platform |
| `/devops` | DevOps engineer | CI/CD, Azure infra, migrations, deployment troubleshooting |

**Typical workflow for a new feature:**
1. `/analyst` — write the spec
2. `/architect` — review the approach
3. `/backend` + `/frontend` — implement
4. `/tester` — write tests
5. `/devops` — update CI if migrations or infra changed

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
