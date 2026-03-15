# Spec: Public / Private Game State

**Status:** approved
**Branch:** claude/public-private-game-state-s0VPh
**Last updated:** 2026-03-15

## Problem

The platform broadcasts the full `rooms.game_state` JSONB blob to every player in a room after every action and on reconnect. A player can open browser DevTools → Network → WS tab and read other players' private data (e.g. dice values in Liar's Dice). FR-MOD-10 placed visibility responsibility on each game as client-side UI filtering — but the raw data still reached every client.

## Non-Goals (v1)

- Spectator accounts (not yet a platform feature)
- Action log privacy
- Replay / history redaction
- Server-side encryption of state at rest

## Design

### Core mechanism

A projection layer is inserted at the SignalR broadcast boundary in `GameDispatcher`. Games opt in by overriding a single virtual method.

**`IGameModule`** (`src/Meepliton.Contracts/IGameModule.cs`) gains two default interface members:

```csharp
bool HasStateProjection => false;
JsonDocument? ProjectStateForPlayer(JsonDocument fullState, string playerId) => null;
```

Default implementations return `false` / `null` — no behaviour change for existing games.

**`ReducerGameModule<TState,TAction,TOptions>`** (`src/Meepliton.Contracts/ReducerGameModule.cs`) gains:

```csharp
public virtual bool HasStateProjection => false;
protected virtual TState? ProjectForPlayer(TState fullState, string playerId) => default;
```

When a subclass overrides `ProjectForPlayer`, it should also override `HasStateProjection` to return `true`. The base class implements `IGameModule.ProjectStateForPlayer` by deserialising `fullState`, calling `ProjectForPlayer`, and serialising the result back to `JsonDocument`. If `ProjectForPlayer` returns `default`, the method returns `null` (triggering full-state fallback in the dispatcher).

**`GameDispatcher`** (`src/Meepliton.Api/Services/GameDispatcher.cs`) after persisting state:

- If `module.HasStateProjection == false` → `Clients.Group(roomId).SendAsync("StateUpdated", newState)` (unchanged)
- If `module.HasStateProjection == true` → load `RoomPlayers` for the room, iterate, call `module.ProjectStateForPlayer(fullState, playerId)` per player, send via `Clients.User(playerId).SendAsync("StateUpdated", projectedState)`

Exposes a public helper: `ProjectStateForPlayerOrFull(string gameId, JsonDocument fullState, string playerId)` — used by both the fan-out path and the reconnect path to avoid duplicated logic. Returns the projected state if projection is active, or the full state if the module does not opt in (never returns null).

**`GameHub`** (`src/Meepliton.Api/Hubs/GameHub.cs`) `JoinRoom` method: calls `dispatcher.ProjectStateForPlayerOrFull(...)` before sending state to the reconnecting client when projection is active.

### Projection contract

`ProjectStateForPlayer` / `ProjectForPlayer` **must** be:

- **Pure** — no side effects, no I/O, no mutation of shared state
- **Deterministic** — same inputs → same output
- **Cheap** — O(state size), no I/O, no allocations beyond the returned value
- **Non-mutating** — must operate on a deep copy or construct a new record; must not mutate the input `fullState`

### Spectators and unknown player IDs

`GameDispatcher` fans out only to IDs present in `RoomPlayers`. If `ProjectStateForPlayer` is called with a player ID not found in the game state, the game should return a maximally restricted projection (treat as an observer who knows only public information). This is the correct behaviour for eliminated players who remain in `RoomPlayers`.

### Persistence

`rooms.game_state` always stores the full, unfiltered state. No migration required. Projection is a read-time, transport-only operation.

### Frontend

`GameContext<TState>` is unchanged. The `state` field simply contains whatever the server sent — full or projected. Games that previously did client-side visibility filtering should remove that filtering once server-side projection is implemented.

## Liar's Dice implementation

`LiarsDiceModule` overrides `ProjectForPlayer`:

| Phase | Requesting player | All other players |
|---|---|---|
| Bidding | Full `dice` array intact | `dice` replaced with `[]`; `diceCount` preserved |
| Reveal | Full state | Full state |
| Finished | Full state | Full state |

Unknown/spectator player ID: treated as Bidding with maximally restricted projection (all dice hidden).

The existing client-side `isMe` dice filtering in the Liar's Dice frontend component is removed — the server now provides the correct state.

## Acceptance Criteria

| # | Criterion |
|---|---|
| AC-1 | Group broadcast unchanged when `HasStateProjection == false` (all existing games unaffected) |
| AC-2 | Per-player fan-out via `Clients.User` when `HasStateProjection == true` |
| AC-3 | `GameHub.JoinRoom` sends projected state on reconnect |
| AC-4 | Liar's Dice: requesting player sees own dice; all others' `dice` are empty; `diceCount` visible for all — during Bidding |
| AC-5 | Liar's Dice: full state returned for all players during Reveal/Finished |
| AC-6 | Spectator/unknown player ID receives maximally restricted projection |
| AC-7 | Integration test: two `HubConnection` clients via `WebApplicationFactory`, assert Player B's `StateUpdated` payload does not contain Player A's dice during Bidding |
| AC-8 | `ProjectStateForPlayer` / `ProjectForPlayer` documented as pure, deterministic, side-effect-free |
| AC-9 | ADR-010 written; FR-MOD-10 updated in `docs/requirements.md` |
| AC-10 | Liar's Dice spec updated; client-side filtering decision superseded |
| AC-11 | `dispatcher.ProjectStateForPlayerOrFull(gameId, fullState, playerId)` is the single shared projection path — no duplicated logic between fan-out and reconnect |
