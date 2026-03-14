# New Game Skill

Attach this file when designing and building a new Meepliton game module from scratch.

## The two-step process

1. **Scaffold** — run `./scripts/new-game.ps1` to create all boilerplate files
2. **Implement** — fill in the TODO sections with the actual game logic

## Step 1: Scaffold

```bash
./scripts/new-game.ps1 -GameId {gameid} -GameName "{Name}" -MinPlayers 2 -MaxPlayers 4 -Description "One sentence."
```

This creates:
- `src/games/Meepliton.Games.{Pascal}/` — C# project with Module, DbContext, Models
- `apps/frontend/src/games/{gameid}/` — React module with Game.tsx, types.ts
- Registers the game in `registry.ts` and adds it to the solution

## Step 2: Implement

### Backend (C#)

Fill in `{Pascal}Module.cs`:

```csharp
public override {Pascal}State CreateInitialState(IReadOnlyList<PlayerInfo> players, object? options)
{
    // Build starting state from the player list
}

public override string? Validate({Pascal}State state, {Pascal}Action action, string playerId)
{
    // Return null if valid, or an error string if not
}

public override {Pascal}State Apply({Pascal}State state, {Pascal}Action action)
{
    // Return new state — do not mutate state directly (use `with` for records)
}
```

### Frontend (TypeScript/React)

1. Mirror state types in `types.ts` to match C# records exactly (camelCase JSON)
2. Implement `components/Game.tsx` — receives `GameContext<YourState>` with:
   - `state` — current game state
   - `players` — all players with connection status
   - `myPlayerId` — the local player's ID
   - `dispatch(action)` — sends action to SignalR hub

## State design tips

- Keep state self-contained — everything needed to render the board belongs in it
- Use discriminated union actions: `{ type: "PlaceTile", row: 0, col: 0 }` etc.
- For undo support: embed move history in state and handle `{ type: "Undo" }` in Apply

## Supplementary tables (optional)

If your game needs persistent stats or results beyond the opaque state blob:

1. Add tables to `{Pascal}DbContext.cs`
2. Add EF migration: `dotnet ef migrations add Init --project src/games/Meepliton.Games.{Pascal} --context {Pascal}DbContext`
3. Add CI step to `.github/workflows/deploy.yml` (see §15.5 in docs/requirements.md)

## Common patterns

**Turn-based with seat index:**
```csharp
var nextSeat = (currentSeatIndex + 1) % players.Count;
var nextPlayerId = players.First(p => p.SeatIndex == nextSeat).Id;
```

**Game over with winner:**
```csharp
return new GameResult(Serialize(newState), Effects: [new GameOverEffect(winnerId)]);
```

**Rejecting an action:**
```csharp
return new GameResult(ctx.CurrentState, RejectionReason: "Not your turn.");
```
