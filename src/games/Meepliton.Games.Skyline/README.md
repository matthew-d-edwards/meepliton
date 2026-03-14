# Skyline

The first Meepliton game module. Players take turns placing numbered tiles on a 5×5 grid.
Completing a row or column scores you the sum of all tiles in it. Most points when the board
fills wins.

## Files

| File | Purpose |
|---|---|
| `SkylineModule.cs` | Game logic — implements `ReducerGameModule<SkylineState, SkylineAction, object>` |
| `SkylineDbContext.cs` | EF Core context for supplementary tables (results, player stats) |
| `Models/SkylineModels.cs` | State, action, and DB model records |
| `Migrations/` | EF Core migration history for Skyline tables |

## Adding migrations

```bash
dotnet ef migrations add <Name> \
  --project src/games/Meepliton.Games.Skyline \
  --context SkylineDbContext
```
