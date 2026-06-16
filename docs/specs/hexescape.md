# Spec: Hex Escape (Outbreak)

**Status:** Agreed — v2 supersedes v1
**Date:** 2026-06-16
**Authors:** analyst + architect

Supersedes the v1 threat-counter design (preserved in git history). v1's hex geometry, tile model, and connection rule carry forward unchanged.

---

## Problem

Meepliton's game library is entirely competitive: every title has a winner and losers. Hex Escape v1 introduced co-op tile placement but used a passive threat counter that never manifested on the board. Players had no spatial pressure — the game was a pipe-puzzle with a countdown bolted on. Hex Escape v2 (Outbreak) replaces the counter with actual zombie tokens that move on the grid and eliminate characters, creating genuine co-op tension and making every tile placement matter.

---

## Solution

Hex Escape (Outbreak) is played on a sparse hexagonal grid identical to v1. Players collectively draw tiles from a shared deck each round and place or rotate them to create open paths. Characters representing each player start at designated survivor cells and must all reach the exit. Zombie tokens start at spawn points defined by the level and move each round based on a d6 roll; any character sharing a cell with a zombie is eliminated. Play ends when all non-eliminated characters reach the exit (win) or all characters are eliminated (loss). A zombie tile drawn from the deck must be placed immediately, spawning a new zombie at that cell.

The game is implemented as a rewrite of the v1 module in place under the same game id `hexescape`. The level loader and `SetupOptions` dropdown from v1 carry forward; v2 ships one level (the tutorial) with the loader already wired for follow-up levels.

---

## Hex geometry and tile model

This section is the single source of truth for both backend BFS and frontend rendering. Backend and frontend must not define a second or divergent coordinate convention. This section is carried forward verbatim from v1.

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

### Tile types (five types)

| Tile type  | Base edges (before rotation) |
|------------|------------------------------|
| `straight` | {0, 3}                       |
| `elbow`    | {0, 1}                       |
| `tee`      | {0, 1, 2}                    |
| `cross`    | {0, 1, 2, 3}                 |
| `deadend`  | {0}                          |

Y-junction is excluded. These five types and their base-edge sets are the complete tile vocabulary.

### BFS direction (win check)

BFS runs **from the exit cell outward** over open shared edges (both sides of the edge must satisfy the connection rule above). `connectedCharacters` = the number of non-eliminated character positions reachable from the exit via this BFS. Win condition: `connectedCharacters == nonEliminatedCharacterCount` and `nonEliminatedCharacterCount >= 1`.

---

## Acceptance criteria

### Setup

- [ ] **AC-v2-1 — Initial state:** Given a host selects a valid level and starts the game, when `CreateInitialState` is called, then: the grid is populated with pre-placed tiles; each player has a character at the designated survivor start cell (or shared start cell if fewer positions than players); each player's hand is empty (draw happens in the first Drawing phase); the deck is shuffled using `Random.Shared`; `phase` is `Drawing`; `roundNumber` is 1; `zombies` contains all level-defined starting zombie tokens with stable string ids; `seatsActedThisRound` is empty; `lastZombieRolls` is empty. `CreateInitialState` does NOT evaluate the win condition.

- [ ] **AC-v2-2 — Null options fallback:** Given `CreateInitialState` receives null or malformed options, then it silently substitutes the default level (`"tutorial-01"`), returns valid initial state, and emits a server-side WARNING log: `"HexEscape: options missing/unknown level '{id}', falling back to tutorial-01"`.

- [ ] **AC-v2-3 — Zero-survivor level rejected:** Given `CreateInitialState` is called with a level whose `survivorStartCells` list is empty, then it throws `ArgumentException`. A catalogue-validation unit test asserts no authored level has 0 survivors.

- [ ] **AC-v2-4 — Pre-won level disallowed:** No authored level may start with all non-eliminated characters already connected to the exit. A catalogue-validation unit test asserts this for every authored level.

### Drawing phase

- [ ] **AC-v2-5 — Draw to hand size:** At the start of each round (phase = Drawing), the server draws tiles from the top of the deck for each player until that player's hand reaches 3 tiles, or the deck is exhausted. If the deck has fewer tiles than needed, players receive whatever remains; no crash occurs and no reshuffle happens. Drawing is server-computed inside `Handle` at the round boundary — there is no client "draw" action.

- [ ] **AC-v2-6 — Zombie tile drawn:** A zombie tile drawn by a player enters that player's hand as a `HeldTile { tileType, rotation: null, isZombieTile: true }`. That player must PlaceZombieTile as their first action in the Actions phase before any other action is accepted. If the player attempts any other action while holding a zombie tile, the action is rejected with the exact string `"You must place your zombie tile first."`.

- [ ] **AC-v2-7 — Deck exhaustion (no crash):** Given the deck is empty when a Drawing phase begins, all players receive zero new tiles. The Actions phase proceeds normally. Players with empty hands may only Pass.

### Actions phase

- [ ] **AC-v2-8 — PlaceTile (valid):** Given a player whose seat is NOT in `seatsActedThisRound` dispatches `PlaceTile { coord, tileType, rotation }`, and the coord exists in the level grid, is unoccupied, the player's hand contains that tile type with `isZombieTile: false`, and rotation is in 0–5, then: the cell is populated; the tile is removed from hand; BFS is recomputed; the seat is added to `seatsActedThisRound`. Win check runs immediately (see AC-v2-14).

- [ ] **AC-v2-9 — PlaceZombieTile (valid):** Given a player holding a zombie tile dispatches `PlaceZombieTile { coord }`, and coord is an EMPTY cell (not occupied by any tile or a zombie), then: the cell is populated with the zombie tile (fixed, non-rotatable); the tile is removed from hand; a new zombie token is spawned at coord with a stable generated id; the seat is added to `seatsActedThisRound`. Win check runs immediately.

- [ ] **AC-v2-10 — PlaceZombieTile on occupied cell rejected:** Given `PlaceZombieTile` targets a coord already occupied by a tile, the action is rejected with `"Cell is already occupied."`. State unchanged.

- [ ] **AC-v2-11 — PlaceZombieTile with no legal cell:** Given a player holds a zombie tile but every cell on the board is occupied, the zombie tile is discarded (appended to `discardPile`, no spawn), and the seat is added to `seatsActedThisRound` as if the player placed normally. Win check runs immediately.

- [ ] **AC-v2-12 — RotateTile (valid):** Given a player whose seat is NOT in `seatsActedThisRound` dispatches `RotateTile { coord, rotation }`, rotation is in 0–5, and the cell contains a player-placed non-zombie tile, then: the rotation is updated; BFS is recomputed; seat added to `seatsActedThisRound`. Same-rotation RotateTile is accepted and consumes the turn. Win check runs immediately.

- [ ] **AC-v2-13 — MoveCharacter (valid):** Given a player whose seat is NOT in `seatsActedThisRound` dispatches `MoveCharacter { toCoord }`, and the player's character is non-eliminated, and the connection rule is satisfied in both directions between the character's current cell and `toCoord` (i.e. current cell's edge toward toCoord is open AND toCoord's opposite edge is open AND toCoord exists in the level grid), then: the character's `pos` is updated to `toCoord`; seat is added to `seatsActedThisRound`. Win check runs immediately. Movement is one hex per action (no multi-hex sprint).

- [ ] **AC-v2-14 — Win check (after each action):** After every accepted action (PlaceTile, PlaceZombieTile, RotateTile, MoveCharacter, or Pass), the server checks: if all non-eliminated characters are on the exit cell AND at least one non-eliminated character exists, then `phase` → `GameOver`, `outcome` → `Escaped`, emit `GameOverEffect(winnerId: null)`. When the win triggers, the ZombieMovement phase is NOT run and the round boundary is NOT advanced.

- [ ] **AC-v2-15 — Pass:** Given a player whose seat is NOT in `seatsActedThisRound` dispatches `Pass`, then: the seat is added to `seatsActedThisRound`; state unchanged. Win check runs immediately (per AC-v2-14). If no win, and round boundary is reached, ZombieMovement runs (per AC-v2-16).

- [ ] **AC-v2-16 — Round boundary — ZombieMovement and loss check:** When the last seated player completes their action (all seat indices are in `seatsActedThisRound`), in the SAME `Handle` invocation: (1) phase transitions to `ZombieMovement`; (2) for each zombie in order: roll `Random.Shared.Next(1, 7)` (1–6), map die face to direction (face mod 6), check the full connection rule (zombie's edge open AND neighbour exists AND neighbour's opposite edge open); if satisfied move the zombie, else it stays; store `{ zombieId, dieFace, direction, moved }` in `lastZombieRolls`; (3) any character sharing a cell with any zombie after all moves is marked `eliminated: true`; (4) loss check: if ALL characters are eliminated, `phase` → `GameOver`, `outcome` → `Overrun`, emit `GameOverEffect(winnerId: null)` — stop processing; (5) if no loss: run Drawing for the next round (each player draws to hand size 3 from deck), increment `roundNumber`, set `seatsActedThisRound` to empty, set `phase` → `Actions`.

- [ ] **AC-v2-17 — Loss excludes win:** Win (AC-v2-14) is checked after each individual action in the Actions phase. Loss (AC-v2-16) is checked only after ZombieMovement completes. They cannot resolve in the same `Handle` call because they occur in different phases.

- [ ] **AC-v2-18 — Eliminated player still participates:** An eliminated player's character no longer counts toward win or loss tracking, but that player still draws tiles, must PlaceZombieTile when holding one, and may otherwise Pass. Their seat remains in the round and must still be included in `seatsActedThisRound` for the round boundary to advance.

### Validation (rejection cases)

- [ ] **AC-v2-19 — Repeat action rejected:** A player whose seat is already in `seatsActedThisRound` dispatching any action is rejected with `"It is not your turn."`. State unchanged.

- [ ] **AC-v2-20 — PlaceTile coord not on board:** Rejected with `"Cell is not on the board."`.

- [ ] **AC-v2-21 — PlaceTile on occupied cell:** Rejected with `"Cell is already occupied."`.

- [ ] **AC-v2-22 — PlaceTile with 0 of that type in hand:** Rejected with `"No tiles of that type remaining."`.

- [ ] **AC-v2-23 — Invalid rotation:** `PlaceTile` or `RotateTile` with rotation outside 0–5 rejected with `"Invalid rotation."`.

- [ ] **AC-v2-24 — RotateTile on empty cell:** Rejected with `"No tile to rotate."`.

- [ ] **AC-v2-25 — RotateTile on pre-placed tile:** Rejected with `"Cannot rotate a fixed tile."`.

- [ ] **AC-v2-26 — MoveCharacter along closed edge:** Rejected with `"No open path to that cell."`.

- [ ] **AC-v2-27 — MoveCharacter by eliminated character:** Rejected with `"Your character has been eliminated."`.

### State projection

- [ ] **AC-v2-28 — Projection hides other players' hands:** Given `HasStateProjection = true` and `ProjectStateForPlayer` is called for player P, then: P's own hand is returned in full; every other player's hand is returned as an empty list; `handSizes: { playerId → int }` exposes each player's true tile count; `deck` is returned as an empty list; `deckSize: int` exposes the true deck count. Board, zombies, characters, phase, roundNumber, lastZombieRolls, and discard count are all returned unmasked.

- [ ] **AC-v2-29 — Projection is pure:** `ProjectStateForPlayer` never mutates the input state. It deserializes, constructs a new state value using `with`-expressions or equivalent, and reserializes. It does not project from live object references.

---

## Architecture decisions

### AD-OB-1: Replace v1 in place (game id `hexescape`)

v2 rewrites the existing `hexescape` module on branch `claude/hex-pipe-zombie-coop-mtlx2m`. The game id, `SetupOptions` mechanism (ADR-012), `GameSetupOption.cs`, `IGameModule.SetupOptions`, AD-9 `room.GameOptions` transport, ADR-013 optional `ILogger`, lobby SetupOptions chrome, and the frontend `HexBoard` axial-to-pixel rendering and geometry are all KEPT without modification.

Files to DELETE/REWRITE: `HexEscapeModule.cs`, `Models/HexEscapeModels.cs`, `HexEscapeLevels.cs`, frontend `types.ts`, frontend `Game.tsx`, and `HexEscapeModuleTests.cs`.

Prerequisite before deleting `HexEscapeModuleTests.cs`: PORT the pure-geometry tests (OpenEdges rotation, opposite-edge, connection rule, BFS reachability) into the new test file. Do not lose math coverage.

### AD-OB-2: RNG via `Random.Shared` inside `Apply` — no seed in state

All randomness (deck shuffle at start; per-zombie d6 each round) uses `Random.Shared` called inside `Apply`/`Handle`, matching the SushiGo/LoveLetter precedent. The resulting rolls are stored in state as `lastZombieRolls: [{ zombieId, dieFace, direction, moved }]` for animation and audit. No RNG seed is stored in state. No change to `GameContext`.

Rationale: nothing in the platform replays `Handle` from the action log. `GameDispatcher` invokes `Handle` once; the only re-run is a post-rollback retry against the same committed state. Storing a seed buys nothing.

### AD-OB-3: `HasStateProjection = true`; implement `ProjectStateForPlayer`

v2 flips `HasStateProjection` from `false` (v1) to `true`. `ProjectStateForPlayer` follows the LoveLetter template: own hand is returned in full; other players' hands are masked to empty lists with real counts in `handSizes`; deck is stripped to an empty list with `deckSize` exposed. Board/grid, zombies, characters, phase, roundNumber, lastZombieRolls, and discard count are all public. Projection must be pure (deserialize / `with` / reserialize — never mutate input or project off live references).

### AD-OB-4: State shape

Top-level fields:

- `characters: [{ playerId, pos, eliminated }]` — SEPARATE from `players`; eliminated applies to the character, not the player slot.
- `players` — pure identity slots, unchanged from platform convention.
- `hands: { playerId → HeldTile[] }` — `HeldTile { tileType, rotation?, isZombieTile }`.
- `deck: DeckEntry[]` — `DeckEntry { tileType, isZombieTile }`. Present in full server-side; stripped to `[]` with `deckSize` in projection.
- `discardPile: DeckEntry[]` — present but inert in v2; retained as a hook for follow-up (reshuffle, etc.).
- `zombies: [{ id, pos }]` — stable string ids for animation continuity across rounds.
- `roundNumber: int`.
- `phase: HexEscapePhase`.
- `lastZombieRolls: [{ zombieId, dieFace, direction, moved }]` — reset each ZombieMovement run.
- `seatsActedThisRound: int[]` — distinct list, not `HashSet<int>` (clean JSON round-trip).
- `connectedCharacters: int` — derived BFS count, broadcast for UI.
- `outcome: HexEscapeOutcome?` — null until GameOver.

Directions: int 0–5 reusing the v1 direction table. Every new enum carries `[JsonConverter(typeof(JsonStringEnumConverter))]`. The module keeps the global `JsonStringEnumConverter` in `SerializerOptions`. `types.ts` mirrors enums as PascalCase string unions; all field and action prop names in camelCase.

### AD-OB-5: Phase model

New enum `HexEscapePhase { Drawing, Actions, ZombieMovement, GameOver }`. New enum `HexEscapeOutcome { Escaped, Overrun }`.

Only the Actions phase accepts client actions. Drawing and ZombieMovement are server-computed inside `Handle` at the round boundary (same invocation as the last Actions-phase action). No client "advance phase" or "roll dice" action exists.

### AD-OB-6: Round structure

1. **Drawing** — each player draws from the top of the shared shuffled deck until their hand reaches the fixed hand size (3), or the deck is exhausted. A drawn zombie tile enters the player's hand and creates a forced PlaceZombieTile obligation.
2. **Actions** — free-order (reusing `seatsActedThisRound`). One action per seat per round: `PlaceTile`, `PlaceZombieTile`, `RotateTile`, `MoveCharacter`, or `Pass`. A player holding a zombie tile MUST PlaceZombieTile before any other action. If no legal cell exists for the zombie tile, it is discarded with no spawn.
3. **ZombieMovement** (server) — per zombie, roll d6 → direction (die face mod 6); move iff the zombie's edge is open AND the neighbour exists AND the neighbour's opposite edge is open (full v1 connection rule); else the zombie stays. After all moves, any character sharing a zombie's cell is eliminated.

### AD-OB-7: Win/loss ordering

WIN is checked after each individual action in the Actions phase. The team wins (`Escaped`, `GameOverEffect(null)`) when all non-eliminated characters are on the exit cell AND at least one non-eliminated character exists.

LOSS is checked after the server-run ZombieMovement. The team loses (`Overrun`, `GameOverEffect(null)`) when ALL characters are eliminated.

Win and loss cannot resolve in the same `Handle` call because they are checked in different phases. This ordering is invariant.

### AD-OB-8: Forced zombie tile with no legal cell

If a player holds a zombie tile and every cell on the board is occupied, the tile is appended to `discardPile` with no zombie spawned. The player's seat is added to `seatsActedThisRound` as normal. Eliminated players still draw and must attempt to place zombie tiles; if no cell is available the same discard rule applies.

### AD-OB-9: Zombie tile scaling (balance TBD)

The proportion of zombie tiles in the deck scales with player count. The following table is the starting point — numbers are explicitly marked as **balance TBD by playtest** and must be tunable constants in one place (not scattered or frozen as a contract):

| Players | Zombie tiles | Total deck size |
|---------|-------------|-----------------|
| 1       | 3           | 30              |
| 2       | 5           | 40              |
| 3       | 7           | 50              |
| 4       | 10          | 60              |
| 5       | 12          | 70              |
| 6       | 15          | 80              |

### AD-OB-10: First-cut scope trims (locked for v2)

These are deliberately constrained to keep v2 shippable. Each trim leaves a field or constant hook so the follow-up is purely additive:

- No discard reshuffle — deck empties and stays empty; `discardPile` field is present.
- No multi-hex sprint — one hex per MoveCharacter; constant `MaxMoveDistance = 1`.
- No zombie-tile overwrite — zombie tiles may only target empty cells.
- Fixed hand size 3 — constant `HandSize = 3`.
- Ship one v2 level (the tutorial) — level loader and SetupOptions dropdown remain wired; more levels are a follow-up.

### AD-OB-11: No database changes

All state lives in the JSONB blob. No `DbContext`, no tables, no migrations. Contract impact: zero changes to `IGameModule`, `IGameHandler`, `GameContext`, `GameResult`, or the TypeScript `GameModule`/`GameContext` interfaces. The only opt-in change is flipping `HasStateProjection` from `false` to `true` — an existing mechanism.

### Carried-forward ADRs from v1

The following v1 architecture decisions remain in effect unchanged:

- **ADR-012** — `IGameModule.SetupOptions` and `GameSetupOption.cs` (generic level selector mechanism).
- **AD-9** — `POST /rooms` populates `room.GameOptions` from `req.Options` (already resolved in v1 implementation).
- **ADR-013** — Optional `ILogger` injection.
- **AD-1** — Implement `IGameModule + IGameHandler` directly (not `ReducerGameModule`), required to emit `GameOverEffect`.
- **AD-2** — Sparse `Dictionary<string, HexCell>` grid keyed `"q,r"`.
- **AD-7** — `SupportsUndo = false`.
- **AD-8** — `MinPlayers = 1`, `MaxPlayers = 6`.
- **AD-11** — `seatsActedThisRound` as `List<int>` with distinct guard (not `HashSet<int>`); all seat logic uses set membership.
- **AD-12** — Frontend must handle `GameFinished` with `winnerId = null`; use `outcome` as sole discriminator.

---

## Out of scope

- Survivors-to-rescue mechanic (future story)
- Discard reshuffle
- Multi-hex sprint (more than one hex per MoveCharacter)
- Zombie tile overwrite of occupied cells
- Dynamic balance tuning at runtime
- Additional levels beyond the tutorial (follow-up story)
- Any RNG seed stored in state or passed via `GameContext`
- Any change to `IGameModule`, `IGameHandler`, `GameContext`, `GameResult`, or TypeScript `GameModule`/`GameContext`
- Database tables, EF migrations, or `HexEscapeDbContext`
- Host-initiated skip-seat or per-seat disconnect timeout
- Spectator mode or late-join
- Co-op undo with player consent
- Y-junction tile type
- Procedural level generation

---

## Implementation hints

### Backend (rewrite module / models / levels)

- Implement `HexEscapeModule` implementing both `IGameModule` and `IGameHandler` (AD-1), following `FThatModule.cs` as template.
- Port pure-geometry tests (OpenEdges rotation, opposite-edge, connection rule, BFS reachability) into the new test file BEFORE deleting `HexEscapeModuleTests.cs`.
- All randomness via `Random.Shared` inside `Handle`/`Apply` — no seed in state (AD-OB-2).
- `HasStateProjection = true`; implement `ProjectStateForPlayer` as pure deserialize/with/reserialize (AD-OB-3).
- Author the tutorial level first — it is the fallback target; its absence breaks the AD-10 null-options fallback.
- Every enum (`HexEscapePhase`, `HexEscapeOutcome`, tile type enums) must carry `[JsonConverter(typeof(JsonStringEnumConverter))]`.
- `seatsActedThisRound` as `List<int>` with distinct guard; all logic uses `.Contains()`, never index arithmetic (AD-11).
- Win check after each action BEFORE round boundary processing; loss check after ZombieMovement (AD-OB-7).
- `CreateInitialState` falls back to `"tutorial-01"` with ILogger WARNING when options are null or malformed (AD-10).
- `CreateInitialState` throws `ArgumentException` if resolved level has 0 survivors.
- No `HexEscapeDbContext`; no EF migrations.

### Frontend (rewrite types.ts and Game.tsx; keep HexBoard)

- Keep `HexBoard` axial-to-pixel rendering and geometry unchanged.
- Rewrite `types.ts` to mirror the v2 state shape (AD-OB-4) in camelCase.
- Add zombie token layer and character token layer over `HexBoard`.
- Render per-player hands, deck size, round number, and phase indicator.
- Animate `lastZombieRolls` — show dice result and movement arrow per zombie.
- Result screen: `outcome === 'Escaped'` → "Escaped!"; `outcome === 'Overrun'` → "Overrun!". Never render null winner. Verify platform lobby chrome tolerates `winnerId === null` without crashing (AD-12).
- Highlight that a player holding a zombie tile must place it first (block other action buttons with a tooltip: "You must place your zombie tile first.").
- TypeScript enums mirror as PascalCase string unions; all field names camelCase.

### Tester

- Port pure-geometry tests first, before the old test file is deleted.
- New test suite covers: deck shuffle produces correct zombie-tile count; Drawing phase deals to hand size 3; deck exhaustion deals fewer tiles without crash; PlaceZombieTile spawns zombie; PlaceZombieTile with no legal cell discards with no spawn; ZombieMovement rolls and moves correctly; eliminated character not counted in win check; win requires at least one non-eliminated character; loss requires ALL characters eliminated; win check fires before ZombieMovement in the same Handle call; projection hides other players' hands and deck.
- Port unhappy-path rejection tests from v1 (occupied cell, not-on-board, invalid rotation, repeat action, etc.) updating for v2 action set.
- Disconnected seat stalls round: `[Fact(Skip="v1 known limitation: disconnected seat stalls round; see follow-up")]`.

### DevOps

No migration and no new DbContext — no CI action required for v2.

---

## Story review

**Verdict:** Approved — conditional on 7 Must-fix items (all encoded above as ACs and ADs). Zero contract changes.
**Spec round:** 3 (final — analyst + architect aligned)
**Total AC count:** 29 (AC-v2-1 through AC-v2-29)
**Total AD count:** 11 new (AD-OB-1 through AD-OB-11) + 10 carried forward from v1
