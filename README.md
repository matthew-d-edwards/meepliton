# Meepliton

Browser-based multiplayer board game platform for a small group of friends. Live at meepliton.com.

## Running locally

Prerequisites: .NET 10 SDK, Node 20, Docker Desktop

```bash
dotnet run --project src/Meepliton.AppHost
```

- Frontend: http://localhost:5173
- API: http://localhost:5000
- Aspire dashboard: http://localhost:15888

## Adding a new game

```bash
git checkout -b add-{gameid}-game
./scripts/new-game.ps1 -GameId {gameid} -GameName "{Name}"
# Implement TODOs, then:
git add src/games/Meepliton.Games.{Name}/ apps/frontend/src/games/{gameid}/
git commit -m "Add {Name} game module"
gh pr create --title "Add {Name}" --base main --web
```

## Claude AI help

Open Claude Code in this folder (`claude`) and use:

| Command | Purpose |
|---|---|
| `/scaffold-game` | Guided walkthrough to create a new game |

Attach skill files from `.claude/skills/` for deeper context:

| Skill file | When to use |
|---|---|
| `NEW-GAME.md` | Designing and building a new game |
| `GAME-MODULE.md` | Implementing game rules and frontend |
| `PLATFORM.md` | Platform architecture, auth, SignalR, database |
| `THEME.md` | Design tokens, CSS patterns |
| `GIT-WORKFLOW.md` | Git and GitHub help |

## Repository layout

```
src/                        .NET solution
  Meepliton.AppHost/        Local dev orchestration (.NET Aspire)
  Meepliton.Api/            ASP.NET Core API + SignalR hub
  Meepliton.Contracts/      IGameModule, IGameHandler, shared types
  Meepliton.Tests/          xUnit tests
  games/
    Meepliton.Games.Skyline/ First game module

apps/frontend/              React 18 + TypeScript + Vite
  src/platform/             Auth, lobby, room chrome
  src/games/
    registry.ts             ← only file edited when adding a game
    skyline/                Skyline game UI

packages/
  contracts/                @meepliton/contracts (TypeScript types)
  ui/                       @meepliton/ui (platform chrome components)

scripts/
  new-game.ps1              Game scaffolder

docs/
  requirements.md           Full architecture and requirements

.claude/
  settings.json             Shared contributor permissions + hooks
  skills/                   Skill files — attach to Claude conversations
  commands/                 Slash commands (/scaffold-game)
```

## Architecture

See [docs/requirements.md](docs/requirements.md) for full architecture, ADRs, and roadmap.
