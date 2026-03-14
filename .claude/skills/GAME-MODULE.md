# Game Module Implementation Skill

Attach this file when implementing game rules and frontend for an existing scaffold.

## Backend contract

Your module extends `ReducerGameModule<TState, TAction, TOptions>` (or implements `IGameModule` + `IGameHandler` directly for complex games).

### CreateInitialState

Called once when the host clicks Start. Returns the full starting state.

```csharp
public override MyState CreateInitialState(IReadOnlyList<PlayerInfo> players, object? options)
{
    return new MyState(
        Players: players.Select((p, i) => new MyPlayerState(p.Id, p.DisplayName, ...)).ToList(),
        CurrentPlayerId: players[0].Id,
        Phase: GamePhase.Playing,
        // ...
    );
}
```

### Validate

Returns `null` if the action is legal, or an error string if not.
Keep validation fast — no async, no side effects.

```csharp
public override string? Validate(MyState state, MyAction action, string playerId)
{
    if (state.CurrentPlayerId != playerId) return "Not your turn.";
    if (state.Phase == GamePhase.GameOver) return "Game is over.";
    // domain-specific checks...
    return null;
}
```

### Apply

Returns new state. Use `with` for record mutation. Never modify state in-place.

```csharp
public override MyState Apply(MyState state, MyAction action) =>
    action.Type switch
    {
        "DoThing" => HandleDoThing(state, action),
        "Undo"    => HandleUndo(state),
        _         => state,
    };
```

## Frontend contract

Your `Game.tsx` receives `GameContext<TState>`:

```tsx
export default function Game({ state, players, myPlayerId, dispatch }: GameContext<MyState>) {
  const isMyTurn = state.currentPlayerId === myPlayerId

  function handleClick(/* ... */) {
    dispatch({ type: 'DoThing', /* payload */ })
  }

  return <div>/* your board */</div>
}
```

## State type mirroring

C# records serialize to camelCase JSON. Mirror them in TypeScript:

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
  phase: 'Playing' | 'GameOver'
}
```

## Rendering approaches

The platform imposes no rendering primitives. Common approaches:

| Approach | Good for |
|---|---|
| HTML + CSS Grid | Tile-based boards, card layouts |
| SVG | Custom shapes, network graphs, map-based games |
| Canvas | Animations, real-time, physics |
| Three.js | 3D games |

Import any npm package you need directly in the game module — no platform restrictions.

## Testing game logic

Write xUnit tests in `src/Meepliton.Tests/`. The module is a pure function — easy to test:

```csharp
[Fact]
public void PlacingTileAdvancesTurn()
{
    var module = new MyModule();
    var players = new[] { new PlayerInfo("p1", "Alice", null, 0), new PlayerInfo("p2", "Bob", null, 1) };
    var initial = module.CreateInitialState(players, null);
    var action  = new MyAction("PlaceTile", /* ... */);
    var result  = module.Apply(initial, action);
    Assert.Equal("p2", result.CurrentPlayerId);
}
```
