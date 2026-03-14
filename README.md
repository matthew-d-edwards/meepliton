# Meepliton

Browser-based multiplayer board game platform for a small group of friends. Live at meepliton.com.

## Running locally

Prerequisites: .NET 9 SDK, Node 20, Docker Desktop

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

Attach skill files from `.claude/skills/` to Claude conversations for deeper context:
`NEW-GAME.md`, `GAME-MODULE.md`, `PLATFORM.md`, `THEME.md`, `GIT-WORKFLOW.md`

## Architecture

See [docs/requirements.md](docs/requirements.md) for full architecture, ADRs, and roadmap.
