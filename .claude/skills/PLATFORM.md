# Meepliton Platform Skill

Attach this file when debugging or extending the platform core (auth, SignalR, database, deployment).

## Stack quick-reference

| Layer | Technology |
|---|---|
| Backend | .NET 9 / ASP.NET Core, SignalR (in-process) |
| Database | PostgreSQL 16 via EF Core 9 + Npgsql |
| Auth | ASP.NET Core Identity — email/password + Google OAuth |
| Real-time | SignalR in-process (no Azure SignalR Service) |
| Local dev | .NET Aspire (`dotnet run --project src/Meepliton.AppHost`) |

## Key files

| File | Purpose |
|---|---|
| `src/Meepliton.Api/Program.cs` | DI wiring, middleware, endpoint registration |
| `src/Meepliton.Api/Identity/ApplicationUser.cs` | Extended Identity user |
| `src/Meepliton.Api/Data/PlatformDbContext.cs` | Platform EF Core context |
| `src/Meepliton.Api/Hubs/GameHub.cs` | SignalR hub |
| `src/Meepliton.Api/Services/GameDispatcher.cs` | Action validation → state persistence → broadcast |
| `src/Meepliton.Api/Services/MigrationRunner.cs` | Applies platform + all game migrations on startup |
| `src/Meepliton.Contracts/` | Shared interfaces between platform and games |

## Auth flow

- JWT stored in HttpOnly, SameSite=Strict cookie — frontend never touches the token
- SignalR authenticates via `?access_token=` query string (mapped in `JwtBearerEvents.OnMessageReceived`)
- Google OAuth: `GET /api/auth/google` → consent → `GET /api/auth/google/callback` → sets cookie
- `RequireConfirmedEmail` applies only to email/password accounts (Google already verified)

## Database patterns

- All Identity tables renamed to snake_case in `OnModelCreating`
- `rooms.game_state` is a JSONB blob — the platform knows nothing about its shape
- No database-level FK from game tables to platform tables — app-enforced only
- Game `DbContext`s use `MigrationsHistoryTable("__EFMigrationsHistory_{gameId}")` to isolate history

## Adding a platform migration

```bash
dotnet ef migrations add <Name> \
  --project src/Meepliton.Api \
  --context PlatformDbContext
```

## Running locally

```bash
dotnet run --project src/Meepliton.AppHost
```

- Frontend: http://localhost:5173
- API: http://localhost:5000
- Aspire dashboard: http://localhost:15888

## Deployment

- API: Azure Container Apps (Consumption, minReplicas=0), custom domain api.meepliton.com
- Frontend: Azure Static Web Apps (Free), custom domain meepliton.com
- Database: Azure PostgreSQL Flexible Server (B1ms)
- CI/CD: `.github/workflows/deploy.yml` — build → test → migrate → push to ACR → deploy

See `docs/requirements.md` for full architecture details.
