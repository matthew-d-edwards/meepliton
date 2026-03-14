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

## Skills

Skills load automatically when relevant. You can also invoke them directly with `/skill-name`.

| Skill | Loads when… |
|---|---|
| `platform` | Working on the API, database, auth, or SignalR |
| `theme` | Building any UI screen or component |
| `game-module` | Implementing or reviewing game logic |
| `new-game` | Designing a game from scratch |
| `git-workflow` | Git or GitHub tasks |

## Slash commands

| Command | What it does |
|---|---|
| `/scaffold-game [game-id]` | Guided walkthrough to scaffold a new game module |

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

**Typical workflow for a new feature:**
1. `analyst` — clarify and spec
2. `architect` — review the approach
3. `backend` + `frontend` — implement in parallel
4. `tester` — write tests
5. `devops` — update CI if migrations added

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
