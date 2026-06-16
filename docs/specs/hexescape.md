# Spec: Hex Escape (Outbreak)

**Status:** Agreed v2 + revision-2 pending architect review
**Date:** 2026-06-16
**Authors:** analyst + architect
**Revision:** 2 — incorporates three locked owner decisions (AP turn economy, spawn-zone start, exit tile from deck). Requires focused architect re-confirmation before implementation begins.

Supersedes the v1 threat-counter design (preserved in git history). v1's hex geometry, tile model, and connection rule carry forward unchanged.

---

## Problem

Meepliton's game library is entirely competitive: every title has a winner and losers. Hex Escape v1 introduced co-op tile placement but used a passive threat counter that never manifested on the board. Players had no spatial pressure — the game was a pipe-puzzle with a countdown bolted on. Hex Escape v2 (Outbreak) replaces the counter with actual zombie tokens that move on the grid and eliminate characters, creating genuine co-op tension and making every tile placement matter.

---

## Solution

Hex Escape (Outbreak) is played on a sparse hexagonal grid identical to v1. Players collectively draw tiles from a shared deck each round by spending action points (AP); they place and rotate tiles to create open paths and move their characters toward a hidden exit. Characters representing each player start on the first tile they place, which must fall within the level's spawn zone. Zombie tokens start at spawn points defined by the level and move each round based on a d6 roll; any character sharing a cell with a zombie is eliminated. A single special exit tile is shuffled into the bottom portion of the deck; when a player draws it they must immediately place it on an empty cell, revealing the exit location. Play ends when all non-eliminated characters reach the exit cell (win) or all characters are eliminated (loss). A zombie tile drawn from the deck is also a forced placement, spawning a new zombie at that cell.

Each player's turn is governed by an **action-point (AP) pool of 3 AP** (tunable constant). On their turn a player may spend AP in any order and any mix on four atomic actions: draw a tile (1 AP), place a tile from hand (1 AP), rotate a player-placed tile (1 AP), or move their character one hex (1 AP). There is no separate automatic draw phase — drawing is an explicit AP-spending action. Unused AP do not carry over.

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

BFS runs **from the exit cell outward** over open shared edges (both sides of the edge must satisfy the connection rule above). `connectedCharacters` = the number of non-eliminated character positions reachable from the exit via this BFS. Win condition: `connectedCharacters == nonEliminatedCharacterCount` AND `nonEliminatedCharacterCount >= 1` AND `exitRevealed == true`.

---

## Acceptance criteria

### Setup

- [ ] **AC-v2-1 — Initial state:** Given a host selects a valid level and starts the game, when `CreateInitialState` is called, then: the grid is populated with pre-placed tiles; no player has a character position yet (characters are unplaced until their first tile placement in the spawn zone); every player's hand is empty; the deck is shuffled using `Random.Shared` with the exit tile placed randomly within the last 25% of the deck (see AD-OB-4); `phase` is `Actions`; `roundNumber` is 1; `zombies` contains all level-defined starting zombie tokens with stable string ids; `seatsActedThisRound` is empty; `lastZombieRolls` is empty; `exitRevealed` is `false`; `exitCell` is `null`; `actionPointsRemaining` for the active seat is `ApPoolSize` (3). `CreateInitialState` does NOT evaluate the win condition.

- [ ] **AC-v2-2 — Null options fallback:** Given `CreateInitialState` receives null or malformed options, then it silently substitutes the default level (`"tutorial-01"`), returns valid initial state, and emits a server-side WARNING log: `"HexEscape: options missing/unknown level '{id}', falling back to tutorial-01"`.

- [ ] **AC-v2-3 — Zero-survivor level rejected:** Given `CreateInitialState` is called with a level whose `spawnZoneCells` list is empty, then it throws `ArgumentException`. A catalogue-validation unit test asserts no authored level has an empty spawn zone.

- [ ] **AC-v2-4 — Pre-won level disallowed:** No authored level may start with all characters already at an exit cell. A catalogue-validation unit test asserts this for every authored level. (Because `exitRevealed` starts `false`, this check is a structural invariant only.)

### AP turn economy

- [ ] **AC-v2-5 — AP pool on turn start:** When a player's turn begins (they are the active seat and their entry into `seatsActedThisRound` has not yet happened), `actionPointsRemaining` for that seat is set to `ApPoolSize` (constant: 3). AP does not carry over between turns.

- [ ] **AC-v2-6 — AP spent per atomic action:** Each accepted atomic action — DrawTile, PlaceTile, RotateTile, MoveCharacter — costs exactly 1 AP and decrements `actionPointsRemaining`. An action dispatched when `actionPointsRemaining == 0` is rejected with `"No action points remaining."`.

- [ ] **AC-v2-7 — Turn ends when AP exhausted or player passes:** A player's turn ends (seat added to `seatsActedThisRound`, AP no longer accepted for that seat this round) when either (a) `actionPointsRemaining` reaches 0 after the last AP spend, or (b) the player dispatches `EndTurn` (which costs 0 AP and commits any remaining AP as forfeited). A player may not dispatch any further actions after their turn ends.

- [ ] **AC-v2-8 — Hand cap enforced on draw:** `DrawTile` is rejected with `"Hand is full."` when the player's hand already contains `HandSize` (constant: 3) tiles. A player cannot draw into an overfull hand regardless of AP remaining.

- [ ] **AC-v2-9 — Deck empty draw rejected:** `DrawTile` is rejected with `"The deck is empty."` when `deck` is empty. The player may spend remaining AP on other actions or EndTurn.

### Spawn zone and character start

- [ ] **AC-v2-10 — First tile must go in spawn zone:** A player's first `PlaceTile` action must target a cell in the level's `spawnZoneCells`. If the coord is not in `spawnZoneCells`, the action is rejected with `"First tile must be placed in the spawn zone."`. Subsequent placements are unrestricted.

- [ ] **AC-v2-11 — Character spawns on first placed tile:** When a player's first `PlaceTile` is accepted, their character is created at the placed tile's coord with `eliminated: false`. Before their first placement the character has no position and is not counted in win or loss checks.

- [ ] **AC-v2-12 — Spawn zone cell may be shared:** Two or more players may place their first tile on the same spawn-zone cell. This is accepted as long as the cell is empty at the time of placement. Characters may coincide at the same cell (being on the same cell is not a win; the exit cell is the only win trigger).

- [ ] **AC-v2-13 — Spawn zone full (all cells occupied):** If all `spawnZoneCells` are occupied before a player has placed their first tile, the `PlaceTile` action for that player is rejected with `"Spawn zone is full — no cell available for first placement."`. The player may spend remaining AP on DrawTile or MoveCharacter (if character already placed) or EndTurn. This is flagged as an edge case requiring architect attention (see Open Questions, revision 2).

### Exit tile reveal

- [ ] **AC-v2-14 — Exit tile in deck:** The deck contains exactly one entry with `isExitTile: true`. It is placed at a random position within the last 25% of the deck during `CreateInitialState` (see AD-OB-4 for the exact rule). Before it is drawn, `exitRevealed` is `false` and `exitCell` is `null`.

- [ ] **AC-v2-15 — Drawing the exit tile — forced placement obligation:** When a player's `DrawTile` action draws the exit tile from the deck, the tile enters the player's hand as `HeldTile { isExitTile: true, isZombieTile: false }`. This creates a forced-placement obligation. The player MUST spend their next available AP to place the exit tile (via `PlaceExitTile { coord }`) before spending AP on any other action. If the player attempts any other action while holding the exit tile, the action is rejected with `"You must place the exit tile first."`.

- [ ] **AC-v2-16 — PlaceExitTile (valid):** Given a player holding the exit tile dispatches `PlaceExitTile { coord }`, and `coord` is an empty cell (not pre-placed, not already occupied, not currently occupied by a zombie — consistent with normal placement rules), then: the cell is marked as the exit (`exitCell = coord`, `exitRevealed = true`); the tile is removed from hand; BFS is recomputed from the new exit cell; `actionPointsRemaining` is decremented by 1. Win check runs immediately.

- [ ] **AC-v2-17 — PlaceExitTile on occupied cell rejected:** Rejected with `"Cell is already occupied."`. State unchanged. The obligation remains.

- [ ] **AC-v2-18 — Exit tile no legal cell — hold obligation:** If the player holding the exit tile has no legal empty cell to place it (every board cell is occupied), the obligation is NOT discarded. The player must EndTurn. At the start of the player's next turn, the forced-placement obligation persists: they must place the exit tile before spending AP on anything else. This repeats until a legal cell is available. The exit tile is never discarded. (See Open Questions, revision 2, for architect input on this rule.)

- [ ] **AC-v2-19 — Win requires exit revealed:** The win condition is never satisfied while `exitRevealed == false`. Before the exit tile is placed, `connectedCharacters` is not computed and no win check is meaningful. (BFS from a null exit cell is not run.)

- [ ] **AC-v2-20 — Exactly one exit tile per game:** A catalogue-validation unit test asserts every authored level configuration results in exactly one `isExitTile: true` entry in the deck after `CreateInitialState`. The level definition itself does not specify an exit cell.

### Tile placement and rotation

- [ ] **AC-v2-21 — PlaceTile (valid, non-first):** Given a player whose turn is active (seat not yet in `seatsActedThisRound`) dispatches `PlaceTile { coord, tileType, rotation }`, and the player has already placed their first tile (character exists), and the coord exists in the level grid, is unoccupied, the player's hand contains that tile type with `isZombieTile: false` and `isExitTile: false`, and rotation is in 0–5, then: the cell is populated; the tile is removed from hand; BFS is recomputed (if `exitRevealed`); `actionPointsRemaining` is decremented by 1. Win check runs immediately (if `exitRevealed`).

- [ ] **AC-v2-22 — PlaceZombieTile (valid):** Given a player holding a zombie tile dispatches `PlaceZombieTile { coord }`, and coord is an EMPTY cell, and the zombie-placement obligation is active (player holds a zombie tile and must place it before other actions), then: the cell is populated with the zombie tile (fixed, non-rotatable); the tile is removed from hand; a new zombie token is spawned at coord with a stable generated id; `actionPointsRemaining` is decremented by 1. Win check runs immediately (if `exitRevealed`).

- [ ] **AC-v2-23 — PlaceZombieTile on occupied cell rejected:** Rejected with `"Cell is already occupied."`. State unchanged.

- [ ] **AC-v2-24 — PlaceZombieTile with no legal cell:** Given a player holds a zombie tile but every cell on the board is occupied, the zombie tile is appended to `discardPile` (no spawn), `actionPointsRemaining` is decremented by 1 (the forced discard still costs 1 AP). Win check runs immediately (if `exitRevealed`). (See Open Questions, revision 2, on whether forced discard should cost AP.)

- [ ] **AC-v2-25 — RotateTile (valid):** Given a player's turn is active, `actionPointsRemaining >= 1`, dispatches `RotateTile { coord, rotation }`, rotation is in 0–5, and the cell contains a player-placed non-zombie tile, then: the rotation is updated; BFS is recomputed (if `exitRevealed`); `actionPointsRemaining` is decremented by 1. Same-rotation RotateTile is accepted and costs 1 AP. Win check runs immediately (if `exitRevealed`).

- [ ] **AC-v2-26 — MoveCharacter (valid):** Given a player's turn is active, `actionPointsRemaining >= 1`, dispatches `MoveCharacter { toCoord }`, the player's character exists and is non-eliminated, and the connection rule is satisfied in both directions between the character's current cell and `toCoord`, then: the character's `pos` is updated; `actionPointsRemaining` is decremented by 1. Win check runs immediately (if `exitRevealed`). Movement is one hex per action.

- [ ] **AC-v2-27 — MoveCharacter before first tile placed rejected:** If a player has not yet placed their first tile (character has no position), `MoveCharacter` is rejected with `"Your character has not been placed yet."`.

### Win and loss

- [ ] **AC-v2-28 — Win check (after each AP action, if exit revealed):** After every accepted action that could change character positions or board connectivity, and only when `exitRevealed == true`, the server checks: if all non-eliminated characters are on `exitCell` AND at least one non-eliminated character exists AND all players have placed their first tile (or have been eliminated), then `phase` → `GameOver`, `outcome` → `Escaped`, emit `GameOverEffect(winnerId: null)`. When the win triggers, ZombieMovement is NOT run and the round boundary is NOT advanced. If `exitRevealed == false`, win check is skipped entirely.

- [ ] **AC-v2-29 — Loss unchanged:** Loss is checked after ZombieMovement completes. If ALL characters are eliminated (including any not-yet-placed characters treated as not yet in play — see Open Questions, revision 2), `phase` → `GameOver`, `outcome` → `Overrun`, emit `GameOverEffect(winnerId: null)`.

- [ ] **AC-v2-30 — Win/loss ordering invariant:** Win is checked in the Actions phase after individual AP actions. Loss is checked in the ZombieMovement phase. They cannot resolve in the same `Handle` call. This ordering is invariant.

### Round boundary and zombie movement

- [ ] **AC-v2-31 — Round boundary triggers when all seats done:** When the last seated player's turn ends (all seat indices in `seatsActedThisRound`), in the SAME `Handle` invocation: (1) phase transitions to `ZombieMovement`; (2) for each zombie in order: roll `Random.Shared.Next(1, 7)` (1–6), map die face to direction (face mod 6), check the full connection rule; if satisfied move the zombie, else it stays; store `{ zombieId, dieFace, direction, moved }` in `lastZombieRolls`; (3) any character sharing a cell with any zombie after all moves is marked `eliminated: true`; (4) loss check: if ALL characters are eliminated, end game; (5) if no loss: increment `roundNumber`, set `seatsActedThisRound` to empty, set `phase` → `Actions`, reset `actionPointsRemaining` for the seat that will move first (or initialise for all seats) to `ApPoolSize`.

- [ ] **AC-v2-32 — Zombie movement once per round:** Zombies move exactly once per round, at the round boundary. Zombie movement is NOT triggered by individual AP actions. The AP pool size does not affect zombie move frequency.

- [ ] **AC-v2-33 — Eliminated player still participates:** An eliminated player's character no longer counts toward win tracking, but that player still takes turns (spending AP to draw, place, rotate, or EndTurn). Their seat must still reach turn-end for the round boundary to advance. If they hold a forced tile (zombie or exit), the obligation persists.

### Validation (rejection cases)

- [ ] **AC-v2-34 — Action after turn ended rejected:** A player whose seat is already in `seatsActedThisRound` dispatching any action is rejected with `"It is not your turn."`. State unchanged.

- [ ] **AC-v2-35 — AP exhausted action rejected:** Any atomic action dispatched when `actionPointsRemaining == 0` is rejected with `"No action points remaining."`. The player must EndTurn.

- [ ] **AC-v2-36 — PlaceTile coord not on board:** Rejected with `"Cell is not on the board."`.

- [ ] **AC-v2-37 — PlaceTile on occupied cell:** Rejected with `"Cell is already occupied."`.

- [ ] **AC-v2-38 — PlaceTile with 0 of that type in hand:** Rejected with `"No tiles of that type remaining."`.

- [ ] **AC-v2-39 — Invalid rotation:** `PlaceTile` or `RotateTile` with rotation outside 0–5 rejected with `"Invalid rotation."`.

- [ ] **AC-v2-40 — RotateTile on empty cell:** Rejected with `"No tile to rotate."`.

- [ ] **AC-v2-41 — RotateTile on pre-placed tile:** Rejected with `"Cannot rotate a fixed tile."`.

- [ ] **AC-v2-42 — MoveCharacter along closed edge:** Rejected with `"No open path to that cell."`.

- [ ] **AC-v2-43 — MoveCharacter by eliminated character:** Rejected with `"Your character has been eliminated."`.

- [ ] **AC-v2-44 — Forced-tile action ordering:** While a player holds a zombie tile OR the exit tile, dispatching any action other than the required forced placement is rejected with the appropriate message (`"You must place your zombie tile first."` or `"You must place the exit tile first."`).

### State projection

- [ ] **AC-v2-45 — Projection hides other players' hands:** Given `HasStateProjection = true` and `ProjectStateForPlayer` is called for player P, then: P's own hand is returned in full (including any forced tile flags); every other player's hand is returned as an empty list; `handSizes: { playerId → int }` exposes each player's true tile count; `deck` is returned as an empty list; `deckSize: int` exposes the true deck count. Board, zombies, characters, phase, roundNumber, lastZombieRolls, exitRevealed, exitCell (once revealed), discard count, and `actionPointsRemaining` are all returned unmasked.

- [ ] **AC-v2-46 — Projection is pure:** `ProjectStateForPlayer` never mutates the input state.

---

## Architecture decisions

### AD-OB-1: Replace v1 in place (game id `hexescape`)

v2 rewrites the existing `hexescape` module on branch `claude/hex-pipe-zombie-coop-mtlx2m`. The game id, `SetupOptions` mechanism (ADR-012), `GameSetupOption.cs`, `IGameModule.SetupOptions`, AD-9 `room.GameOptions` transport, ADR-013 optional `ILogger`, lobby SetupOptions chrome, and the frontend `HexBoard` axial-to-pixel rendering and geometry are all KEPT without modification.

Files to DELETE/REWRITE: `HexEscapeModule.cs`, `Models/HexEscapeModels.cs`, `HexEscapeLevels.cs`, frontend `types.ts`, frontend `Game.tsx`, and `HexEscapeModuleTests.cs`.

Prerequisite before deleting `HexEscapeModuleTests.cs`: PORT the pure-geometry tests (OpenEdges rotation, opposite-edge, connection rule, BFS reachability) into the new test file. Do not lose math coverage.

### AD-OB-2: RNG via `Random.Shared` inside `Apply` — no seed in state

All randomness (deck shuffle at start including exit-tile placement; per-zombie d6 each round) uses `Random.Shared` called inside `Apply`/`Handle`, matching the SushiGo/LoveLetter precedent. The resulting rolls are stored in state as `lastZombieRolls: [{ zombieId, dieFace, direction, moved }]` for animation and audit. No RNG seed is stored in state. No change to `GameContext`.

Rationale: nothing in the platform replays `Handle` from the action log. `GameDispatcher` invokes `Handle` once; the only re-run is a post-rollback retry against the same committed state. Storing a seed buys nothing.

AP actions (draw, place, rotate, move) are all deterministic given the stored deck order and stored zombie rolls. The only RNG surfaces are (a) deck shuffle at `CreateInitialState` (stored order persists in `deck`) and (b) zombie d6 rolls at each round boundary (stored in `lastZombieRolls`). No new RNG surface is introduced by the AP economy.

### AD-OB-3: `HasStateProjection = true`; implement `ProjectStateForPlayer`

v2 flips `HasStateProjection` from `false` (v1) to `true`. `ProjectStateForPlayer` follows the LoveLetter template: own hand is returned in full; other players' hands are masked to empty lists with real counts in `handSizes`; deck is stripped to an empty list with `deckSize` exposed. Board/grid, zombies, characters, phase, roundNumber, lastZombieRolls, exitRevealed, exitCell, discard count, and `actionPointsRemaining` are all public. Projection must be pure (deserialize / `with` / reserialize — never mutate input or project off live references).

### AD-OB-4: State shape

Top-level fields:

- `characters: [{ playerId, pos?, eliminated }]` — `pos` is nullable; null until the player places their first tile in the spawn zone. `eliminated` applies to the character only.
- `players` — pure identity slots, unchanged from platform convention.
- `hands: { playerId → HeldTile[] }` — `HeldTile { tileType, rotation?, isZombieTile, isExitTile }`.
- `deck: DeckEntry[]` — `DeckEntry { tileType, isZombieTile, isExitTile }`. Present in full server-side; stripped to `[]` with `deckSize` in projection. Exactly one entry has `isExitTile: true`; it is placed during `CreateInitialState` at a random position within the last floor(deckSize * 0.25) positions (minimum index = deckSize - floor(deckSize * 0.25); if deckSize < 4, the exit tile occupies the last position). This shuffle result is stored directly — the deck array in state IS the authoritative order.
- `discardPile: DeckEntry[]` — present but inert in v2; retained as a hook for follow-up.
- `zombies: [{ id, pos }]` — stable string ids for animation continuity across rounds.
- `roundNumber: int`.
- `phase: HexEscapePhase`.
- `lastZombieRolls: [{ zombieId, dieFace, direction, moved }]` — reset each ZombieMovement run.
- `seatsActedThisRound: int[]` — distinct list, not `HashSet<int>` (clean JSON round-trip).
- `actionPointsRemaining: int` — AP remaining for the currently active seat's turn. Reset to `ApPoolSize` at turn start. May be tracked per-seat as `{ seatIndex → int }` if the architect prefers (open question — see revision-2 questions); analyst recommendation is a single int that is reset at turn boundaries.
- `connectedCharacters: int` — derived BFS count, broadcast for UI; 0 when `exitRevealed == false`.
- `exitRevealed: bool` — false until the exit tile is placed by a player.
- `exitCell: string?` — null until `exitRevealed` becomes true; set to `"q,r"` on `PlaceExitTile`.
- `outcome: HexEscapeOutcome?` — null until GameOver.

Removed from v2: `characterStartCells` is no longer a level or state field.

Level shape fields (replacing characterStartCells and exitCell):

- `spawnZoneCells: string[]` — set of `"q,r"` keys defining valid cells for a player's first tile placement. Replaces `characterStartCells`.
- No `exitCell` in level definition — the exit emerges from the deck.

Directions: int 0–5 reusing the v1 direction table. Every new enum carries `[JsonConverter(typeof(JsonStringEnumConverter))]`. The module keeps the global `JsonStringEnumConverter` in `SerializerOptions`. `types.ts` mirrors enums as PascalCase string unions; all field and action prop names in camelCase.

### AD-OB-5: Phase model (revised — AP economy, no Drawing phase)

New enum `HexEscapePhase { Actions, ZombieMovement, GameOver }`. The `Drawing` phase is REMOVED. New enum `HexEscapeOutcome { Escaped, Overrun }`.

Only the Actions phase accepts client actions. ZombieMovement is server-computed inside `Handle` at the round boundary (same invocation as the turn-ending action of the last seat). No client "advance phase" or "roll dice" action exists. Drawing is now a player-initiated `DrawTile` action costing 1 AP within the Actions phase.

### AD-OB-6: Round structure (revised — AP turn economy)

1. **Actions** — free-order (reusing `seatsActedThisRound`). Each seat gets `ApPoolSize` (3) AP at the start of their turn. On their turn a player spends AP in any order on: `DrawTile` (1 AP), `PlaceTile` (1 AP), `RotateTile` (1 AP), `MoveCharacter` (1 AP). A player holding a forced tile (zombie or exit) MUST spend their next AP on that forced placement before any other action. When AP reaches 0, or the player dispatches `EndTurn`, the turn ends (seat added to `seatsActedThisRound`).
2. **ZombieMovement** (server, at round boundary) — per zombie, roll d6 → direction (die face mod 6); move iff the zombie's edge is open AND the neighbour exists AND the neighbour's opposite edge is open; else zombie stays. After all moves, any character sharing a zombie's cell is eliminated.

The round still ends after every seat has taken its turn. Zombie movement runs once per full round at the round boundary, not per AP action.

**Forced tile cost:** Both zombie tiles and exit tiles, when drawn, create a forced-placement obligation. The forced placement costs 1 AP like a normal placement action. The player must spend that 1 AP on the forced placement before spending AP on anything else. **Edge case — 0 AP remaining when a forced tile is drawn:** This cannot occur under normal rules because `DrawTile` costs 1 AP, leaving at most `ApPoolSize - 1` AP after drawing; if the drawn tile is forced the player still has AP remaining. However, if the forced tile was carried over from a previous turn (exit-tile no-legal-cell scenario per AC-v2-18), the obligation activates at the start of the player's next turn when they have full `ApPoolSize` AP, so they always have AP available. **Analyst recommendation:** forced placement costs 1 AP; the 0-AP-carried-obligation scenario is handled by obligation activating at turn start with full AP replenishment. Architect to confirm.

### AD-OB-7: Win/loss ordering (revised)

WIN is checked after each accepted AP action in the Actions phase, but ONLY when `exitRevealed == true`. The team wins (`Escaped`, `GameOverEffect(null)`) when all non-eliminated characters are on `exitCell` AND at least one non-eliminated character exists. Before `exitRevealed`, the win check is entirely skipped (BFS from null exit is not computed).

LOSS is checked after the server-run ZombieMovement. The team loses (`Overrun`, `GameOverEffect(null)`) when ALL characters are eliminated.

Win and loss cannot resolve in the same `Handle` call because they are checked in different phases. This ordering is invariant.

### AD-OB-8: Forced zombie tile with no legal cell

If a player holds a zombie tile and every cell on the board is occupied, the tile is appended to `discardPile` with no zombie spawned. This forced discard costs 1 AP (consistent with the rule that forced placement costs 1 AP — see AD-OB-6). The player's turn continues with remaining AP. Eliminated players still take turns and must attempt to place zombie tiles; if no cell is available the same discard rule applies.

### AD-OB-8b: Exit tile with no legal cell (IMPORTANT — flagged for architect)

If a player holds the exit tile and every cell on the board is occupied, the exit tile CANNOT be discarded. Discarding the only exit tile would make the game permanently unwinnable. The obligation is held: the player EndTurns and the obligation carries over to their next turn. The exit tile remains in the player's hand. At the start of their next turn, the forced-placement obligation is the first thing they must satisfy (at cost of 1 AP). This repeats until a legal cell is available. See Open Questions (revision 2) for architect input on this rule and potential degenerate cases.

### AD-OB-9: Zombie tile scaling (balance TBD)

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

### AD-OB-10: First-cut scope trims (locked for v2)

These are deliberately constrained to keep v2 shippable. Each trim leaves a field or constant hook so the follow-up is purely additive:

- No discard reshuffle — deck empties and stays empty; `discardPile` field is present.
- No multi-hex sprint — one hex per MoveCharacter; constant `MaxMoveDistance = 1`.
- No zombie-tile overwrite — zombie tiles may only target empty cells.
- Fixed hand cap 3 — constant `HandSize = 3`.
- Fixed AP pool 3 — constant `ApPoolSize = 3`.
- Ship one v2 level (the tutorial) — level loader and SetupOptions dropdown remain wired; more levels are a follow-up.
- The tutorial level defines a `spawnZoneCells` set and does NOT define an exit cell; the exit emerges from the deck.
- Exactly one exit tile per game.
- Win reachability is the players' problem: nothing in the rules guarantees the placed exit tile will be pipe-connected to characters' positions. This is an accepted design constraint — players must route their network toward wherever the exit lands.

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

## Open questions (revision 2 — for architect)

These questions arise from the three locked owner decisions integrated in revision 2. Each includes the analyst's recommendation. The architect must address all of them before implementation begins.

**OQ-R2-1 — AP pool size and draw limits**
Is 3 AP the right pool size for the first shipped build? With 3 AP a player could draw twice and place once, or draw three times (if the hand cap allows). The hand cap is 3 tiles. A player starting with an empty hand could spend all 3 AP on DrawTile and end their turn with a full hand but no placement or movement. Is this an acceptable tempo sacrifice, or should there be a per-turn draw limit (e.g. max 1 or 2 DrawTile actions per turn)? Analyst recommendation: keep hand cap at 3 and do not impose a separate per-turn draw limit. The cost of burning all 3 AP on draws is already a meaningful sacrifice; a draw cap adds complexity without obvious benefit. Architect to confirm or propose a draw limit.

**OQ-R2-2 — Forced tile AP cost and the 0-AP edge case**
Should forced placements (zombie tile, exit tile) cost 1 AP like normal placements, or should they be free (no AP cost)? The obligation already imposes a sequencing constraint — must go first — so a free cost would make forced tiles a net-neutral burden rather than a turn disruptor. Analyst recommendation: cost 1 AP, consistent with "every action costs AP". The 0-AP-when-forced scenario is moot for same-turn obligations (drawing costs 1 AP, so a player always has AP left after drawing). For carried-over exit-tile obligations (no-legal-cell scenario) the obligation activates at turn start with full AP replenishment. Architect to confirm or change to free.

**OQ-R2-3 — Exit tile deck placement: exact rule and small-deck edge case**
The bottom 25% rule: exit tile placed at random within indices [deckSize - floor(deckSize * 0.25), deckSize - 1]. For deckSize = 30, that is indices [22, 29] — 8 positions. For deckSize = 4, indices [3, 3] — only the last slot. Is the floor rule correct? What if deckSize < 4 (unlikely given the table in AD-OB-9, but possible with a future level)? Analyst recommendation: minimum window of 1 (last slot) is acceptable as a degenerate case. If a future level has deckSize < 4 the exit is always last. Architect to confirm the formula and the small-deck degenerate case.

**OQ-R2-4 — Exit tile drawn with no legal empty cell (IMPORTANT — POTENTIAL UNWINNABLE STATE)**
If a player draws the exit tile but every board cell is occupied, the exit tile cannot be placed. Unlike the zombie tile, the exit tile must NOT be discarded — discarding it makes the game permanently unwinnable. The analyst recommendation (AD-OB-8b, AC-v2-18) is: hold the obligation across turns, EndTurn without placing, repeat until a cell is free. However this could theoretically persist forever if the board stays full indefinitely. Is there a fallback needed? Options considered: (a) hold obligation indefinitely (recommended — in practice tiles are player-placed so a cell is always freeable in theory, though not guaranteed); (b) place exit tile on a zombie-occupied cell (overriding zombie, creating complexity); (c) treat exit-tile no-legal-cell as a loss condition (harsh). Architect must rule on this. Flag as highest-priority open question.

**OQ-R2-5 — Spawn zone: can two players share a first-tile cell, and what if the zone fills up**
AC-v2-12 allows two players to place their first tile on the same spawn-zone cell as long as the cell is empty at placement time. Is this the right rule? (It means a player can get locked out if others fill all spawn-zone cells before them.) If the zone fills up before all players have placed, AC-v2-13 rejects further first-placements with an error. Is that sufficient, or should the spec add a fallback (e.g. expand the zone, or allow first placement anywhere once zone is full)? Analyst recommendation: the filled-zone lockout is an edge case that good level design prevents (spawn zones should have at least as many cells as max players). Add a catalogue-validation assertion that `spawnZoneCells.Count >= MaxPlayers`. Architect to confirm whether an in-game fallback is also needed.

**OQ-R2-6 — Win reachability: placed exit may not be pipe-connected to characters**
Nothing in the rules guarantees the exit tile, once placed, will be pipe-connected to any character via open edges. Characters must route their tile network toward wherever the exit lands. This is an intentional design constraint (AD-OB-10). The BFS win check still requires all non-eliminated characters to be on `exitCell` physically (not merely connected). Confirm: win condition is physical co-location on exit cell, not BFS connectivity. The BFS `connectedCharacters` field is a UI hint (showing how many characters could reach the exit via open paths) but is NOT the win trigger. Win trigger is `character.pos == exitCell` for all non-eliminated characters. Architect to confirm this interpretation.

**OQ-R2-7 — Zombie and AP interaction: zombies move once per round**
Confirm: zombie movement is triggered once per round at the round boundary (after all seats have taken their AP turns), not per AP action. A player spending all 3 AP on tile placements does not trigger additional zombie movement. This is stated in AD-OB-6 and AC-v2-32 but is worth explicit architect confirmation given the change from a single action/turn model.

**OQ-R2-8 — Determinism: new RNG surfaces introduced by revision 2**
The AP economy introduces no new RNG. DrawTile draws the next tile from the stored deck (deterministic given stored order). PlaceExitTile placement is player-chosen (deterministic). Zombie tile placement is player-chosen. The only RNG remains: deck shuffle at `CreateInitialState` (including exit-tile position within the bottom 25%, which is one `Random.Shared.Next(minIndex, deckSize)` call stored implicitly in the deck array order), and zombie d6 rolls per round boundary. Confirm no new RNG surface.

**OQ-R2-9 — `actionPointsRemaining` tracking: single int vs per-seat map**
The current recommendation (AD-OB-4) is to track `actionPointsRemaining` as a single `int` representing the active seat's remaining AP, reset at turn start. An alternative is a `Dictionary<int, int>` (seatIndex → remainingAP) that persists across the turn for observability. The single-int approach is simpler and sufficient since only the active seat's AP matters at any given moment. Architect to confirm single-int is acceptable, or specify the per-seat map if there is a reason to expose other seats' AP to the frontend.

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
- Fixed pre-defined exit cell in the level definition (exit always comes from the deck)
- Pre-defined character start cells (spawn zone replaces fixed per-player starts)

---

## Implementation hints

### Backend (rewrite module / models / levels)

- Implement `HexEscapeModule` implementing both `IGameModule` and `IGameHandler` (AD-1), following `FThatModule.cs` as template.
- Port pure-geometry tests (OpenEdges rotation, opposite-edge, connection rule, BFS reachability) into the new test file BEFORE deleting `HexEscapeModuleTests.cs`.
- All randomness via `Random.Shared` inside `Handle`/`Apply` — no seed in state (AD-OB-2). Exit tile position within bottom 25% is determined by a single `Random.Shared.Next(minIndex, deckSize)` call at `CreateInitialState`; the resulting deck array order is the stored authoritative order.
- `HasStateProjection = true`; implement `ProjectStateForPlayer` as pure deserialize/with/reserialize (AD-OB-3).
- Author the tutorial level first — it is the fallback target. Tutorial level must define `spawnZoneCells` (no `exitCell`, no `characterStartCells`).
- Every enum (`HexEscapePhase`, `HexEscapeOutcome`, tile type enums) must carry `[JsonConverter(typeof(JsonStringEnumConverter))]`.
- `seatsActedThisRound` as `List<int>` with distinct guard; all logic uses `.Contains()`, never index arithmetic (AD-11).
- Win check after each AP action IF `exitRevealed` BEFORE round boundary processing; loss check after ZombieMovement (AD-OB-7).
- Win check skipped entirely when `exitRevealed == false`.
- `CreateInitialState` falls back to `"tutorial-01"` with ILogger WARNING when options are null or malformed.
- `CreateInitialState` throws `ArgumentException` if resolved level has empty `spawnZoneCells`.
- `actionPointsRemaining` is reset to `ApPoolSize` at the start of each seat's turn.
- No `HexEscapeDbContext`; no EF migrations.
- Phase enum no longer includes `Drawing` — remove it from the initial implementation.

### Frontend (rewrite types.ts and Game.tsx; keep HexBoard)

- Keep `HexBoard` axial-to-pixel rendering and geometry unchanged.
- Rewrite `types.ts` to mirror the v2 state shape (AD-OB-4) in camelCase. Include `exitRevealed: boolean`, `exitCell: string | null`, `actionPointsRemaining: number`.
- Remove `Drawing` from the `HexEscapePhase` string union.
- Add zombie token layer and character token layer over `HexBoard`.
- Render per-player hands, deck size, round number, phase indicator, and AP counter for the active player.
- Highlight spawn zone cells until a player has placed their first tile.
- Show AP remaining for the active seat. Disable action buttons when AP is 0.
- Show exit cell highlight once `exitRevealed` is true.
- Show `exitCell` as a distinct tile highlight on the board once revealed.
- Animate `lastZombieRolls` — show dice result and movement arrow per zombie.
- Result screen: `outcome === 'Escaped'` → "Escaped!"; `outcome === 'Overrun'` → "Overrun!". Never render null winner.
- Highlight forced-tile obligation: block other action buttons with tooltip when player holds zombie or exit tile.
- TypeScript enums mirror as PascalCase string unions; all field names camelCase.

### Tester

- Port pure-geometry tests first, before the old test file is deleted.
- New test suite covers: deck shuffle places exit tile in bottom 25%; deck composition has exactly one exit tile; DrawTile costs 1 AP; AP exhausted rejects further actions; EndTurn ends turn with AP remaining; hand cap rejects draw when full; spawn-zone validation rejects first tile outside zone; character spawns on first tile; PlaceExitTile sets exitRevealed and exitCell; win check skipped before exitRevealed; win fires when all non-eliminated characters on exitCell; exit-tile obligation carries over turn if no legal cell; zombie tile discard with no legal cell; ZombieMovement runs once at round boundary; AP pool resets at turn start; eliminated character not counted in win check; loss requires ALL characters eliminated; projection hides other players' hands and deck.
- Port unhappy-path rejection tests from v1 (occupied cell, not-on-board, invalid rotation, repeat action) updating for v2 action set.
- Disconnected seat stalls round: `[Fact(Skip="v1 known limitation: disconnected seat stalls round; see follow-up")]`.

### DevOps

No migration and no new DbContext — no CI action required for v2.

---

## Story review

**Verdict:** Approved (v2 baseline) — revision 2 pending architect re-confirmation on 9 open questions (OQ-R2-1 through OQ-R2-9)
**Spec round:** 3 (v2 baseline) + revision 2 (owner decisions integrated, architect review required)
**Total AC count:** 46 (AC-v2-1 through AC-v2-46)
**Total AD count:** 11 new (AD-OB-1 through AD-OB-11, with AD-OB-5/6/7/8 revised + AD-OB-8b added) + 10 carried forward from v1
