# Spec: Hex Escape

**Status:** Agreed
**Date:** 2026-06-15
**Authors:** analyst + architect

---

## Round 3 — Analyst response

All eight OQ decisions are accepted without contest. The architect's reasoning on each point is sound and the precedents from F'That and Skyline are directly applicable. The three "must fix" platform items are elevated to explicit architecture decisions below and must be tracked as blocking prerequisites before the level-selector flow can be tested end-to-end.

No contested points.

---

## Problem

Meepliton's game library is entirely competitive: every title has a winner and losers. There is no co-operative game where players unite against a shared system threat. For groups who want a lower-stakes, collaborative experience — or for solo sessions — the current library offers nothing. Hex Escape fills this gap with a co-op tile-placement puzzle in which players must connect survivors to an exit before the zombie threat overruns them.

---

## Solution

Hex Escape is played on a sparse hexagonal grid. The map consists of up to 49 named hex cells arranged in an axial coordinate system (q, r). Some cells are pre-filled by the level definition; the remainder are empty connection points or impassable terrain. Six directional edges connect each hex to its neighbours. Survivors start at one or more designated source cells; a single exit cell is the win target.

Players place pipe-shaped tiles from their shared hand onto empty grid cells, one action per turn. Each tile has a type (straight, elbow, T-junction, cross, or dead-end) and a rotation expressed in 60-degree increments (0–5). A placed tile creates open passages on its active edges and blocks the rest. Players may also rotate a previously placed tile on their turn, or pass if no useful move is available. Between each full round — once every seated player has acted — the threat counter advances by one. If the threat counter reaches the level's threat threshold, the zombies overrun the remaining survivors and all players lose. Players win the moment the server's BFS connectivity check confirms that a continuous open-edge path exists from every living survivor to the exit.

Before the game starts, the host selects a level from a fixed catalogue defined as static C# data in the game module. The level definition specifies the grid layout, pre-placed tiles, survivor start positions, exit position, the starting tile hand, and the threat threshold. No procedural generation, no database tables, and no migration are needed for v1. A level selector screen is presented to the host in the lobby before the game is started; other players see the host's selection in real time via the existing room SignalR channel.

The game supports 1–6 players. A single-player session is a valid and explicitly supported mode; the round boundary advances after that one player acts.

---

## Acceptance criteria

- [ ] **AC-1 — Level setup:** Given a host selects a valid level and starts the game, when `CreateInitialState` is called, then: the grid is populated with the level's pre-placed tiles; survivors are at their designated start positions; the exit cell is marked; the threat counter is 0; `seatsActedThisRound` is an empty set; `phase` is `Playing`; each player's `seatIndex` is assigned in join order.

- [ ] **AC-2 — Place tile (valid):** Given it is player A's turn and target cell is empty and within the grid, when A dispatches `PlaceTile { coord, tileType, rotation }`, then: the cell is populated with the specified tile; BFS connectivity is recomputed; `seatsActedThisRound` gains player A's seat; if all seats have acted, the threat counter advances by 1 and `seatsActedThisRound` resets to empty; win/lose is evaluated after the threat update (see AC-5 and AC-6); state is broadcast.

- [ ] **AC-3 — Rotate tile (valid):** Given it is player A's turn and the target cell contains a previously placed (non-pre-placed) tile, when A dispatches `RotateTile { coord, rotation }`, then: the tile's rotation is updated; BFS connectivity is recomputed; `seatsActedThisRound` advances exactly as in AC-2; win/lose is evaluated after any threat update.

- [ ] **AC-4 — Pass:** Given it is player A's turn, when A dispatches `Pass`, then: `seatsActedThisRound` gains player A's seat; if all seats have acted, the threat counter advances by 1 and `seatsActedThisRound` resets; win/lose is evaluated after any threat update; the grid is unchanged.

- [ ] **AC-5 — Win condition:** Given any action causes BFS to confirm an open-edge path from every living survivor to the exit, then: `phase` transitions to `GameOver`; `outcome` is `Escaped`; `Handle` returns a `GameResult` whose `Effects` list contains `GameOverEffect(winnerId: null)` (co-op win has no individual winner).

- [ ] **AC-6 — Lose condition (threat threshold):** Given the threat counter advances to the level's threat threshold, then: `phase` transitions to `GameOver`; `outcome` is `Overrun`; `Handle` returns a `GameResult` whose `Effects` list contains `GameOverEffect(winnerId: null)`.

- [ ] **AC-7 — Turn validation:** Given player B dispatches any action when it is player A's turn (i.e. player A's seat is the next seat absent from `seatsActedThisRound`), then the action is rejected with "It is not your turn." and the state is unchanged.

- [ ] **AC-8 — Null options fallback:** Given `CreateInitialState` receives a null or malformed `options` argument (e.g. from an old client or the pre-fix platform path), then the module silently substitutes the default level (level ID "tutorial-01") and returns valid initial state without throwing.

- [ ] **AC-9 — BFS correctness:** Given a grid where tiles create a fully connected open path from survivor to exit, when BFS runs, then `connectedSurvivors` in the broadcast state equals the count of survivors reachable by that path. Given the path is broken, `connectedSurvivors` is less than the total survivor count.

---

## Architecture decisions

### AD-1 (OQ-HE-01): Implement `IGameModule + IGameHandler` directly

`HexEscapeModule` implements `IGameModule` and `IGameHandler` as a single class. It does not extend `ReducerGameModule<,,>`.

Rationale: `ReducerGameModule.Handle()` (ReducerGameModule.cs:31–39) returns `new GameResult(Serialize(newState))` with no effects and the method is not virtual. `GameDispatcher.cs:88–90` keys room-finished and `GameFinished` SignalR events off `GameOverEffect` appearing in `result.Effects`. Hex Escape must emit `GameOverEffect(null)` on both win and loss. The only path to emit effects is to implement `IGameHandler.Handle()` directly, following the F'That precedent (FThatModule.cs). `GameOverEffect.WinnerId` is already nullable (GameResult.cs:16), so a null winner ID on co-op win requires no contract change.

### AD-2 (OQ-HE-02): Sparse `Dictionary<string, HexCell>` grid keyed `"q,r"`

The canonical grid representation is `Dictionary<string, HexCell>` where each key is the string `$"{q},{r}"`. Empty cells are absent from the dictionary; only occupied cells (pre-placed or player-placed) appear as entries. This mirrors the Skyline board pattern (`Dictionary<string, string>` in SkylineModels.cs:8) and keeps state compact.

### AD-3 (OQ-HE-03): Server runs BFS; `connectedSurvivors` is a derived broadcast field

On every action, the server runs BFS over shared open edges across the `Dictionary<string, HexCell>`. With at most 49 cells this is trivially fast. The computed connectivity count is stored as `connectedSurvivors` in the state broadcast to all clients. The frontend renders this value only — it performs no connectivity computation.

### AD-4 (OQ-HE-04): Static readonly C# level data; no `DbContext` or migration

All level definitions are static readonly C# objects inside the game module. No `HexEscapeDbContext`, no EF migrations, and no `__EFMigrationsHistory_hexescape` table. ADR-004 reserves game database tables for leaderboards and statistics only; v1 Hex Escape has neither. There is no embedded-JSON-resource precedent in `src/games/` and we deliberately avoid introducing one — static C# is simpler and immediately navigable by all agents.

### AD-5 (OQ-HE-05): Three action types — `PlaceTile`, `RotateTile`, `Pass`

The action discriminated union has exactly three members: `PlaceTile { coord, tileType, rotation }`, `RotateTile { coord, rotation }`, and `Pass`. `Pass` is required so a player who cannot make a useful move can still advance the round boundary without blocking the threat track.

### AD-6 (OQ-HE-06): `HasStateProjection = false`

All game state (grid contents, threat counter, survivor positions) is public information in Hex Escape. The module does not override the default `HasStateProjection => false` (IGameModule.cs:32). `GameDispatcher` broadcasts the full state to the group (GameDispatcher.cs:119–124).

### AD-7 (OQ-HE-07): `SupportsUndo = false` in v1

Co-op undo requires a consent model (all players must agree to roll back a shared state). That model is out of scope per ADR-009. `SupportsUndo` returns false.

### AD-8 (OQ-HE-08): `MinPlayers = 1`, `MaxPlayers = 6`

Solo play is a first-class mode and the cleanest contract test for the round-boundary logic (a single seat immediately triggers threat advance). `MinPlayers` is a free int on `IGameModule` (IGameModule.cs:14); setting it to 1 requires no contract change.

### AD-9 (PLATFORM FIX): `POST /rooms` must populate `room.GameOptions` from `req.Options`

`RoomEndpoints.cs:143–158` currently constructs the `Room` entity without ever reading `req.Options` (declared as `object?` in `CreateRoomRequest`, line 347). As a result `room.GameOptions` is always null when `CreateInitialState` is called at line 223. `Room.GameOptions` is already a `JsonDocument?` column with no schema change required (Room.cs:14).

Fix: serialize `req.Options` to `JsonDocument` and assign it to `room.GameOptions` inside the `MapPost("/rooms")` handler before calling `db.SaveChangesAsync()`. The platform must transport the opaque options blob unchanged — it must not parse or understand level IDs. Level schema stays entirely inside the game module.

This is a shared platform change. It must land on the session branch before level-selector behaviour can be tested. Architect sign-off is required before merging.

### AD-10 (PLATFORM FIX): `CreateInitialState` must defend against null/malformed options

Even after AD-9 is deployed, old clients, integration tests, or manually created rooms may omit options. `HexEscapeModule.CreateInitialState` must treat a null or malformed `JsonDocument?` options argument as a signal to fall back to the default level (`"tutorial-01"`) rather than throwing. A thrown exception at this call site (RoomEndpoints.cs:223) returns a 500 and leaves the room in an unstarted state with no error message visible to the host. The fallback is silent — no error surface to the caller.

### AD-11 (ROUND BOUNDARY): Explicit `seatsActedThisRound` state field

The state record carries `seatsActedThisRound: HashSet<int>` (or equivalent serialisable collection of seat indices). `Handle` appends the acting seat on every accepted action. When `seatsActedThisRound.Count == players.Count`, the threat counter increments and `seatsActedThisRound` resets to empty in the same `Handle` call — atomically, in one state transition. This makes the round-boundary logic deterministic and testable in isolation without a running hub.

---

## Out of scope

- Procedural level generation
- Moving zombie tokens on the board (threat is a counter only in v1)
- Breach-cell loss trigger (individual cells being overrun; only the threshold matters in v1)
- Co-op undo or rollback with player consent
- Leaderboard, statistics, or match-history tables (`HexEscapeDbContext` deferred)
- Spectator mode or late-join
- Embedded JSON resources in the game project (no precedent; use static C#)
- Any changes to `IGameModule`, `IGameHandler`, `GameContext`, `GameResult`, or the TypeScript `GameModule`/`GameContext` interfaces

---

## Implementation hints

**Platform prerequisite — must land first:**
- `{agent: backend}` In `RoomEndpoints.cs` `MapPost("/rooms")` handler (lines 143–158): deserialize `req.Options` into a `JsonDocument?` and assign to `room.GameOptions` before `db.SaveChangesAsync()`. `CreateRoomRequest.Options` is already `object?`; replace its type with `JsonDocument?` or serialize via `JsonSerializer`. No migration needed. `{agent: architect}` sign-off required before this merges.

**Backend:**
- `{agent: backend}` Implement `HexEscapeModule` as a single class implementing both `IGameModule` and `IGameHandler`, following FThatModule.cs as the template.
- Static level data as `static readonly HexEscapeLevel[]` inside the module or a companion `HexEscapeLevels.cs` file.
- BFS runs on every accepted action over the current `Dictionary<string, HexCell>` grid; result written to `connectedSurvivors` in state before serialisation.
- `seatsActedThisRound` drives threat advance; win/loss checked after each threat update.
- Both `HexEscapePhase` and `HexEscapeActionType` enums must carry `[JsonConverter(typeof(JsonStringEnumConverter))]`.
- No `HexEscapeDbContext`; no EF migrations.
- `CreateInitialState` falls back to level `"tutorial-01"` when options are null or unparseable (AD-10).

**Frontend:**
- `{agent: frontend, ux}` Hex board rendered from `state.grid` (sparse dictionary); empty cells rendered as blank hexagons.
- Tile rotation UI: clicking a placed tile offers a rotate action; clicking an empty cell offers a place-tile picker.
- Level selector pre-game screen shown to the host in the lobby; dispatches the selected level ID as part of the `CreateRoomRequest.Options` payload; other players see the selection via the room SignalR channel.
- Shared result screen for both outcomes: "Escaped!" (win) and "Overrun!" (loss) — both trigger off `phase === 'GameOver'` with `outcome` discriminating the message.
- `registry.ts` one-line add: `import hexescape from './hexescape'` and entry in the registry object.

**Tests:**
- `{agent: tester}` xUnit tests covering: BFS connectivity (connected path, broken path, partial path); threat counter reaching threshold triggers `Overrun` + `GameOverEffect(null)`; BFS win triggers `Escaped` + `GameOverEffect(null)`; `seatsActedThisRound` resets after full round; `PlaceTile` on occupied cell rejected; `RotateTile` on empty cell rejected; `Pass` advances round boundary; wrong-turn rejection; null-options fallback to default level.

**CI / DevOps:**
- `{agent: devops}` No migration and no new DbContext — no CI action required unless that changes in a future story.
