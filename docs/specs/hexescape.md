# Spec: Hex Escape (Outbreak)

**Status:** Refined v3 — pending architect confirmation on round-boundary mechanics (D1/D2) before implementation begins
**Date:** 2026-06-16
**Authors:** analyst + architect
**Revision:** 3 — incorporates Round 3 story-review resolutions. All 32 adversarial challenges addressed; 7 blockers resolved. Owner design decisions D1–D4 encoded as ADs. AC count restructured and expanded.

Supersedes the v1 threat-counter design (preserved in git history). v1's hex geometry, tile model, and connection rule carry forward unchanged. Supersedes v2 revision 2 (which had exit-placement player choice, no anti-turtle rule, no mandatory draw, no directional layout, single-int AP tracking without activeSeat, and conflated ACs 15–18 and 28 and 31).

---

## Problem

Meepliton's game library is entirely competitive: every title has a winner and losers. Hex Escape v1 introduced co-op tile placement but used a passive threat counter that never manifested on the board. Players had no spatial pressure — the game was a pipe-puzzle with a countdown bolted on. Hex Escape v2 (Outbreak) replaces the counter with actual zombie tokens that move on the grid and eliminate characters, creating genuine co-op tension and making every tile placement matter.

---

## Solution

Hex Escape (Outbreak) is played on a sparse hexagonal grid identical to v1. Players collectively draw tiles from a shared deck each round by spending action points (AP); they place and rotate tiles to create open paths and move their characters toward a hidden exit. Characters representing each player start on the first tile they place, which must fall within the level's spawn zone on one side of the board. The exit zone is on the opposite side and is reserved — no normal tile may be placed there. Zombie tokens start at spawn points defined by the level and move each round based on a d6 roll; any character sharing a cell with a zombie is eliminated immediately. A single special exit tile is shuffled into the bottom portion of the deck; when a player draws it, the server immediately places it deterministically on an exit-zone cell — the player does not choose where. Play ends when all placed, non-eliminated characters physically occupy the exit cell (win) or all characters who were ever placed are eliminated (loss). A zombie tile drawn from the deck creates a forced-placement obligation, spawning a new zombie at a player-chosen empty non-exit-zone cell.

Each player's turn is governed by an **action-point (AP) pool of `ApPoolSize` AP** (tunable constant; initial value 3). On their turn a player may spend AP in any order and any mix on four atomic actions: draw a tile (1 AP), place a tile from hand (1 AP), rotate a player-placed tile (1 AP), or move their character one hex (1 AP). Drawing is explicit and mandatory each turn (unless hand is full or deck is empty). Unused AP do not carry over.

To prevent turtling (sealing zombies away), a zombie that is fully contained — no connection-rule-valid move in any direction — both rotates its own tile to break out and spawns an additional zombie on an adjacent tiled cell. To maintain pressure as the deck shrinks, one additional zombie spawns from a level-defined horde origin each round boundary.

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

### exitConnectedCount (UI hint only — does NOT gate the win)

BFS runs **from the exit cell outward** over open shared edges (both sides of the edge must satisfy the connection rule above). `exitConnectedCount` = the number of non-eliminated, placed character positions reachable from the exit via this BFS. This value is a **UI hint only**. It is NOT a win condition. Win is physical co-location on `exitCell`, not BFS connectivity. `exitConnectedCount` is 0 when `exitRevealed == false`.

---

## Acceptance criteria

### Setup

- [ ] **AC-v2-1 — Initial state:** Given a host selects a valid level and starts the game, when `CreateInitialState` is called, then: the grid is populated with pre-placed tiles; no player has a character position yet (characters are unplaced until their first tile placement in the spawn zone); every player's hand is empty; the deck is shuffled using `Random.Shared` with the exit tile placed randomly within the last 25% of the deck (see AD-OB-4); `phase` is `Actions`; `roundNumber` is 1; `zombies` contains all level-defined starting zombie tokens with stable string ids; `seatsActedThisRound` is empty; `lastZombieRolls` is empty; `exitRevealed` is `false`; `exitCell` is `null`; `activeSeat` is `null`; `actionPointsRemaining` is 0 (no seat is active yet). `CreateInitialState` does NOT evaluate the win condition.

- [ ] **AC-v2-2 — Null options fallback:** Given `CreateInitialState` receives null or malformed options, then it silently substitutes the default level (`"tutorial-01"`), returns valid initial state, and emits a server-side WARNING log: `"HexEscape: options missing/unknown level '{id}', falling back to tutorial-01"`.

- [ ] **AC-v2-3 — Zero-survivor level rejected:** Given `CreateInitialState` is called with a level whose `spawnZoneCells` list is empty, then it throws `ArgumentException`. A catalogue-validation unit test asserts no authored level has an empty spawn zone.

- [ ] **AC-v2-4 — Pre-won level disallowed:** No authored level may start with `exitRevealed == true`. A catalogue-validation unit test asserts `exitRevealed` starts `false` for every authored level.

- [ ] **AC-v2-5 — Catalogue completeness:** A catalogue-validation unit test asserts that for every authored level: `spawnZoneCells.Count >= MaxPlayers` (6); `exitZoneCells` is non-empty; `hordeOriginCells` is non-empty; the deck produced by `CreateInitialState` contains exactly one `isExitTile: true` entry; deck composition matches the scaling table (AD-OB-9); and the level is structurally solvable (exit zone is reachable from spawn zone via in-grid cells, ignoring tile rotation). The tutorial level satisfies all of these.

### AP turn model and seat claiming

- [ ] **AC-v2-6 — Seat claiming:** A turn is claimed when the first action from an unacted, non-active seat is received. On claiming: `activeSeat` is set to that seat index; `actionPointsRemaining` is reset to `ApPoolSize`. Only `activeSeat` may take actions while a seat is active. Any action from a seat that is not `activeSeat` (and not claiming because `activeSeat` is already occupied) is rejected with `"It is not your turn."`.

- [ ] **AC-v2-7 — AP spent per atomic action:** Each accepted atomic action — DrawTile, PlaceTile, PlaceZombieTile, RotateTile, MoveCharacter — costs exactly 1 AP and decrements `actionPointsRemaining`. An action dispatched by `activeSeat` when `actionPointsRemaining == 0` is rejected with `"No action points remaining."`.

- [ ] **AC-v2-8 — Turn ends — AP exhausted:** When `actionPointsRemaining` reaches 0 after the last AP spend: the seat is added to `seatsActedThisRound`; `activeSeat` → `null`; `actionPointsRemaining` → 0. Another unacted seat may now claim.

- [ ] **AC-v2-9 — Turn ends — EndTurn:** When `activeSeat` dispatches `EndTurn` (0 AP cost): remaining AP are forfeited; the seat is added to `seatsActedThisRound`; `activeSeat` → `null`; `actionPointsRemaining` → 0. Another unacted seat may now claim.

- [ ] **AC-v2-10 — Mandatory draw enforcement:** On each turn, a player MUST spend at least 1 AP on `DrawTile` before they may dispatch `EndTurn`, UNLESS their hand is already at `HandSize` (3) tiles OR the deck is empty. If a player dispatches `EndTurn` without having drawn while a draw was possible (hand not full AND deck not empty), the action is rejected with `"You must draw a tile this turn."`. The mandatory-draw obligation is satisfied by any `DrawTile` that succeeds in that turn, including a draw that produces a zombie tile.

- [ ] **AC-v2-11 — Hand cap enforced on draw:** `DrawTile` is rejected with `"Hand is full."` when the player's hand already contains `HandSize` (constant: 3) tiles.

- [ ] **AC-v2-12 — Deck empty draw rejected:** `DrawTile` is rejected with `"The deck is empty."` when `deck` is empty. The mandatory-draw obligation is automatically waived when the deck is empty.

### Spawn zone and character start

- [ ] **AC-v2-13 — First tile must go in spawn zone:** A player's first `PlaceTile` action must target a cell in the level's `spawnZoneCells`. If the coord is not in `spawnZoneCells`, the action is rejected with `"First tile must be placed in the spawn zone."`. Subsequent placements are unrestricted (except exit zone, see AC-v2-21).

- [ ] **AC-v2-14 — Character spawns on first placed tile:** When a player's first `PlaceTile` is accepted, their character is created at the placed tile's coord with `eliminated: false`. Before their first placement the character has no position and is not counted in win or loss checks — unplaced characters are treated as NOT IN PLAY.

- [ ] **AC-v2-15 — Spawn zone cell may be shared:** Two or more players may place their first tile on the same spawn-zone cell if it is empty at the moment of each placement. Characters at the same cell are allowed (co-location is not itself a win trigger).

- [ ] **AC-v2-16 — Spawn zone full:** If all `spawnZoneCells` are occupied before a player has placed their first tile, the `PlaceTile` action for that player is rejected with `"Spawn zone is full — no cell available for first placement."`. Level design must ensure `spawnZoneCells.Count >= MaxPlayers`; the catalogue-validation test (AC-v2-5) enforces this.

### Exit zone and server exit placement

- [ ] **AC-v2-17 — Exit zone is reserved:** `PlaceTile` and `PlaceZombieTile` targeting a cell in `exitZoneCells` are rejected with `"Cannot place tiles in the exit zone."`. This applies to all players and all tile types.

- [ ] **AC-v2-18 — Exit tile in deck:** The deck contains exactly one entry with `isExitTile: true`. It is placed at a random position within the last 25% of the deck during `CreateInitialState` (see AD-OB-4 for the exact rule). Before it is drawn, `exitRevealed` is `false` and `exitCell` is `null`.

- [ ] **AC-v2-19 — Server places exit tile deterministically:** When a player's `DrawTile` action draws the exit tile from the deck, the server immediately and deterministically places it on an exit-zone cell. The placement algorithm: find all exit-zone cells that are currently empty (no tile placed there yet); select the empty exit-zone cell closest to the board centre (tie-break: lowest lexicographic `"q,r"` key); place the exit tile there; set `exitCell` to that coord and `exitRevealed` to `true`; remove the exit tile from the deck draw result (it does not enter the player's hand). The player's hand is unchanged; no forced-placement obligation is created. The draw costs 1 AP (normal draw cost). The mandatory-draw obligation is satisfied.

- [ ] **AC-v2-20 — Win check after server exit placement:** Immediately after the server places the exit tile (AC-v2-19), win check runs. If all non-eliminated, placed characters are already on `exitCell`, win fires immediately.

- [ ] **AC-v2-21 — exitRevealed invariant:** `exitRevealed` starts `false`. It becomes `true` at the moment the exit tile is drawn and server-placed. It never becomes `false` again.

### Tile placement and rotation

- [ ] **AC-v2-22 — PlaceTile (valid, non-first):** Given `activeSeat` dispatches `PlaceTile { coord, tileType, rotation }`, the player has already placed their first tile (character exists), the coord exists in the level grid and is not in `exitZoneCells`, the cell is unoccupied, the player's hand contains that tile type with `isZombieTile: false` and `isExitTile: false`, and rotation is in 0–5, then: the cell is populated; the tile is removed from hand; `exitConnectedCount` is recomputed (if `exitRevealed`); `actionPointsRemaining` decremented by 1. Win check runs immediately (if `exitRevealed`).

- [ ] **AC-v2-23 — PlaceZombieTile (valid):** Given `activeSeat` holds a zombie tile and dispatches `PlaceZombieTile { coord }`, the coord is an EMPTY cell NOT in `exitZoneCells`, and the forced zombie-tile obligation is active (player must place zombie tile before other non-forced actions), then: the cell is populated with the zombie tile (fixed, non-rotatable); the tile is removed from hand; a new zombie token is spawned at coord with a stable generated id; co-location elimination check runs (if any character is at coord, eliminate it); `actionPointsRemaining` decremented by 1. Win check runs immediately (if `exitRevealed`).

- [ ] **AC-v2-24 — PlaceZombieTile on occupied cell rejected:** Rejected with `"Cell is already occupied."`. State unchanged. Obligation remains.

- [ ] **AC-v2-25 — PlaceZombieTile with no legal cell:** Given a player holds a zombie tile but every non-exit-zone cell on the board is occupied, the zombie tile is appended to `discardPile` (no spawn); `actionPointsRemaining` decremented by 1. The forced discard costs 1 AP. Win check runs immediately (if `exitRevealed`).

- [ ] **AC-v2-26 — RotateTile (valid):** Given `activeSeat` dispatches `RotateTile { coord, rotation }`, rotation is in 0–5, and the cell contains a player-placed non-zombie tile, then: the rotation is updated; `exitConnectedCount` recomputed (if `exitRevealed`); `actionPointsRemaining` decremented by 1. Same-rotation RotateTile is accepted and costs 1 AP. Win check runs immediately (if `exitRevealed`).

- [ ] **AC-v2-27 — MoveCharacter (valid):** Given `activeSeat` dispatches `MoveCharacter { toCoord }`, the player's character exists, is non-eliminated, and the connection rule is satisfied in both directions between the character's current cell and `toCoord`, then: character `pos` is updated; `actionPointsRemaining` decremented by 1. Co-location elimination check runs BEFORE win check — if `toCoord` contains a zombie, the character is eliminated and win check is NOT run for that character. Win check runs immediately after (if `exitRevealed`).

- [ ] **AC-v2-28 — MoveCharacter before first tile placed rejected:** If a player has not yet placed their first tile (character has no position), `MoveCharacter` is rejected with `"Your character has not been placed yet."`.

### Win condition

- [ ] **AC-v2-29a — Win condition — definition:** Win = `exitRevealed == true` AND at least one placed non-eliminated character exists AND ALL placed non-eliminated characters have `pos == exitCell`. Physical co-location on `exitCell` is the sole win trigger. BFS connectivity (`exitConnectedCount`) does NOT gate the win.

- [ ] **AC-v2-29b — Unplaced players do not block win:** A player who has never placed their first tile (character `pos` is null) is NOT IN PLAY and is ignored for win evaluation. They neither satisfy nor block the win condition.

- [ ] **AC-v2-29c — Win fires — effect:** On win: `phase` → `GameOver`; `outcome` → `Escaped`; emit `GameOverEffect(winnerId: null)`. ZombieMovement is NOT run; round boundary is NOT advanced.

- [ ] **AC-v2-29d — Win skipped before exit revealed:** While `exitRevealed == false`, the win check is skipped entirely. BFS from a null exit cell is not computed.

### Loss condition

- [ ] **AC-v2-30a — Loss condition — definition:** Loss = at least one character was ever placed AND ALL placed characters are `eliminated: true`. Unplaced characters (null `pos`) are ignored for loss evaluation.

- [ ] **AC-v2-30b — Loss fires — effect:** On loss: `phase` → `GameOver`; `outcome` → `Overrun`; emit `GameOverEffect(winnerId: null)`.

- [ ] **AC-v2-30c — Loss check timing:** Loss is checked AFTER zombie movement completes (end of the ZombieMovement phase, after all zombie moves and all resulting eliminations including horde spawn eliminations). Loss is NOT checked during the Actions phase.

### Co-location elimination

- [ ] **AC-v2-31a — Elimination on player move:** When a player moves their character to `toCoord` via `MoveCharacter` and `toCoord` is occupied by one or more zombies, the character is eliminated IMMEDIATELY. Elimination occurs BEFORE the win check. A player moving to `exitCell` while zombies are there is eliminated and does NOT win.

- [ ] **AC-v2-31b — Elimination on zombie move:** When a zombie moves to a cell occupied by one or more characters during ZombieMovement, each such character is eliminated immediately after that zombie's move resolves. All zombie moves are processed first (per-zombie in order), eliminations accumulate, then post-movement loss check and horde spawn occur.

- [ ] **AC-v2-31c — Elimination on zombie spawn:** When a zombie spawns on a cell (via `PlaceZombieTile`, break-out spawn from D1, or horde spawn from D2b) that is occupied by one or more characters, each character on that cell is eliminated immediately at the moment of spawn.

- [ ] **AC-v2-31d — Two zombies may stack:** Two or more zombie tokens may occupy the same cell simultaneously. This is valid state; no special rule applies to stacked zombies beyond their individual movement.

### Round boundary and zombie movement

- [ ] **AC-v2-32a — Round boundary triggers when all seats done:** When the last seated player's turn ends (all seat indices in `seatsActedThisRound`), in the SAME `Handle` invocation, the following phases execute in order with no client interaction between them.

- [ ] **AC-v2-32b — Phase 1 — transition to ZombieMovement:** `phase` transitions to `ZombieMovement`.

- [ ] **AC-v2-32c — Phase 2 — containment evaluation (D1):** For each zombie (in stable id order): determine if the zombie is CONTAINED — a zombie is contained if NONE of the 6 directions yields a connection-rule-valid move (every direction is either off-grid or a closed edge on either side). For each contained zombie: (a) BREAK-OUT: the server rotates the zombie's own tile to an orientation that opens at least one edge toward an existing in-grid neighbour. Pick the lowest direction index d (0–5) such that a neighbour exists in the grid at offset[d]; set the zombie tile's rotation so that it has an open edge in direction d. If no in-grid neighbour exists in any direction, skip the rotation. (b) SPAWN: spawn one new zombie on an adjacent in-grid tiled cell. Pick the lowest direction index d (0–5) such that an in-grid cell with a tile exists at offset[d]; spawn the new zombie there. If no such cell exists, skip the spawn. Co-location elimination check runs for each spawned zombie (AC-v2-31c). Non-contained zombies receive no break-out or spawn.

- [ ] **AC-v2-32d — Phase 3 — per-zombie d6 roll and move:** For each zombie (in stable id order): roll `Random.Shared.Next(1, 7)` (1–6), map die face to direction (die face mod 6). Check the full connection rule: if the zombie's tile has an open edge in that direction AND the neighbour exists in the grid AND the neighbour's tile has an open edge in the opposite direction, move the zombie to the neighbour. Otherwise zombie stays. Store `{ zombieId, dieFace, direction, moved }` in `lastZombieRolls`. Co-location elimination check runs after each zombie move (AC-v2-31b).

- [ ] **AC-v2-32e — Phase 4 — horde spawn (D2b):** Spawn `HordeRatePerRound` (constant: 1) new zombie(s) from `hordeOriginCells`. Deterministic pick: select the first `hordeOriginCells` entry (lowest index) that is an in-grid cell not occupied by a zombie; spawn there. If that cell is zombie-occupied, try the next entry; if all are zombie-occupied or unavailable, skip the spawn for this round. Co-location elimination check runs for each horde-spawned zombie (AC-v2-31c).

- [ ] **AC-v2-32f — Phase 5 — loss check and round advance:** Loss check runs. If loss condition met (AC-v2-30a), game ends. Otherwise: `roundNumber`++; `seatsActedThisRound` → `[]`; `activeSeat` → `null`; `actionPointsRemaining` → 0; `phase` → `Actions`.

- [ ] **AC-v2-33 — Zombie movement once per round:** Zombies move exactly once per round, at the round boundary. Zombie movement is NOT triggered by individual AP actions.

- [ ] **AC-v2-34 — Eliminated player still participates:** An eliminated player's character no longer counts toward win/loss tracking per AC-v2-29a and AC-v2-30a, but that player still takes turns spending AP to draw, place, rotate, or EndTurn. Their seat must still reach turn-end for the round boundary to advance. If they hold a forced zombie tile, the obligation persists.

### Validation (rejection cases)

- [ ] **AC-v2-35 — Action from non-active seat rejected:** A player whose seat is not `activeSeat` and is not claiming a free turn (either `activeSeat` is already occupied by another seat, or the player's seat is already in `seatsActedThisRound`) dispatching any action is rejected with `"It is not your turn."`.

- [ ] **AC-v2-36 — AP exhausted action rejected:** Any atomic action dispatched by `activeSeat` when `actionPointsRemaining == 0` is rejected with `"No action points remaining."`.

- [ ] **AC-v2-37 — PlaceTile coord not on board:** Rejected with `"Cell is not on the board."`.

- [ ] **AC-v2-38 — PlaceTile on occupied cell:** Rejected with `"Cell is already occupied."`.

- [ ] **AC-v2-39 — PlaceTile in exit zone:** Rejected with `"Cannot place tiles in the exit zone."`.

- [ ] **AC-v2-40 — PlaceTile with 0 of that type in hand:** Rejected with `"No tiles of that type remaining."`.

- [ ] **AC-v2-41 — Invalid rotation:** `PlaceTile` or `RotateTile` with rotation outside 0–5 rejected with `"Invalid rotation."`.

- [ ] **AC-v2-42 — RotateTile on empty cell:** Rejected with `"No tile to rotate."`.

- [ ] **AC-v2-43 — RotateTile on pre-placed tile:** Rejected with `"Cannot rotate a fixed tile."`.

- [ ] **AC-v2-44 — MoveCharacter along closed edge:** Rejected with `"No open path to that cell."`.

- [ ] **AC-v2-45 — MoveCharacter by eliminated character:** Rejected with `"Your character has been eliminated."`.

- [ ] **AC-v2-46 — Forced zombie tile action ordering:** While a player holds a zombie tile (and has not yet placed it), dispatching any action other than `PlaceZombieTile` (or the no-legal-cell forced discard path) is rejected with `"You must place your zombie tile first."`.

- [ ] **AC-v2-47 — EndTurn without mandatory draw rejected:** If `activeSeat` dispatches `EndTurn` and has not drawn this turn, and the deck is not empty, and the player's hand is not at `HandSize`, the action is rejected with `"You must draw a tile this turn."`.

### State projection

- [ ] **AC-v2-48 — Projection hides other players' hands:** Given `HasStateProjection = true` and `ProjectStateForPlayer` is called for player P, then: P's own hand is returned in full (including any forced tile flags); every other player's hand is returned as an empty list; `handSizes: { playerId → int }` exposes each player's true tile count; `deck` is returned as an empty list; `deckSize: int` exposes the true deck count. Board, zombies, characters, phase, roundNumber, lastZombieRolls, exitRevealed, exitCell (once revealed), exitConnectedCount, discard count, `activeSeat`, and `actionPointsRemaining` are all returned unmasked.

- [ ] **AC-v2-49 — Projection is pure:** `ProjectStateForPlayer` never mutates the input state.

---

## Architecture decisions

### AD-OB-1: Replace v1 in place (game id `hexescape`)

v2 rewrites the existing `hexescape` module on branch `claude/hex-pipe-zombie-coop-mtlx2m`. The game id, `SetupOptions` mechanism (ADR-012), `GameSetupOption.cs`, `IGameModule.SetupOptions`, AD-9 `room.GameOptions` transport, ADR-013 optional `ILogger`, lobby SetupOptions chrome, and the frontend `HexBoard` axial-to-pixel rendering and geometry are all KEPT without modification.

Files to DELETE/REWRITE: `HexEscapeModule.cs`, `Models/HexEscapeModels.cs`, `HexEscapeLevels.cs`, frontend `types.ts`, frontend `Game.tsx`, and `HexEscapeModuleTests.cs`.

Prerequisite before deleting `HexEscapeModuleTests.cs`: PORT the pure-geometry tests (OpenEdges rotation, opposite-edge, connection rule, BFS reachability) into the new test file. Do not lose math coverage.

### AD-OB-2: RNG via `Random.Shared` inside `Apply` — no seed in state

All randomness (deck shuffle at start including exit-tile placement; per-zombie d6 each round) uses `Random.Shared` called inside `Apply`/`Handle`, matching the SushiGo/LoveLetter precedent. The resulting rolls are stored in state as `lastZombieRolls: [{ zombieId, dieFace, direction, moved }]` for animation and audit. No RNG seed is stored in state. No change to `GameContext`.

Rationale: nothing in the platform replays `Handle` from the action log. `GameDispatcher` invokes `Handle` once; the only re-run is a post-rollback retry against the same committed state. Storing a seed buys nothing.

AP actions (draw, place, rotate, move) are all deterministic given the stored deck order and stored zombie rolls. The only RNG surfaces are (a) deck shuffle at `CreateInitialState` (stored order persists in `deck`) and (b) zombie d6 rolls at each round boundary (stored in `lastZombieRolls`). No new RNG surface is introduced by the AP economy, D1 (containment), or D2 (horde).

### AD-OB-3: `HasStateProjection = true`; implement `ProjectStateForPlayer`

v2 flips `HasStateProjection` from `false` (v1) to `true`. `ProjectStateForPlayer` follows the LoveLetter template: own hand is returned in full; other players' hands are masked to empty lists with real counts in `handSizes`; deck is stripped to an empty list with `deckSize` exposed. Board/grid, zombies, characters, phase, roundNumber, lastZombieRolls, exitRevealed, exitCell, exitConnectedCount, discard count, `activeSeat`, and `actionPointsRemaining` are all public. Projection must be pure (deserialize / `with` / reserialize — never mutate input or project off live references).

### AD-OB-4: State shape

Top-level fields:

- `characters: [{ playerId, pos?, eliminated }]` — `pos` is nullable; null until the player places their first tile in the spawn zone. `eliminated` applies to the character only. Characters with null `pos` are NOT IN PLAY for win/loss.
- `players` — pure identity slots, unchanged from platform convention.
- `hands: { playerId → HeldTile[] }` — `HeldTile { tileType, rotation?, isZombieTile, isExitTile }`.
- `deck: DeckEntry[]` — `DeckEntry { tileType, isZombieTile, isExitTile }`. Present in full server-side; stripped to `[]` with `deckSize` in projection. Exactly one entry has `isExitTile: true`; it is placed during `CreateInitialState` at a random position within the last floor(deckSize * 0.25) positions (minimum index = deckSize - floor(deckSize * 0.25); if deckSize < 4, the exit tile occupies the last position). This shuffle result is stored directly — the deck array in state IS the authoritative order.
- `discardPile: DeckEntry[]` — present but inert in v2; retained as a hook for follow-up.
- `zombies: [{ id, pos }]` — stable string ids for animation continuity across rounds. Two zombies may share a cell (stacking is valid).
- `roundNumber: int`.
- `phase: HexEscapePhase`.
- `lastZombieRolls: [{ zombieId, dieFace, direction, moved }]` — reset each ZombieMovement run.
- `seatsActedThisRound: int[]` — distinct list, not `HashSet<int>` (clean JSON round-trip).
- `activeSeat: int?` — the seat index currently taking its turn; null between turns and before the first turn is claimed each round.
- `actionPointsRemaining: int` — AP remaining for `activeSeat`. 0 when `activeSeat` is null.
- `exitConnectedCount: int` — derived BFS count from exit cell outward; broadcast as a UI hint only. 0 when `exitRevealed == false`. Does NOT gate the win condition. Renamed from `connectedCharacters` in v2r2 — TypeScript must use `exitConnectedCount` (camelCase).
- `exitRevealed: bool` — false until the exit tile is drawn and server-placed.
- `exitCell: string?` — null until `exitRevealed` becomes true; set to `"q,r"` by the server when the exit tile is drawn.
- `outcome: HexEscapeOutcome?` — null until GameOver.
- `handSizes: { playerId → int }` — populated by projection only; not stored in authoritative server state.
- `deckSize: int` — populated by projection only; not stored in authoritative server state.

Removed from v2: `characterStartCells` is no longer a level or state field. No `HeldExitTile` obligation (exit placement is a server side-effect of drawing).

Level shape fields:

- `spawnZoneCells: string[]` — set of `"q,r"` keys on ONE side of the board defining valid cells for a player's first tile placement. `Count >= MaxPlayers` required.
- `exitZoneCells: string[]` — set of `"q,r"` keys on the OPPOSITE side of the board. RESERVED: no player tile or zombie tile may be placed here. The server places the exit tile on one of these cells when it is drawn.
- `hordeOriginCells: string[]` — set of `"q,r"` keys defining the zombie origin zone, thematically behind the spawn side so zombies chase players toward the exit. Used for horde spawns (D2b). Must be non-empty.
- No `exitCell` in level definition — the exit emerges from the deck and is placed by the server.

### AD-OB-5: D1 — Anti-turtle: contained zombies break out and multiply

A zombie is **contained** at the round boundary (before rolling) if NONE of the 6 directions yields a connection-rule-valid move — every direction is either off-grid or a closed edge on either side.

A contained zombie does BOTH in sequence:
- **(a) Break-out:** The server rotates the zombie's own tile to open at least one edge toward an in-grid neighbour. Pick the lowest direction index d (0–5) such that a neighbour cell exists in the grid at offset[d]; set the zombie tile's rotation so that it has an open edge in that direction. If no in-grid neighbour exists in any direction (isolated cell), skip the rotation.
- **(b) Spawn:** Spawn one new zombie on an adjacent in-grid tiled cell. Pick the lowest direction index d (0–5) such that an in-grid cell with a tile exists at offset[d]; spawn the new zombie there with a new stable id. If no such cell exists, skip the spawn. Co-location elimination runs immediately on spawn.

Containment is checked per-zombie in stable id order before the d6 roll phase. Zombies that are merely unable to move on a single d6 roll (normal stall) are NOT contained — full containment requires every direction to be blocked.

Rationale: sealing zombies away is counterproductive. Break-out prevents indefinite containment; spawn adds attrition pressure.

### AD-OB-6: D2 — Deck pressure: mandatory draw and escalating horde

Two mechanisms enforce continuous pressure:

**(a) Mandatory draw:** On each turn, a player MUST spend at least 1 AP on `DrawTile` before dispatching `EndTurn`, UNLESS their hand is at `HandSize` (3) OR the deck is empty. Attempting `EndTurn` without drawing (when a draw was possible) is rejected with `"You must draw a tile this turn."`. This forces the deck down, surfacing the exit tile.

**(b) Escalating horde:** At each round boundary (after all zombie d6 moves, before loss check), `HordeRatePerRound` (constant: 1) new zombie(s) spawn from `hordeOriginCells`. Deterministic pick: try each `hordeOriginCells` entry in index order; pick the first that is an in-grid cell with no zombie on it; spawn there. If all entries are zombie-occupied or unavailable, skip for this round. Co-location elimination runs immediately on horde spawn.

Both constants (`HordeRatePerRound = 1`, `HandSize = 3`) are tunable in one place.

### AD-OB-7: D3 — Directional layout and reserved exit zone

Levels define a directional layout with `spawnZoneCells` on ONE side and `exitZoneCells` on the OPPOSITE side. Exit-zone cells are RESERVED — `PlaceTile` and `PlaceZombieTile` are forbidden there. This eliminates the no-legal-cell soft-lock for exit placement (the exit always has a valid cell) and prevents players from placing the exit adjacent to their spawn.

When the exit tile is drawn, the SERVER places it deterministically:
1. Collect all `exitZoneCells` that are currently empty.
2. Select the one closest to the board centre (tie-break: lowest lexicographic `"q,r"` key).
3. Place the exit tile there; set `exitCell` and `exitRevealed = true`.

The player who drew the exit tile does NOT choose placement. The exit tile does NOT enter the player's hand. No forced-placement obligation is created. The draw costs 1 AP and satisfies the mandatory-draw obligation.

This eliminates the v2r2 player-chosen `PlaceExitTile` action and the exit-tile held-obligation (AD-OB-8b). Those are removed from this revision.

### AD-OB-8: D4 — Disconnect handling deferred (known v2 limitation)

A disconnected player's seat stalls the round — all other players must wait for it to act. This is accepted as a known v2 limitation. Implementation adds a `[Fact(Skip="v2 known limitation: disconnected seat stalls round; auto-skip / turn-timer deferred to follow-up")]` test that documents the gap. A follow-up must add auto-skip or a turn timer before public play.

See Known Limitations section and docs/owner/TODO.md.

### AD-OB-9: AP turn model — `activeSeat` and `actionPointsRemaining`

`activeSeat: int?` and `actionPointsRemaining: int` are top-level state fields. A turn is claimed by the first action from any unacted seat when `activeSeat == null`. On claiming: `activeSeat` = that seat index, `actionPointsRemaining` = `ApPoolSize`. Only `activeSeat` may act. When the turn ends (AP hits 0 or `EndTurn`): `activeSeat` → null, `actionPointsRemaining` → 0, seat added to `seatsActedThisRound`. Another seat may then claim.

Single `int` (not a per-seat map) is used for `actionPointsRemaining` because only one seat is ever active at a time. A per-seat map would add complexity with no benefit.

`ApPoolSize = 3` is a tunable constant.

### AD-OB-10: Win and loss ordering

WIN is checked after each accepted AP action in the Actions phase, but ONLY when `exitRevealed == true`. Co-location elimination (AC-v2-31a) runs BEFORE win check — a character moving onto `exitCell` while zombies are there is eliminated, not victorious.

LOSS is checked after all zombie movement and spawning completes at the round boundary (after horde spawn). LOSS is never checked during the Actions phase.

Win and loss cannot resolve in the same `Handle` call because they are checked in different phases. This ordering is invariant.

Win definition: `exitRevealed == true` AND all placed non-eliminated characters have `pos == exitCell` AND at least one placed non-eliminated character exists. Unplaced players are NOT IN PLAY and do not block win.

Loss definition: at least one character was ever placed AND all placed characters are eliminated. Unplaced characters do not block or contribute to loss.

### AD-OB-11: Co-location elimination timing

A character is eliminated IMMEDIATELY whenever it comes to share a cell with a zombie, in ALL three cases:
- (a) Player moves their character onto a zombie's cell (`MoveCharacter`).
- (b) A zombie moves onto a character's cell during ZombieMovement.
- (c) A zombie spawns on a character's cell (via `PlaceZombieTile`, D1 break-out spawn, or D2b horde spawn).

Elimination check runs before the win check in all cases. No "end of phase" batching for case (a).

### AD-OB-12: Zombie tile scaling (proportional to player count — balance TBD)

The proportion of zombie tiles in the deck scales with player count. The following table is the starting point — numbers are explicitly marked as **balance TBD by playtest** and must be tunable constants in one place (not scattered or frozen as a contract):

| Players | Zombie tiles | Total deck size | Exit tile | Non-special tiles |
|---------|-------------|-----------------|-----------|-------------------|
| 1       | 3           | 30              | 1         | 26                |
| 2       | 5           | 40              | 1         | 34                |
| 3       | 7           | 50              | 1         | 42                |
| 4       | 10          | 60              | 1         | 49                |
| 5       | 12          | 70              | 1         | 57                |
| 6       | 15          | 80              | 1         | 64                |

One exit tile is always included. Deck composition constants are tunable in one place.

### AD-OB-13: First-cut scope trims (locked for v2)

These are deliberately constrained to keep v2 shippable. Each trim leaves a field or constant hook so the follow-up is purely additive:

- No discard reshuffle — deck empties and stays empty; `discardPile` field is present.
- No multi-hex sprint — one hex per MoveCharacter; constant `MaxMoveDistance = 1`.
- No zombie-tile overwrite — zombie tiles may only target empty cells.
- Fixed hand cap 3 — constant `HandSize = 3`.
- Fixed AP pool 3 — constant `ApPoolSize = 3`.
- Horde rate 1 — constant `HordeRatePerRound = 1`.
- Ship one v2 level (the tutorial) — level loader and SetupOptions dropdown remain wired; more levels are a follow-up.
- Exactly one exit tile per game.
- Win reachability is the players' problem: nothing in the rules guarantees the placed exit tile will be pipe-connected to characters' positions. Players must route their network toward wherever the exit lands on the opposite side.
- Rotate-spam is bounded by AP cost + mandatory draw + horde pressure. No special anti-spam rule is added.

### AD-OB-14: No database changes

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
- Host-initiated skip-seat or per-seat disconnect timeout (deferred — see Known Limitations)
- Spectator mode or late-join
- Co-op undo with player consent
- Y-junction tile type
- Procedural level generation
- Fixed pre-defined exit cell in the level definition (exit always comes from the deck)
- Pre-defined character start cells (spawn zone replaces fixed per-player starts)
- Player-chosen exit tile placement (server places deterministically on exit zone — see AD-OB-7)
- Exit tile held-obligation / carry-over across turns (eliminated by D3)
- BFS connectivity as a win gate (`exitConnectedCount` is a UI hint only)

---

## Known limitations (v2)

### KL-1: Disconnected seat stalls round (deferred — D4)

If a player disconnects during their turn, no other player can act until the disconnected seat dispatches `EndTurn`. There is no auto-skip or turn timer. This is accepted as a known v2 limitation. A follow-up must add auto-skip or a per-seat turn timer before the game is opened for public play. A `[Fact(Skip=...)]` test documents the expected (but unimplemented) behaviour.

See docs/owner/TODO.md for the deferred action item.

---

## Implementation hints

### Backend (rewrite module / models / levels)

- Implement `HexEscapeModule` implementing both `IGameModule` and `IGameHandler` (AD-1), following `FThatModule.cs` as template.
- Port pure-geometry tests (OpenEdges rotation, opposite-edge, connection rule, BFS reachability) into the new test file BEFORE deleting `HexEscapeModuleTests.cs`.
- All randomness via `Random.Shared` inside `Handle`/`Apply` — no seed in state (AD-OB-2). Exit tile position within bottom 25% is determined by a single `Random.Shared.Next(minIndex, deckSize)` call at `CreateInitialState`; the resulting deck array order is the stored authoritative order.
- `HasStateProjection = true`; implement `ProjectStateForPlayer` as pure deserialize/with/reserialize (AD-OB-3).
- Author the tutorial level first — it is the fallback target. Tutorial level must define `spawnZoneCells`, `exitZoneCells`, and `hordeOriginCells`. `exitZoneCells` must be on the opposite side from `spawnZoneCells`.
- Every enum (`HexEscapePhase`, `HexEscapeOutcome`, tile type enums) must carry `[JsonConverter(typeof(JsonStringEnumConverter))]`.
- `seatsActedThisRound` as `List<int>` with distinct guard; all logic uses `.Contains()`, never index arithmetic (AD-11).
- `activeSeat: int?` and `actionPointsRemaining: int` tracked in state. Seat claiming sets `activeSeat` and resets AP. Turn-end clears both.
- Win check: after each AP action IF `exitRevealed` AND in Actions phase. Co-location elimination runs before win check on MoveCharacter. Loss check: after ZombieMovement phase (after all moves, spawns, and eliminations).
- Win check skipped entirely when `exitRevealed == false`.
- Round-boundary sequence (all in one `Handle` invocation): ZombieMovement transition → D1 containment evaluation → d6 rolls + moves → D2b horde spawn → loss check → round advance.
- D1 break-out: rotate zombie tile to open lowest-index direction toward an in-grid neighbour. D1 spawn: new zombie on lowest-index in-grid tiled neighbour. Both are deterministic — no RNG.
- D2b horde: pick lowest-index non-zombie-occupied `hordeOriginCells` entry.
- Server exit placement: when exit tile is drawn, place on closest-to-centre empty `exitZoneCells` entry (tie-break: lowest lex key). No player involvement.
- Mandatory draw: track per-turn whether `DrawTile` was executed. If `EndTurn` arrives without a draw and a draw was possible, reject.
- `exitConnectedCount` (renamed from `connectedCharacters`): recomputed after any action that changes board connectivity or character positions, when `exitRevealed`.
- `CreateInitialState` falls back to `"tutorial-01"` with ILogger WARNING when options are null or malformed.
- `CreateInitialState` throws `ArgumentException` if resolved level has empty `spawnZoneCells`.
- Phase enum no longer includes `Drawing` — remove it.
- Expose `ResolveZombieMove` as `internal static` (with `InternalsVisibleTo` for the test project) so d6 mapping and movement are unit-testable with fixed directions. Tests assert structural invariants (deck composition counts, exit-tile index range, lastZombieRolls shape) — never exact RNG outcomes.
- No `HexEscapeDbContext`; no EF migrations.

### Frontend (rewrite types.ts and Game.tsx; keep HexBoard)

- Keep `HexBoard` axial-to-pixel rendering and geometry unchanged.
- Rewrite `types.ts` to mirror the v2 state shape (AD-OB-4) in camelCase. Include `exitRevealed: boolean`, `exitCell: string | null`, `activeSeat: number | null`, `actionPointsRemaining: number`, `exitConnectedCount: number`. Remove `connectedCharacters`.
- Remove `Drawing` from the `HexEscapePhase` string union.
- Add zombie token layer and character token layer over `HexBoard`.
- Render per-player hands, deck size, round number, phase indicator, and AP counter for `activeSeat`.
- Highlight spawn zone cells until a player has placed their first tile.
- Highlight exit zone cells as reserved (distinct visual treatment).
- Show AP remaining for the active seat. Disable action buttons when AP is 0 or seat is not `activeSeat`.
- Show exit cell highlight once `exitRevealed` is true.
- Animate `lastZombieRolls` — show dice result and movement arrow per zombie.
- Result screen: `outcome === 'Escaped'` → "Escaped!"; `outcome === 'Overrun'` → "Overrun!". Never render null winner.
- Highlight forced-tile obligation: block other action buttons with tooltip when player holds zombie tile.
- Show mandatory-draw reminder when player has not yet drawn and hand is not full and deck is not empty.
- TypeScript enums mirror as PascalCase string unions; all field names camelCase.

### Tester

- Port pure-geometry tests first, before the old test file is deleted.
- New test suite covers: deck shuffle places exit tile in bottom 25%; deck composition has exactly one exit tile; catalogue validation (AC-v2-5) passes for all authored levels; DrawTile costs 1 AP; AP exhausted rejects further actions; EndTurn without draw rejected when draw was possible; EndTurn without draw allowed when hand full or deck empty; hand cap rejects draw when full; spawn-zone validation rejects first tile outside zone; exit-zone rejection for PlaceTile and PlaceZombieTile; character spawns on first tile; server exit placement sets exitRevealed and exitCell without entering hand; server exit placed in exitZoneCells; win check skipped before exitRevealed; win fires when all placed non-eliminated characters on exitCell; unplaced players do not block win; win fires immediately after server exit placement if all characters already there; zombie tile discard with no legal non-exit-zone cell; ZombieMovement runs once at round boundary; round-boundary sequence is correct (containment before roll, horde after roll); D1 break-out rotates zombie tile and spawns; D1 spawn eliminates character if present; D2b horde spawns one zombie per round from hordeOriginCells; horde skips if all origin cells occupied; AP pool resets at turn start (seat claiming); activeSeat set and cleared correctly; eliminated character not counted in win check; loss requires ALL placed characters eliminated; unplaced characters ignored for loss; projection hides other players' hands and deck; two zombies may stack on one cell; mandatory draw waived when deck empty; mandatory draw waived when hand full.
- Port unhappy-path rejection tests from v1 (occupied cell, not-on-board, invalid rotation, repeat action) updating for v2 action set.
- Disconnected seat stalls round: `[Fact(Skip="v2 known limitation: disconnected seat stalls round; auto-skip / turn-timer deferred to follow-up")]`.
- Expose `ResolveZombieMove` as `internal static` for unit tests (deterministic direction-to-movement assertions without RNG).

### DevOps

No migration and no new DbContext — no CI action required for v2.

---

## Story review

**Reviewed by:** adversarial analyst + tester
**Review date:** 2026-06-16
**Spec version reviewed:** v2 revision 2 (46 ACs, 11 ADs)
**Challenges raised:** 32 (7 blockers)
**This revision:** v3 — all 32 challenges resolved; all 7 blockers closed

### Blockers and resolution

| Blocker | Challenge | Resolution |
|---------|-----------|------------|
| B1 | Anti-turtle: sealed zombies are safe indefinitely | D1 (AD-OB-5): contained zombies break-out + spawn (ACs 32b–32c) |
| B2 | No directional constraint — exit could land adjacent to spawn | D3 (AD-OB-7): exitZoneCells reserved on opposite side; server places exit there (ACs 17–21) |
| B3 | No deck-pressure mechanism — players could stall | D2 (AD-OB-6): mandatory draw + escalating horde (ACs 10, 32e) |
| B4 (ch.9/16/27) | Win condition conflated BFS with co-location | AC-29a–d: win = physical co-location; exitConnectedCount is UI hint only |
| B5 (ch.14/26) | Exit placement by player could soft-lock or be gamed | D3 eliminates player-chosen placement entirely |
| B6 (ch.13) | Disconnect stalls round indefinitely | D4 (AD-OB-8): deferred with Skip test + TODO item |
| B7 (ch.24) | AP reset timing undefined (activeSeat not in state) | AD-OB-9: activeSeat + actionPointsRemaining both in state; seat-claiming model defined (ACs 6–9) |

### Changes summary

- **ACs restructured:** 46 → 49 (net). Removed: v2r2 ACs 15–18 (player-chosen exit placement, held-obligation, no-legal-cell carry-over — all eliminated by D3). Split: AC-28 → 29a–d; AC-31 → 32a–32f. Added: ACs for mandatory draw (10, 47), exit zone reservation (17, 39), server exit placement (18–21), containment/break-out (32b–32c), horde spawn (32e), co-location in all three cases (31a–31c), zombie stacking (31d), seat claiming (6–9), catalogue validation (5).
- **ADs added:** AD-OB-5 (D1 anti-turtle), AD-OB-6 (D2 deck pressure), AD-OB-7 (D3 directional layout), AD-OB-8 (D4 disconnect deferred), AD-OB-9 (activeSeat/AP model), AD-OB-10 (win/loss ordering — expanded), AD-OB-11 (co-location elimination timing).
- **State shape changes:** added `activeSeat`, `exitZoneCells`/`hordeOriginCells` to level shape, renamed `connectedCharacters` → `exitConnectedCount`, removed held-exit-tile obligation.
- **Known Limitations section added** (KL-1: disconnect stall deferred).

### Key edge cases to implement carefully

1. D1 containment check must run BEFORE d6 rolls — containment evaluation is on pre-roll state.
2. Co-location elimination on MoveCharacter runs BEFORE win check — moving to exitCell occupied by a zombie eliminates, does not win.
3. Server exit placement fires as a side-effect of DrawTile — exit tile never enters the player's hand; no new action type needed.
4. Mandatory draw is waived automatically when deck is empty or hand is full — not an error, just waived.
5. Unplaced characters (null pos) are ignored for BOTH win and loss — they are not IN PLAY until first tile placed.
6. Round-boundary sequence is strict: transition → containment evaluation → d6 rolls → horde spawn → loss check → round advance (all in one Handle call).
7. `exitConnectedCount` rename from `connectedCharacters` must be applied in both C# and TypeScript.

### Architect confirmation required before implementation

The new round-boundary mechanics (D1 containment break-out + spawn; D2b escalating horde) materially change the complexity and ordering of the round-boundary `Handle` invocation. The architect must confirm:
- The proposed round-boundary sequence (AC-32a–32f) is correct and complete.
- D1 break-out rotation (opening lowest-index direction toward a neighbour) is unambiguous and implementable without new state fields.
- D2b horde spawn via `hordeOriginCells` index ordering is sufficient (no need for weighted or dynamic origin selection in v2).
- `activeSeat` as a nullable int in state (not derived) is the right approach.

**Verdict:** Approved for implementation, subject to architect confirmation of round-boundary mechanics (D1/D2) before or during backend implementation begins.
