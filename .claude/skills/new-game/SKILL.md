---
name: new-game
description: Deep guide for designing and implementing a complete new Meepliton game — state design, action modelling, validation, apply logic, and frontend. Load when building a game from scratch.
user-invocable: false
---

## Design process

Before writing any code, answer these questions:

1. **State shape** — what does the board look like? What does each player have? What phase are we in?
2. **Actions** — what can a player do? What payload does each action carry?
3. **Validation** — what makes an action illegal? (wrong turn, illegal move, game over)
4. **Apply** — how does the state change for each action?
5. **Win condition** — when and how does the game end?

## State design

Keep state self-contained — everything needed to render the board lives in it.

```csharp
public record MyState(
    List<PlayerState> Players,
    string CurrentPlayerId,
    MyPhase Phase,
    int Turn,
    string? WinnerId
    // ... game-specific fields
);

public enum MyPhase { Playing, GameOver }
```

## Action modelling

Use discriminated actions with a `Type` string:

```csharp
public record MyAction(string Type, DoThingPayload? DoThing = null);
public record DoThingPayload(int Row, int Col);
```

```typescript
// TypeScript mirror
export type MyAction =
  | { type: 'DoThing'; doThing: { row: number; col: number } }
  | { type: 'Undo' }
```

## Validation checklist

Every `Validate` should check at minimum:
- [ ] Is the game still in progress (not GameOver)?
- [ ] Is it this player's turn?
- [ ] Is the action payload well-formed?
- [ ] Is the specific move legal given the current board/state?

## Apply patterns

```csharp
// Record mutation (immutable)
return state with { CurrentPlayerId = nextPlayerId, Turn = state.Turn + 1 };

// List mutation
var newPlayers = state.Players.ToList();
newPlayers[idx] = player with { Score = player.Score + points };
return state with { Players = newPlayers };

// Turn advancement
var nextIdx = (currentIdx + 1) % state.Players.Count;
var nextPlayerId = state.Players[nextIdx].Id;
```

## Undo support

If `SupportsUndo = true`, handle it in Apply:

```csharp
// Option B: embed history in state
public record MyState(/* ... */, List<MyState> History);

// In Apply:
"Undo" => state.History.Count > 0
    ? state.History[^1] with { History = state.History[..^1] }
    : state, // nothing to undo
```

## Frontend

```tsx
export default function Game({ state, myPlayerId, dispatch }: GameContext<MyState>) {
  const isMyTurn = state.currentPlayerId === myPlayerId

  function send(action: MyAction) { dispatch(action) }

  if (state.phase === 'GameOver') {
    return <div>Game over! Winner: {state.winnerId}</div>
  }

  return (
    <div>
      <p>{isMyTurn ? 'Your turn' : `Waiting for ${currentPlayer?.displayName}…`}</p>
      {/* board */}
    </div>
  )
}
```

## Rendering approaches

| Approach | Good for |
|---|---|
| CSS Grid / Flexbox | Tile boards, card layouts, simple grids |
| SVG | Custom shapes, network graphs, hex grids |
| Canvas | Animations, physics, real-time |

Import any npm package needed — no platform restrictions on game dependencies.
