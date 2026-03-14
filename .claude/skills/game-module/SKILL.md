---
name: game-module
description: Meepliton game module patterns — IGameModule, IGameHandler, ReducerGameModule, frontend GameContext. Load when implementing or reviewing game logic.
user-invocable: false
---

## Backend contract (`Meepliton.Contracts`)

Games implement `IGameModule` (metadata + initial state) and `IGameHandler` (action processing).
Most simple games can extend `ReducerGameModule<TState, TAction, TOptions>` instead.

### ReducerGameModule pattern

```csharp
public class MyModule : ReducerGameModule<MyState, MyAction, object>
{
    public override string GameId      => "mygame";
    public override string Name        => "My Game";
    public override string Description => "...";
    public override int    MinPlayers  => 2;
    public override int    MaxPlayers  => 4;

    public override MyState CreateInitialState(IReadOnlyList<PlayerInfo> players, object? options)
    {
        // build full starting state from player list
    }

    public override string? Validate(MyState state, MyAction action, string playerId)
    {
        if (state.CurrentPlayerId != playerId) return "Not your turn.";
        return null; // null = valid
    }

    public override MyState Apply(MyState state, MyAction action) =>
        action.Type switch
        {
            "DoThing" => state with { /* mutation via record with-expression */ },
            "Undo"    => HandleUndo(state),
            _         => state,
        };
}
```

### Side effects

```csharp
// Game over
return new GameResult(Serialize(newState), Effects: [new GameOverEffect(winnerId)]);

// Reject action
return new GameResult(ctx.CurrentState, RejectionReason: "Not your turn.");
```

## Frontend contract (`@meepliton/contracts`)

```tsx
export default function Game({ state, players, myPlayerId, dispatch }: GameContext<MyState>) {
  const isMyTurn = state.currentPlayerId === myPlayerId
  return <div>{/* board */}</div>
}
```

## TypeScript ↔ C# type mirroring

C# records serialize to **camelCase** JSON. Mirror exactly in `types.ts`:

```csharp
// C#
public record MyState(List<PlayerState> Players, string CurrentPlayerId, GamePhase Phase);
public enum GamePhase { Playing, GameOver }
```

```typescript
// TypeScript — types.ts
export interface MyState {
  players: PlayerState[]
  currentPlayerId: string
  phase: 'Playing' | 'GameOver'   // enum → string union
}
```

## Game project structure

```
src/games/Meepliton.Games.{Pascal}/
├── Meepliton.Games.{Pascal}.csproj  — refs Meepliton.Contracts only (NOT Meepliton.Api)
├── {Pascal}Module.cs                — game logic
├── {Pascal}DbContext.cs             — optional supplementary tables
├── Migrations/                      — EF migrations (own history table)
└── Models/{Pascal}Models.cs         — state + action records

apps/frontend/src/games/{gameId}/
├── index.tsx                        — module entry point
├── types.ts                         — mirrors C# records
├── styles.module.css                — CSS Modules, game-scoped
└── components/Game.tsx              — main game component
```

## Key constraints

- Game projects must **not** reference `Meepliton.Api` — only `Meepliton.Contracts`
- No DB-level FK to platform tables — app-enforced only
- Game migrations use `MigrationsHistoryTable("__EFMigrationsHistory_{gameId}")`
- `dispatch()` is the only way the frontend sends actions — no direct fetch/SignalR calls
