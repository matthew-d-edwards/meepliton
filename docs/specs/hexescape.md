# Spec: Hex Escape

**Status:** Implemented — pending CI verification
**Date:** 2026-06-15
**Authors:** analyst + architect
**Implementation note:** Level selection is wired via the generic `IGameModule.SetupOptions` mechanism (ADR-012). `HexEscapeModule.SetupOptions` declares a single `levelId` dropdown populated from `HexEscapeLevels.Ordered`. The platform renders it as a labelled dropdown in the lobby and transports the host's selection as `{ "levelId": "<id>" }` to `CreateInitialState` — satisfying the AD-9 prerequisite, which is now resolved. See `src/Meepliton.Contracts/GameSetupOption.cs`.

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

Players place pipe-shaped tiles from their shared hand onto empty grid cells, one action per turn. Each tile has a type (straight, elbow, tee, cross, or dead-end — five types in v1) and a rotation expressed in 60-degree increments (0–5). A placed tile creates open passages on its active edges and blocks the rest. Players may also rotate a previously placed (player-placed) tile on their turn, or pass if no useful move is available. Between each full round — once every seated player has acted — the threat counter advances by one. If the threat counter reaches or exceeds the level's threat threshold, the zombies overrun and all players lose. Players win the moment the server's BFS connectivity check confirms that a continuous open-edge path exists from every survivor to the exit — checked BEFORE any threat increment within the same Handle call.

Before the game starts, the host selects a level from a fixed catalogue defined as static C# data in the game module. The level definition specifies the grid layout, pre-placed tiles, survivor start positions, exit position, the starting tile hand counts per type, and the threat threshold. No procedural generation, no database tables, and no migration are needed for v1. A level selector screen is presented to the host in the lobby before the game is started; other players see the host's selection in real time via the existing room SignalR channel.

The game supports 1–6 players. A single-player session is a valid and explicitly supported mode; the round boundary advances after that one player acts.

---

## Hex geometry and tile model

This section is the single source of truth for both backend BFS and frontend rendering. Backend and frontend must not define a second or divergent coordinate convention.

### Axial coordinate system

Cells are addressed by integer pair **(q, r)**. This is the standard redblobgames axial hex grid. Dictionary keys use the string form `"q,r"`.

### Six directions

| Index | Axial offset (Δq, Δr) |
|-------|----------------------|
| 0     | (+1,  0)             |
| 1     | (+1, −1)             |
| 2     | ( 0, −1)             |
| 3     | (−1,  0)             |
| 4     | (−1, +1)             |
| 5     | ( 0, +1)             |

Opposite of direction d is **(d + 3) mod 6**.

### Connection rule

A tile at cell C with open edge in direction d connects to its neighbour N (at C + offset[d]) if and only if:
1. N exists in the level grid (is a valid cell), AND
2. N's tile has an open edge in direction **(d + 3) mod 6**.

An open edge whose neighbour cell does not exist in the level grid is a dead end — it is not traversable and is not an error.

### Rotation

Rotation k ∈ {0..5} maps each base edge index e to **(e + k) mod 6**. A tile's open edges after rotation are `{ (e + k) mod 6 | e in base_edges }`.

### Tile types (v1 — five types)

| Tile type  | Base edges (before rotation) |
|------------|------------------------------|
| `straight` | {0, 3}                       |
| `elbow`    | {0, 1}                       |
| `tee`      | {0, 1, 2}                    |
| `cross`    | {0, 1, 2, 3}                 |
| `deadend`  | {0}                          |

Y-junction is excluded from v1. These five types and their base-edge sets are the complete tile vocabulary.

### BFS direction (win check)

BFS runs **from the exit cell outward** over open shared edges (both sides of the edge must satisfy the connection rule above). `connectedSurvivors` = the number of survivor start cells reachable from the exit via this BFS. Win condition: `connectedSurvivors == totalSurvivors`.

### Level field contract

A `HexEscapeLevel` carries:

| Field                  | Type                     | Description                                                  |
|------------------------|--------------------------|--------------------------------------------------------------|
| `id`                   | string                   | Unique level identifier, e.g. `"tutorial-01"`                |
| `cells`                | list of (q, r)           | Every valid cell in the grid (defines the board boundary)    |
| `walls`                | list of (q, r)           | Cells that exist but are always impassable (no tile allowed) |
| `prePlacedTiles`       | list of {coord, type, rotation} | Fixed tiles; cannot be rotated or replaced by players  |
| `survivorStartCells`   | list of (q, r)           | Where survivors begin (must be ≥ 1 per valid level)          |
| `exitCell`             | (q, r)                   | The win target                                               |
| `tileHandCounts`       | map of tileType → int    | Starting count of each tile type in the shared hand          |
| `threatThreshold`      | int                      | Threat counter value at or above which the game is lost      |

The `"tutorial-01"` level must be authored first — it is the fallback target. Its absence causes a `KeyNotFoundException` that defeats AD-10.

### Options wire schema

```json
{ "levelId": "<string>" }
```

---

## Acceptance criteria

- [ ] **AC-1 — Level setup:** Given a host selects a valid level and starts the game, when `CreateInitialState` is called, then: the grid is populated with the level's pre-placed tiles; every survivor is at its designated start cell; the exit cell is marked; the threat counter is 0; `seatsActedThisRound` is an empty set; `phase` is `Playing`; each player's `seatIndex` is assigned in join order. `CreateInitialState` does NOT evaluate the win condition — the first win check occurs only after the first accepted action.

- [ ] **AC-2 — Place tile (valid):** Given a player whose seat index is NOT in `seatsActedThisRound` dispatches `PlaceTile { coord, tileType, rotation }`, and the target coord exists in the level grid, is not occupied (pre-placed or player-placed), and the hand contains at least one tile of that type, and rotation is in 0–5, then: the cell is populated with the specified tile; the hand count for that type decrements by 1; BFS connectivity is recomputed; `seatsActedThisRound` gains the player's seat index. **Win check runs first:** if `connectedSurvivors == totalSurvivors`, `phase` → `GameOver`, `outcome` → `Escaped`, emit `GameOverEffect(winnerId: null)`, and the threat increment is NOT applied. If no win, and all seat indices are now in `seatsActedThisRound`, the threat counter increments by 1 and `seatsActedThisRound` resets to empty; if threat counter >= threatThreshold, `phase` → `GameOver`, `outcome` → `Overrun`, emit `GameOverEffect(winnerId: null)`. State is broadcast.

- [ ] **AC-3 — Rotate tile (valid and rejected paths):** Given a player whose seat index is NOT in `seatsActedThisRound` dispatches `RotateTile { coord, rotation }`, and rotation is in 0–5, then:
  - If the target cell contains a player-placed tile: the tile's rotation is updated to the new value (even if it equals the current rotation — same-rotation rotate is allowed and consumes the turn); BFS is recomputed; `seatsActedThisRound` and win/threat logic proceed identically to AC-2 (win check before threat increment).
  - If the target cell contains a pre-placed (fixed) tile: rejected with `"Cannot rotate a fixed tile."` State unchanged.
  - If the target cell is empty: rejected with `"No tile to rotate."` State unchanged.
  - If rotation is outside 0–5: rejected with `"Invalid rotation."` State unchanged.

- [ ] **AC-4 — Pass:** Given a player whose seat index is NOT in `seatsActedThisRound` dispatches `Pass`, then: `seatsActedThisRound` gains the player's seat index; the grid is unchanged. Win check runs first (BFS result before any threat increment): if win condition met, `phase` → `GameOver`, `outcome` → `Escaped`, emit `GameOverEffect(null)`, no threat increment. If no win, and all seats have now acted, threat counter increments and `seatsActedThisRound` resets; if threat >= threatThreshold, `phase` → `GameOver`, `outcome` → `Overrun`, emit `GameOverEffect(null)`.

- [ ] **AC-5 — Win condition (priority and ordering):** Within a single `Handle` call, the win check runs BEFORE the threat increment. If placing or rotating a tile (or passing, on the rare case BFS is already satisfied) causes `connectedSurvivors == totalSurvivors`, the module emits `GameOverEffect(winnerId: null)` with `outcome = Escaped` and does NOT apply the threat increment or advance the round boundary. This ordering is invariant and applies to all three action types. The value of `connectedSurvivors` in the broadcast state is frozen at the value computed in the final winning action. The result screen uses `outcome` (not `connectedSurvivors`) to branch between "Escaped!" and "Overrun!".

- [ ] **AC-6 — Lose condition (threat threshold):** Given the threat counter increments (because the win check did NOT trigger first) and the new value is >= the level's `threatThreshold`, then: `phase` → `GameOver`; `outcome` is `Overrun`; `Handle` returns a `GameResult` with `Effects` containing `GameOverEffect(winnerId: null)`.

- [ ] **AC-7 — Turn validation (free-order model):** Given any player dispatches any action when that player's own seat index is ALREADY present in `seatsActedThisRound` for the current round, the action is rejected with the exact string `"It is not your turn."` and state is unchanged. Seat indices may be non-contiguous (after pre-game leave); the logic uses set membership only, never index arithmetic or sequential ordering.

- [ ] **AC-8 — Null options fallback:** Given `CreateInitialState` receives a null or malformed `options` argument (e.g. from an old client or the pre-fix platform path), then the module silently substitutes the default level (`"tutorial-01"`) and returns valid initial state without throwing. The module emits a server-side WARNING log line: `"HexEscape: options missing/unknown level '{id}', falling back to tutorial-01"` (see AD-10).

- [ ] **AC-9 — BFS correctness (observable):** `connectedSurvivors` in the broadcast state equals the number of survivor start cells from which a continuous open-edge path to the exit exists, using the canonical axial offsets defined in the "Hex geometry and tile model" section; 0 when none; recomputed on every accepted action.

- [ ] **AC-10 — Zero-survivor level rejected:** Given `CreateInitialState` is called with a level whose `survivorStartCells` list is empty, then it throws `ArgumentException`. A catalogue-validation unit test must assert that no authored level has 0 survivors.

- [ ] **AC-11 — Hand exhaustion (PlaceTile type with 0 remaining):** Given a player dispatches `PlaceTile` for a tile type whose hand count is currently 0, the action is rejected with `"No tiles of that type remaining."` State is unchanged. A hand fully exhausted with no path to the exit is an accepted design outcome — players continue to Pass each round until the threat counter reaches `threatThreshold` and the game ends as Overrun.

- [ ] **AC-12 — Invalid rotation rejected:** Given `PlaceTile` or `RotateTile` is dispatched with a `rotation` value outside 0–5, the action is rejected with `"Invalid rotation."` State is unchanged.

- [ ] **AC-13 — Out-of-bounds coord rejected:** Given `PlaceTile` is dispatched with a coord that is not present in the level's cell list, the action is rejected with `"Cell is not on the board."` State is unchanged.

- [ ] **AC-14 — Occupied cell rejected:** Given `PlaceTile` is dispatched to a coord that is already occupied (pre-placed OR player-placed), the action is rejected with `"Cell is already occupied."` State is unchanged. No overwrite is permitted.

- [ ] **AC-15 — Pre-won level disallowed:** No authored level in the catalogue may start in a state where BFS immediately returns `connectedSurvivors == totalSurvivors` (all survivors already connected to the exit before any action is taken). A catalogue-validation unit test must assert this for every authored level.

- [ ] **AC-16 — Disconnected seat stalls round (known limitation — skip test in v1):** The round only advances when all currently-seated players have acted. A disconnected seat that never dispatches an action will stall the round indefinitely. This is a v1 known limitation; the corresponding test is authored as:
  ```csharp
  [Fact(Skip="v1 known limitation: disconnected seat stalls round; see follow-up")]
  public void DisconnectedSeat_DoesNotAdvanceRound() { ... }
  ```

---

## Architecture decisions

### AD-1 (OQ-HE-01): Implement `IGameModule + IGameHandler` directly

`HexEscapeModule` implements `IGameModule` and `IGameHandler` as a single class. It does not extend `ReducerGameModule<,,>`.

Rationale: `ReducerGameModule.Handle()` (ReducerGameModule.cs:31–39) returns `new GameResult(Serialize(newState))` with no effects and the method is not virtual. `GameDispatcher.cs:88–90` keys room-finished and `GameFinished` SignalR events off `GameOverEffect` appearing in `result.Effects`. Hex Escape must emit `GameOverEffect(null)` on both win and loss. The only path to emit effects is to implement `IGameHandler.Handle()` directly, following the F'That precedent (FThatModule.cs). `GameOverEffect.WinnerId` is already nullable (GameResult.cs:16), so a null winner ID on co-op win requires no contract change.

### AD-2 (OQ-HE-02): Sparse `Dictionary<string, HexCell>` grid keyed `"q,r"`

The canonical grid representation is `Dictionary<string, HexCell>` where each key is the string `$"{q},{r}"`. Empty cells are absent from the dictionary; only occupied cells (pre-placed or player-placed) appear as entries. This mirrors the Skyline board pattern (`Dictionary<string, string>` in SkylineModels.cs:8) and keeps state compact.

### AD-3 (OQ-HE-03): Server runs BFS; `connectedSurvivors` is a derived broadcast field

On every action, the server runs BFS from the exit cell outward over shared open edges across the `Dictionary<string, HexCell>`. With at most 49 cells this is trivially fast. The computed connectivity count is stored as `connectedSurvivors` in the state broadcast to all clients. The frontend renders this value only — it performs no connectivity computation.

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

Backward safety: No existing game (FThat, Skyline, LiarsDice) passes non-null options today — all pass null. Populating `room.GameOptions` from `req.Options` is therefore backward-safe; when `req.Options` is null the column remains null and existing games are unaffected. An integration test must confirm that existing games still start correctly with null options after this fix.

This is a shared platform change. It must land on the session branch before level-selector behaviour can be tested end-to-end. Architect sign-off is required before merging.

### AD-10 (PLATFORM FIX): `CreateInitialState` must defend against null/malformed options

Even after AD-9 is deployed, old clients, integration tests, or manually created rooms may omit options. `HexEscapeModule.CreateInitialState` must treat a null or malformed `JsonDocument?` options argument as a signal to fall back to the default level (`"tutorial-01"`) rather than throwing. A thrown exception at this call site (RoomEndpoints.cs:223) returns a 500 and leaves the room in an unstarted state with no error message visible to the host. The fallback emits a server-side WARNING log line via `ILogger`:

```
HexEscape: options missing/unknown level '{id}', falling back to tutorial-01
```

This warning fires for both the null case and the unknown-level-id case. It is observable in the Aspire dashboard and Azure Monitor logs.

### AD-11 (ROUND BOUNDARY): Explicit `seatsActedThisRound` state field

The state record carries `seatsActedThisRound` as a **set of seat indices** (e.g., `int[]` or `List<int>` with a distinct guard — NOT `HashSet<int>`, which does not round-trip cleanly through JSON). Serializes to a JSON array. `Handle` appends the acting seat on every accepted action, with a distinct check before appending. When all currently-assigned seat indices are present in `seatsActedThisRound`, the threat counter increments and `seatsActedThisRound` resets to empty in the same `Handle` call — atomically, in one state transition.

Seat indices may be non-contiguous after a pre-game leave (e.g., seats 0, 2, 4 with no seats 1 or 3). All logic must use set membership (`seatsActedThisRound.Contains(seatIndex)`) and never assume seats are 0..N-1 or use index arithmetic.

### AD-12 (FRONTEND): Frontend must handle `GameFinished` with `winnerId = null`

`GameDispatcher` emits a `GameFinished` SignalR event that includes `winnerId`. For Hex Escape, `winnerId` is always null (co-op — no individual winner). The frontend result screen must use `outcome` (`Escaped` vs `Overrun`) as the sole discriminator for result messaging. It must never render "null won" or attempt to look up a player by a null ID.

A frontend task is required to verify that existing lobby chrome (the platform-level post-game screen) tolerates a null `winnerId` without throwing or rendering a broken state. This applies to all screens that branch on `winnerId`.

---

## Known limitations (v1)

### Disconnected seat deadlock

The round only advances when every seated player has dispatched an action in the current round. A player who disconnects mid-game and never dispatches will stall the round indefinitely. There is no host-initiated "skip seat" or timeout mechanism in v1. Follow-up story: host-initiated skip-seat / per-seat timeout.

### Hand exhaustion loss path

If the shared tile hand is fully exhausted before a winning path is established, players have no moves other than `Pass`. They will pass each round, the threat counter will advance each round, and eventually the game ends as `Overrun`. This is an intended design outcome, not an error. The system correctly processes it without special handling.

---

## Out of scope

- Procedural level generation
- Y-junction tile type (deferred to v2)
- Moving zombie tokens on the board (threat is a counter only in v1)
- Breach-cell loss trigger (individual cells being overrun; only the threshold matters in v1)
- Co-op undo or rollback with player consent
- Host-initiated skip-seat or per-seat disconnect timeout (follow-up story)
- Leaderboard, statistics, or match-history tables (`HexEscapeDbContext` deferred)
- Spectator mode or late-join
- Embedded JSON resources in the game project (no precedent; use static C#)
- Any changes to `IGameModule`, `IGameHandler`, `GameContext`, `GameResult`, or the TypeScript `GameModule`/`GameContext` interfaces

---

## Implementation hints

**Platform prerequisite — must land first:**
- `{agent: backend}` In `RoomEndpoints.cs` `MapPost("/rooms")` handler (lines 143–158): deserialize `req.Options` into a `JsonDocument?` and assign to `room.GameOptions` before `db.SaveChangesAsync()`. `CreateRoomRequest.Options` is already `object?`; replace its type with `JsonDocument?` or serialize via `JsonSerializer`. No migration needed. `{agent: architect}` sign-off required before this merges.
- `{agent: tester}` Integration test: existing games (FThat, Skyline, LiarsDice) still start correctly with null options after the AD-9 fix.

**Backend:**
- `{agent: backend}` Implement `HexEscapeModule` as a single class implementing both `IGameModule` and `IGameHandler`, following FThatModule.cs as the template.
- Author `"tutorial-01"` level first — it is the fallback target; all other levels may follow.
- Static level data as `static readonly HexEscapeLevel[]` inside the module or a companion `HexEscapeLevels.cs` file.
- BFS runs from the exit cell outward on every accepted action using the canonical axial offsets in the "Hex geometry and tile model" section; result written to `connectedSurvivors` before serialisation.
- Win check runs BEFORE threat increment inside `Handle` — this ordering is invariant.
- Tile hand is finite; `PlaceTile` must check hand count before accepting.
- `seatsActedThisRound` uses `List<int>` with a distinct guard (not `HashSet<int>`); all seat logic uses set membership, never index arithmetic.
- Both `HexEscapePhase` and `HexEscapeActionType` enums must carry `[JsonConverter(typeof(JsonStringEnumConverter))]`.
- No `HexEscapeDbContext`; no EF migrations.
- `CreateInitialState` falls back to `"tutorial-01"` when options are null or unparseable and logs an `ILogger` WARNING (AD-10).
- `CreateInitialState` throws `ArgumentException` if the resolved level has 0 survivors (AC-10).
- `CreateInitialState` does NOT evaluate the win condition.

**Frontend:**
- `{agent: frontend, ux}` Hex board rendered from `state.grid` (sparse dictionary); empty cells rendered as blank hexagons.
- Tile rotation UI: clicking a placed tile offers a rotate action; clicking an empty cell offers a place-tile picker.
- Level selector pre-game screen shown to the host in the lobby; dispatches the selected level ID as part of the `CreateRoomRequest.Options` payload (`{ "levelId": "..." }`); other players see the selection via the room SignalR channel.
- Result screen: "Escaped!" (win) or "Overrun!" (loss) — both trigger off `phase === 'GameOver'` with `outcome` as the sole discriminator. Must handle `winnerId === null` — never render null winner. Verify platform lobby chrome also tolerates null `winnerId` (AD-12).
- `registry.ts` one-line add: `import hexescape from './hexescape'` and entry in the registry object.

**Tests — happy path:**
- `{agent: tester}` BFS: fully connected path → `connectedSurvivors == totalSurvivors`.
- BFS: broken path → `connectedSurvivors < totalSurvivors`.
- BFS: partial path (some but not all survivors reachable) → intermediate value.
- Threat counter reaching `>= threatThreshold` triggers `Overrun` + `GameOverEffect(null)`.
- BFS win triggers `Escaped` + `GameOverEffect(null)` without applying threat increment.
- Win check fires before threat increment in the same round-boundary `Handle` call.
- `seatsActedThisRound` resets to empty after all seats have acted.
- Solo game (1 seat): single `PlaceTile` immediately advances threat if no win.
- `Pass` advances round boundary; threat increments when all seats have passed.
- All-pass stalemate repeats until `Overrun` — this is intentional and testable.
- Null-options fallback selects `"tutorial-01"`.
- Same-rotation `RotateTile` is accepted and consumes the turn.
- Catalogue validation: no authored level has 0 survivors.
- Catalogue validation: no authored level is pre-won (BFS passes before any action).

**Tests — unhappy path (rejection cases):**
- `PlaceTile` on occupied cell (pre-placed) → `"Cell is already occupied."`
- `PlaceTile` on occupied cell (player-placed) → `"Cell is already occupied."`
- `PlaceTile` to coord not in level grid → `"Cell is not on the board."`
- `PlaceTile` with rotation outside 0–5 → `"Invalid rotation."`
- `PlaceTile` with hand count = 0 for that type → `"No tiles of that type remaining."`
- `RotateTile` on empty cell → `"No tile to rotate."`
- `RotateTile` on pre-placed tile → `"Cannot rotate a fixed tile."`
- `RotateTile` with rotation outside 0–5 → `"Invalid rotation."`
- Repeat action by seat already in `seatsActedThisRound` → `"It is not your turn."`
- `CreateInitialState` with 0-survivor level → `ArgumentException`.

**Tests — known limitation (skip):**
- Disconnected seat stalls round → `[Fact(Skip="v1 known limitation: disconnected seat stalls round; see follow-up")]`

**Integration tests:**
- AD-9: existing games (FThat, Skyline, LiarsDice) start correctly with null options after the `room.GameOptions` fix.
- AD-12: frontend null-winner `GameFinished` does not crash lobby chrome or render "null won".

**CI / DevOps:**
- `{agent: devops}` No migration and no new DbContext — no CI action required unless that changes in a future story.

---

## Story review

**Reviewed by:** adversarial analyst + tester
**Review date:** 2026-06-15
**Round:** 3 (final)
**Challenges raised:** 28
**Challenges resolved:** 28
**Criteria added:** 7 (AC-10 through AC-16; AC-1–AC-9 substantially rewritten for testability)
**Total AC count:** 16
**Total AD count:** 12

**Verdict: Ready for implementation.**

**Key edge cases that must not be missed:**

1. Win check ALWAYS runs before threat increment in the same `Handle` call (AC-2, AC-3, AC-4, AC-5).
2. Seat indices are a free set — may be non-contiguous after pre-game leave; no index arithmetic (AD-11, AC-7).
3. `GameOverEffect(winnerId: null)` for both win and loss — frontend must not render null winner (AD-12, AC-5, AC-6).
4. Pre-placed tiles are immutable: cannot be rotated or overwritten (AC-3, AC-14).
5. Same-rotation `RotateTile` is accepted (AC-3) — keeps validation simple.
6. Hand is finite; PlaceTile must guard against zero remaining (AC-11).
7. BFS runs from exit outward; dead edges at the grid boundary are not errors (Hex geometry section).
8. `"tutorial-01"` must be authored first; its absence breaks the AD-10 fallback (AD-10, AC-8).
9. All-pass stalemate is intentional — no special handling required (AC-4, AC-15 validation, known limitations).
10. Disconnected seat deadlock is a v1 known limitation — test is skipped with `[Fact(Skip=...)]` (AC-16).

**Test complexity note:** Pure `Handle` and BFS unit tests are straightforward xUnit — no running hub required, no DB, no migrations. The AD-9 platform fix (`room.GameOptions` propagation) and AD-12 null-winner `GameFinished` require integration tests against the running hub and lobby chrome respectively. These are the two tests with the highest setup cost and should be implemented by `{agent: tester}` after backend and frontend are committed.
