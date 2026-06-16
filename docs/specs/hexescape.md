# Spec: Hex Escape (Outbreak)

**Status:** Refined v8 — zombie mobility & exit-approach fix (round-5)
**Date:** 2026-06-16
**Authors:** analyst + architect
**Revision:** 8 — round-5 playtest zombie mobility & exit-approach fix. Applies 3 owner-approved deltas (D1–D3): zombie-hosting and horde-origin pre-placed tiles changed from Straight to Cross r0 so route zombies cannot be bypassed by adjacent-row detours — trade-off documented: D1 break-out will rarely fire on these cells in normal play (D1); exit-approach requirement strengthened — AC-v2-5 now validates ≥2 approaches against the SINGLE DETERMINISTIC exit cell (centroid-closest empty exit-zone cell the server will actually pick) and its actual Cross r0 edge set; tutorial-01 exit zone set back from the east wall to {(3,-1),(3,0),(3,1)} so q=4 column is normal interior and approach paths are genuinely usable; prior "wall-flush exit zone" disallowed (D2); latent deck-spacing gate corrected — AC-v2-1b minSlotsRequired changed from zombieCount+(zombieCount-1)×ZombieTileMinSpacing to zombieCount+(zombieCount-1)×(ZombieTileMinSpacing+1) to match the loop's actual advancement step; AD-OB-12 table gains a "minSlotsRequired (corrected)" column (D3). AC count: 82 (unchanged; D1 and D2 update existing AC text; D3 tightens the gate formula within AC-v2-1b — no new AC IDs added). AD count: 14 numbered ADs plus AD-OB-12b design note (AD-OB-12, AD-OB-12b, AD-OB-13 updated in place). Band math re-verified: corrected spacing gate shows 1p and 2p satisfy guaranteed spacing (10≥9, 18≥17); 3p–6p do not (23<25, 31<37, 38<45, 45<57) — these counts fall back to uniform random and emit WARNING; all 6 counts pass size constraints (a)–(e); spacing guarantee is best-effort.

Supersedes the v1 threat-counter design (preserved in git history). v1's hex geometry, tile model, and connection rule carry forward unchanged. Supersedes v2 revisions 2–4.

---

## Problem

Meepliton's game library is entirely competitive: every title has a winner and losers. Hex Escape v1 introduced co-op tile placement but used a passive threat counter that never manifested on the board. Players had no spatial pressure — the game was a pipe-puzzle with a countdown bolted on. Hex Escape v2 (Outbreak) replaces the counter with actual zombie tokens that move on the grid and eliminate characters, creating genuine co-op tension and making every tile placement matter.

---

## Solution

Hex Escape (Outbreak) is played on a sparse hexagonal grid identical to v1. Players collectively draw tiles from a shared deck each round by spending action points (AP); they place and rotate tiles to create open paths and move their characters toward a hidden exit. Characters representing each player start on the first tile they place, which must fall within the level's spawn zone on one side of the board. The exit zone is on the opposite side and is reserved — no normal tile may be placed there. Zombie tokens start at spawn points defined by the level and move each round based on a d6 roll; any character sharing a cell with a zombie is eliminated immediately. A single special exit tile is shuffled into the bottom portion of the deck (band width tunable per player count); when a player draws it, the server immediately places it deterministically on an exit-zone cell — the player does not choose where. Play ends when all placed, non-eliminated characters physically occupy the exit cell (win) or all characters who were ever placed are eliminated (loss). A zombie tile drawn from the deck creates a forced-placement obligation (highest priority on that turn), spawning a new zombie at a player-chosen empty in-grid tiled cell not in the exit zone.

Each player's turn is governed by an **action-point (AP) pool of `ApPoolSize` AP** (tunable per player count; see Tunable Constants). On their turn a player may spend AP in any order and any mix on five atomic actions: draw a tile (1 AP), place a tile from hand (1 AP), place a zombie tile (1 AP), rotate a player-placed tile (1 AP), or move their character one hex (1 AP). A player must spend at least `MinActionsPerTurn` AP on real actions before they may end their turn. Unused AP do not carry over.

To prevent turtling (sealing zombies away), a zombie that is fully contained — no connection-rule-valid move in any direction — both rotates its own tile to break out and spawns an additional zombie on an adjacent in-grid tiled cell. To maintain pressure as the deck shrinks, one additional zombie spawns from a level-defined horde origin each round boundary. Threat scales by player count via per-count constants (balance TBD by playtest).

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

## Tunable constants

All balance values are defined in one place (e.g., a static `HexEscapeConstants` class or equivalent). Implementations must not scatter these values. All are marked as balance-TBD-by-playtest except where noted as structurally fixed.

| Constant | Type | Default / Table | Notes |
|---|---|---|---|
| `MinActionsPerTurn` | `int` | 2 | Minimum qualifying actions before EndTurn is legal (DD1, F1). Balance TBD. |
| `HandSize` | `int` | 3 | Maximum tiles in hand. Structurally fixed for v2. |
| `MaxMoveDistance` | `int` | 1 | Hexes per MoveCharacter action. Structurally fixed for v2. |
| `MaxZombies` | `int` | 200 | JSONB growth safety cap. Structurally fixed for v2. |
| `StartingHandSize` | `int` | 1 | Non-zombie, non-exit tiles dealt at CreateInitialState (DD3, D2). Reduced from 2 — safe opening was too generous at 2 tiles with SafeOpeningFraction=0.30. Balance TBD. |
| `SafeOpeningFraction` | `double` | 0.20 | Top fraction of raw pool guaranteed zombie/exit-free (DD3, F2, D2). Reduced from 0.30 — combined with StartingHandSize=1 this tightens the opening without removing the structural safety guarantee. Balance TBD. |
| `ApPoolSize` | `int[]` per count | `[5, 4, 4, 4, 4, 4]` (indices 1–6) | AP granted per turn; higher for solo (DD2, F7, D2). Raised 4–6p from 3 to 4 so every player count has ≥2 discretionary AP above MinActionsPerTurn=2; 6p had almost no per-turn choice at AP=3. Balance TBD by playtest. |
| `HordeRatePerRound` | `int[]` per count | `[1, 1, 1, 2, 2, 2]` (indices 1–6) | Zombies spawned per round boundary; higher for high counts (DD2). Balance TBD by playtest. |
| `ExitBandFraction` | `double[]` per count | `[0.50, 0.40, 0.40, 0.35, 0.32, 0.30]` (indices 1–6) | Exit tile placed randomly in the last X fraction of the deck (post-deal); higher fraction = earlier exit for low counts (DD2, F7, D2). Raised mid counts (2p: 0.35→0.40; 3p: 0.30→0.40; 4p: 0.28→0.35; 5p: 0.26→0.32; 6p: 0.25→0.30) so the exit surfaces earlier — a 3-player game was unwinnable because the team was overrun before the exit appeared. Balance TBD by playtest. |
| `ZombieTileMinSpacing` | `int` | 2 | Minimum deck-position gap between any two zombie tiles in the middle band during construction (D2, AC-v2-1b). Prevents an uncontrollable difficulty cliff from clustered zombie draws. Best-effort if the band is too small to honour the gap. Balance TBD. |

All per-count arrays are indexed by player count (1-based index = player count). For a 3-player game, `ApPoolSize[3]`, `HordeRatePerRound[3]`, `ExitBandFraction[3]`. All balance values are marked TBD by playtest and must be adjusted based on actual play experience.

---

## Acceptance criteria

### Setup

- [ ] **AC-v2-1 — Initial state:** Given a host selects a valid level and starts the game, when `CreateInitialState` is called, then: the grid is populated with pre-placed level tiles; each player is assigned a reserved spawn cell (deterministic: seat index maps to `spawnZoneCells[seatIndex]`); each player is dealt `StartingHandSize` tiles from the top of the deck into their starting hand — these tiles are drawn before shuffling zombie tiles in (the top `SafeOpeningFraction` of the deck is guaranteed free of zombie and exit tiles; see AC-v2-1b); no player has a character position yet (characters are unplaced until their first tile placement in the spawn zone); `phase` is `Actions`; `roundNumber` is 1; `zombies` contains all level-defined starting zombie tokens with stable string ids; `seatsActedThisRound` is empty; `lastZombieRolls` is empty; `exitRevealed` is `false`; `exitCell` is `null`; `activeSeat` is `null`; `actionPointsRemaining` is 0 (no seat is active yet). `CreateInitialState` does NOT evaluate the win condition.

- [ ] **AC-v2-1b — Deck construction with safe opening and exit band (DD3, DD2, F2):** The deck is constructed using two distinct quantities to avoid ambiguity between pre-deal and post-deal sizes:

  - `postDealSize` = the value from the AD-OB-12 scaling table for this player count (e.g. 30 for solo, 80 for 6-player). This is the size of `deck[]` stored in state AFTER starting hands are dealt.
  - `rawPoolSize` = `postDealSize + (StartingHandSize × playerCount)` — total tiles assembled before dealing.
  - `safeCount` = `floor(rawPoolSize × SafeOpeningFraction)` — the number of safe-opening slots in the raw pool (guaranteed free of zombie and exit tiles).

  Construction steps: (1) assemble all non-zombie, non-exit tiles into a normal pool; (2) fill the top `safeCount` slots of the raw pool with tiles drawn from the normal pool; (3) deal `StartingHandSize` tiles per player from the top `safeCount` slots of the raw pool — these tiles are removed from the raw pool and placed into each player's starting hand; after dealing, `(safeCount − StartingHandSize × playerCount)` safe-opening slots remain at the top of the deck; (4) compute `exitBandStart = postDealSize − floor(postDealSize × ExitBandFraction[playerCount])`; place the exit tile at a uniformly random position in `[exitBandStart, postDealSize − 1]` using `Random.Shared`; (5) distribute zombie tiles in the middle band `[(safeCount − StartingHandSize × playerCount), exitBandStart − 1]` with a MINIMUM GAP of `ZombieTileMinSpacing` deck positions between any two zombie tiles (i.e. each pair of consecutive zombie tiles must be at least `ZombieTileMinSpacing + 1` positions apart, meaning ≥ `ZombieTileMinSpacing` empty slots between them) — compute the band width, distribute zombie tiles at computed intervals within the band (divide band evenly by zombie count, then offset each position by a small uniformly random jitter within the interval using `Random.Shared`, clamped so no two tiles are within `ZombieTileMinSpacing` positions of each other); the construction gate MUST use `minSlotsRequired = zombieCount + (zombieCount − 1) × (ZombieTileMinSpacing + 1)` to determine whether the band is large enough to honour the gap — this matches the placement loop's actual advancement step (each zombie advances by `ZombieTileMinSpacing + 1` positions from the previous); if `bandWidth < minSlotsRequired`, fall back to uniform random distribution without gap enforcement and emit a server-side WARNING log `"HexEscape: zombie tile spacing constraint could not be satisfied for {playerCount}p — band too small"`; (6) fill remaining slots with normal tiles from the pool. The resulting `deck[]` of length `postDealSize` is the stored authoritative order.

  **Invariant after dealing:** the top `(safeCount − StartingHandSize × playerCount)` entries of `deck[]` are guaranteed to contain no zombie or exit tiles. Zombie tiles in the middle band are separated by at least `ZombieTileMinSpacing` positions from each other (best-effort; see step 5). Deck composition totals (post-deal) match the per-count scaling table (AD-OB-12).

- [ ] **AC-v2-2 — Null options fallback:** Given `CreateInitialState` receives null or malformed options, then it silently substitutes the default level (`"tutorial-01"`), returns valid initial state, and emits a server-side WARNING log: `"HexEscape: options missing/unknown level '{id}', falling back to tutorial-01"`.

- [ ] **AC-v2-3 — Zero-survivor level rejected:** Given `CreateInitialState` is called with a level whose `spawnZoneCells` list is empty, then it throws `ArgumentException`. A catalogue-validation unit test asserts no authored level has an empty spawn zone.

- [ ] **AC-v2-4 — Pre-won level disallowed:** No authored level may start with `exitRevealed == true`. A catalogue-validation unit test asserts `exitRevealed` starts `false` for every authored level.

- [ ] **AC-v2-5 — Catalogue completeness (F3, D1, D2):** A catalogue-validation unit test asserts that for every authored level: `spawnZoneCells.Count >= MaxPlayers` (6); `exitZoneCells` is non-empty; `hordeOriginCells` is non-empty; the deck produced by `CreateInitialState` contains exactly one `isExitTile: true` entry; deck composition matches the scaling table (AD-OB-12); the level is structurally solvable with rotation-aware exit connectivity (see below); `hordeOriginCells ∩ exitZoneCells = ∅` (no horde origin is an exit-zone cell); `spawnZoneCells ∩ exitZoneCells = ∅` (no spawn-zone cell is an exit-zone cell); `hordeOriginCells ∩ spawnZoneCells = ∅` (horde origin is adjacent to but NOT within the spawn zone — H7); `spawnZoneCells` has no pre-placed level tiles (H9); `exitZoneCells` has no pre-placed level tiles (H9); every starting zombie position defined by the level has a pre-placed tile (C4, mirrors AD-OB-4 — without this, horde spawns targeting those cells will silently fail); every `hordeOriginCell` has a pre-placed level tile (mirrors AD-OB-4 — without it horde spawns silently fail); `ApPoolSize[n] >= MinActionsPerTurn` for all player counts n (invariant: AP exhaustion always implies the minimum qualifying-actions threshold was satisfiable); the exit-zone connectivity check is rotation-aware: assert that the exit tile being a `cross r0` (edges {0,1,2,3}) means at least one in-grid, non-exit-zone cell adjacent to the deterministic exit cell has a valid-open-edge connection to the exit at cross r0; **the ≥2 exit-approach criterion MUST be validated against the SINGLE DETERMINISTIC exit cell** — the centroid-closest empty exit-zone cell the server will actually place the exit tile on at runtime (computed using the same algorithm as AC-v2-19: lowest axial distance to board centroid; tie-break lowest q then lowest r) — NOT against every exit-zone cell independently. Define the criterion precisely: there must exist ≥2 distinct in-grid, non-exit-zone cells C, each satisfying ALL of: (a) C is adjacent to the deterministic exit cell E in a direction d where the exit tile (Cross r0, open edges {E(0), NE(1), N(2), W(3)}) has an open edge; (b) C can open its edge in direction (d+3)%6 (a player can rotate a normal tile on C to connect back to E); AND (c) C is reachable from the spawn zone through the playable interior (not isolated behind or inside the exit zone). The catalogue-validation test must compute the deterministic exit cell for every authored level and verify ≥2 such cells C exist. **Disallowed layout:** an exit zone that occupies a full edge column (wall-flush) with no in-grid non-exit-zone cells beyond it eliminates reachable interior approaches and is forbidden. The tutorial level satisfies all of these. Starting zombies not adjacent to spawn cells (D1 level-design intent). Every zombie-hosting cell and every `hordeOriginCell` has a pre-placed Cross r0 tile (D1 — see Tutorial-01 level-design intent).

- [ ] **AC-v2-5b — Reserved spawn cell assignment (H10):** At `CreateInitialState`, each player (seat index 0 through players.Count-1) is assigned `reservedSpawnCell = spawnZoneCells[seatIndex]`. This assignment is stored in state. A player's first `PlaceTile` must target their assigned spawn cell (see AC-v2-13). Reserved spawn cells cannot be consumed by other players' tile placements — non-first placements are forbidden from targeting a cell that is another player's reserved spawn cell and that player has not yet placed (see AC-v2-13b). This eliminates the spawn-zone-full permanent-unplaced softlock.

### AP turn model and seat claiming

- [ ] **AC-v2-6 — Seat claiming (F1):** A turn is claimed when the first action from an unacted, non-active seat is received. On claiming: `activeSeat` is set to that seat index; `actionPointsRemaining` is reset to `ApPoolSize[playerCount]`; `qualifyingActionsThisTurn` counter is reset to 0. Only `activeSeat` may take actions while a seat is active. Any action from a seat that is not `activeSeat` (and not claiming because `activeSeat` is already occupied) is rejected with `"It is not your turn."`.

- [ ] **AC-v2-7 — AP spent per atomic action (F1):** Each accepted atomic action — DrawTile, PlaceTile, PlaceZombieTile, RotateTile, MoveCharacter — costs exactly 1 AP and decrements `actionPointsRemaining`. The **qualifying actions** (DrawTile, PlaceTile, PlaceZombieTile, MoveCharacter) additionally increment `qualifyingActionsThisTurn`. RotateTile costs 1 AP but does NOT increment `qualifyingActionsThisTurn` — it is always permitted (subject to AP) but never counts toward the minimum and never blocks EndTurn. An action dispatched by `activeSeat` when `actionPointsRemaining == 0` is rejected with `"No action points remaining."`.

- [ ] **AC-v2-8 — Turn ends — AP exhausted (F1):** When `actionPointsRemaining` reaches 0 after the last AP spend: the seat is added to `seatsActedThisRound`; `activeSeat` → `null`; `actionPointsRemaining` → 0; `qualifyingActionsThisTurn` → 0. Another unacted seat may now claim.

- [ ] **AC-v2-8b — DrawTile-as-last-AP atomic resolution (MF-1, F6, D4):** When a `DrawTile` action spends the player's LAST action point (i.e., `actionPointsRemaining` would reach 0 after this draw) AND the drawn tile is a zombie tile (or any tile that would otherwise create a forced-placement obligation), the forced obligation is resolved ATOMICALLY within the same `Handle` invocation — there is NO state in which `actionPointsRemaining == 0` while a forced zombie tile is held. The server resolves the obligation immediately using the same logic as `PlaceZombieTile`/forced-discard with the following target-selection rule (D4): among all legal candidate cells (in-grid, tiled, non-exit-zone, not zombie-occupied), PREFER cells that have NO character on them; only fall back to a character-occupied cell if EVERY candidate cell is character-occupied. The deterministic tiebreak (lowest q; for equal q, lowest r — H6 numeric ordering) applies WITHIN the preferred (character-free) subset first; if all candidates are character-occupied the tiebreak applies across all candidates. If at least one valid candidate exists, place the zombie tile on the selected cell and spawn a zombie at that coord (co-location elimination check runs immediately per AC-v2-31c); if no valid candidate exists, append the zombie tile to `discardPile`. This preference eliminates unavoidable, deterministic self-elimination of a teammate caused by MF-1 resolution landing on an occupied cell when character-free alternatives existed. (Player-chosen `PlaceZombieTile` is unchanged — the player already selects the target.) The atomic forced placement (or discard) increments `qualifyingActionsThisTurn` by 1 (it is a qualifying action equivalent to PlaceZombieTile) and does NOT decrement `actionPointsRemaining` below 0 (AP is already 0 at point of resolution). After this atomic resolution, the minimum-actions obligation is considered satisfied in all cases — no further check is performed. The turn ends normally: the seat is added to `seatsActedThisRound`; `activeSeat` → `null`; `actionPointsRemaining` → 0; `qualifyingActionsThisTurn` → 0. There is NO carry-over of a forced obligation to a later turn, to the next round boundary, or to any subsequent `Handle` call.

- [ ] **AC-v2-9 — Turn ends — EndTurn (F1, F5):** When `activeSeat` dispatches `EndTurn` (0 AP cost), EndTurn is subject to two checks in this order before it is accepted: (1) **Forced zombie tile check (highest priority):** if the player currently holds a zombie tile (forced placement obligation active), EndTurn is rejected with `"You must place your zombie tile first."` — this takes precedence over the qualifying-actions check regardless of `qualifyingActionsThisTurn`; (2) **Minimum qualifying-actions check (AC-v2-10):** see AC-v2-10. If both checks pass: remaining AP are forfeited; the seat is added to `seatsActedThisRound`; `activeSeat` → `null`; `actionPointsRemaining` → 0; `qualifyingActionsThisTurn` → 0. Another unacted seat may now claim.

- [ ] **AC-v2-10 — Minimum qualifying actions per turn (DD1, F1):** On each turn, a player must take at least `min(MinActionsPerTurn, numberOfQualifyingActionsAvailableThisTurn)` **qualifying actions** (DrawTile, PlaceTile, PlaceZombieTile, MoveCharacter) before `EndTurn` is legal. RotateTile is always allowed (costs AP) but does NOT count as a qualifying action and never blocks EndTurn.

  The escape hatch is computed as follows at the moment `EndTurn` is received (after the forced-zombie-tile check in AC-v2-9): count `numberOfQualifyingActionsAvailableThisTurn` = the number of distinct qualifying action types currently available to the player (a qualifying action is "available" if it would not be rejected on the next dispatch). Then: the required minimum = `min(MinActionsPerTurn, numberOfQualifyingActionsAvailableThisTurn)`. If `qualifyingActionsThisTurn < required minimum`, reject with `"You must take at least {MinActionsPerTurn} actions this turn."`.

  **Short-circuit implementation guidance:** if the deck is non-empty OR the player's hand is non-full, then DrawTile is available (a qualifying action), so `numberOfQualifyingActionsAvailableThisTurn >= 1` and the minimum is at least 1. Only enumerate further qualifying actions when both the deck is empty AND the hand is full.

  **Consequence:** if 0 qualifying actions are available, EndTurn is accepted immediately (player can do nothing meaningful). If exactly 1 is available, the player must take that 1. If 2 or more are available, the player must take at least `MinActionsPerTurn` (= 2).

  **Eliminated-player busy-work eliminated:** an eliminated player whose deck is empty, hand is full, and has no legal PlaceTile or MoveCharacter actions available has 0 qualifying actions available → EndTurn accepted immediately.

  **Rotate-spam does not defer EndTurn:** a player who has taken 0 qualifying actions but used all AP on RotateTile has `qualifyingActionsThisTurn = 0`; if qualifying actions were available this turn, EndTurn is rejected until they take them (or AP runs out, which auto-ends the turn per AC-v2-8).

  **Unplaced player cannot escape:** an unplaced player with a full hand always has PlaceTile available (their reserved spawn cell), so `numberOfQualifyingActionsAvailableThisTurn >= 1` and the minimum is never 0 — they must place before ending their turn.

- [ ] **AC-v2-11 — Hand cap enforced on draw:** `DrawTile` is rejected with `"Hand is full."` when the player's hand already contains `HandSize` (3) tiles.

- [ ] **AC-v2-12 — Deck empty draw rejected:** `DrawTile` is rejected with `"The deck is empty."` when `deck` is empty.

### Spawn zone, reserved cells, and character start

- [ ] **AC-v2-13 — First tile must go in reserved spawn cell:** A player's first `PlaceTile` action must target their assigned `reservedSpawnCell`. If the coord is not their assigned cell, the action is rejected with `"First tile must be placed in your assigned spawn cell."`. Subsequent placements are unrestricted (except exit zone and other players' reserved cells per AC-v2-13b).

- [ ] **AC-v2-13b — Reserved spawn cells protected until used:** A non-first `PlaceTile` targeting another player's `reservedSpawnCell` — where that player has not yet placed their first tile — is rejected with `"That cell is reserved for another player's spawn."`. Once a player has placed their first tile (their reserved cell is in use), their reserved cell becomes a normal board cell freely placeable by anyone.

- [ ] **AC-v2-14 — Character spawns on first placed tile:** When a player's first `PlaceTile` is accepted, their character is created at the placed tile's coord with `eliminated: false`. Before their first placement the character has no position and is not counted in win or loss checks — unplaced characters are treated as NOT IN PLAY.

- [ ] **AC-v2-15 — Spawn zone cell occupancy for simultaneous first placements:** Two or more players place on their own assigned reserved cells; these are distinct cells by construction (each seat maps to a distinct spawnZoneCells entry). Characters at the same cell are allowed if different players somehow end up there by later movement; co-location of characters is not itself a win trigger.

- [ ] **AC-v2-16 — Spawn-zone-full scenario eliminated:** Because each player has a reserved spawn cell (AC-v2-5b, AC-v2-13), the spawn-zone-full rejection from earlier revisions is eliminated. The failure mode of a permanently-unplaced player due to a full spawn zone cannot occur. This AC documents the closure of that finding.

### Exit zone and server exit placement

- [ ] **AC-v2-17 — Exit zone is reserved:** `PlaceTile` and `PlaceZombieTile` targeting a cell in `exitZoneCells` are rejected with `"Cannot place tiles in the exit zone."`. This applies to all players and all tile types.

- [ ] **AC-v2-18 — Exit tile in deck:** The deck contains exactly one entry with `isExitTile: true`. Its tileType is `cross` at rotation 0 (all six edges open, reachable from any direction — C3). Before it is drawn, `exitRevealed` is `false` and `exitCell` is `null`. The exit tile is placed at a random position within the last `ExitBandFraction[playerCount]` of the deck during deck construction (AC-v2-1b).

- [ ] **AC-v2-19 — Server places exit tile deterministically:** When a player's `DrawTile` action draws the exit tile from the deck, the server immediately and deterministically places it on an exit-zone cell. Placement algorithm: (1) collect all exit-zone cells currently empty (no tile placed there yet); (2) select the empty exit-zone cell whose axial distance to the board centre is smallest — board centre = axial centroid of all in-grid cells; tie-break: lowest q, then for equal q, lowest r (H6 numeric ordering); (3) place the exit tile there as `tileType = cross, rotation = 0, fixed = true` (non-rotatable by players); (4) set `exitCell` to that coord and `exitRevealed` to `true`; (5) remove the exit tile from the deck draw result (it does not enter the player's hand). The player's hand is unchanged; no forced-placement obligation is created. The draw costs 1 AP (normal draw cost) and increments `qualifyingActionsThisTurn` (DrawTile is a qualifying action).

- [ ] **AC-v2-20 — Win check after server exit placement:** Immediately after the server places the exit tile (AC-v2-19), win check runs. If all non-eliminated, placed characters are already on `exitCell`, win fires immediately.

- [ ] **AC-v2-21 — exitRevealed invariant:** `exitRevealed` starts `false`. It becomes `true` at the moment the exit tile is drawn and server-placed. It never becomes `false` again.

### Tile placement and rotation

- [ ] **AC-v2-22 — PlaceTile valid:** Given `activeSeat` dispatches `PlaceTile { coord, tileType, rotation }`, the player has already placed their first tile (character exists), the coord exists in the level grid and is not in `exitZoneCells` and is not another player's reserved-but-unused spawn cell, the cell is unoccupied (has no tile — zombie tokens do NOT block tile placement; H6/M), the player's hand contains that tile type with `isZombieTile: false` and `isExitTile: false`, and rotation is in 0–5, then: the cell is populated; the tile is removed from hand; `exitConnectedCount` is recomputed (if `exitRevealed`); `actionPointsRemaining` decremented by 1; `qualifyingActionsThisTurn` incremented by 1 (PlaceTile is a qualifying action). Win check runs immediately (if `exitRevealed`).

- [ ] **AC-v2-23 — PlaceZombieTile valid:** Given `activeSeat` holds a zombie tile and dispatches `PlaceZombieTile { coord }`, the coord is an in-grid cell WITH A PLACED TILE (not tile-less), NOT in `exitZoneCells`, and NOT currently zombie-occupied (C4 unified spawn rule), and the forced zombie-tile obligation is active (player must place zombie tile before other non-forced actions), then: the cell is populated with the zombie tile; the tile is removed from hand; a new zombie token is spawned at coord with a stable generated id; co-location elimination check runs (if any character is at coord, eliminate it); `actionPointsRemaining` decremented by 1; `qualifyingActionsThisTurn` incremented by 1 (PlaceZombieTile is a qualifying action). Win check runs immediately (if `exitRevealed`). Note: the zombie tile has a normal `tileType` drawn from the standard set, placed at rotation 0, fixed (non-rotatable by players — but rotatable by D1 break-out, see AD-OB-5). The zombie tile placement obligation takes precedence over all other actions on that player's turn and counts toward the minimum qualifying-actions requirement.

- [ ] **AC-v2-24 — PlaceZombieTile on occupied cell rejected:** Rejected with `"Cell is already occupied."`. State unchanged. Obligation remains.

- [ ] **AC-v2-25 — PlaceZombieTile with no legal in-grid tiled cell:** Given a player holds a zombie tile but every in-grid tiled non-exit-zone cell is either zombie-occupied or otherwise unavailable, the zombie tile is appended to `discardPile` (no spawn); `actionPointsRemaining` decremented by 1; `qualifyingActionsThisTurn` incremented by 1 (PlaceZombieTile/forced-discard is a qualifying action). The forced discard costs 1 AP. Win check runs immediately (if `exitRevealed`).

- [ ] **AC-v2-26 — RotateTile valid:** Given `activeSeat` dispatches `RotateTile { coord, rotation }`, rotation is in 0–5, the cell contains a player-placed non-zombie tile AND the tile is not a pre-placed level tile AND not the exit tile (those are fixed against player rotation), then: the rotation is updated; `exitConnectedCount` recomputed (if `exitRevealed`); `actionPointsRemaining` decremented by 1. `qualifyingActionsThisTurn` is NOT incremented (RotateTile is not a qualifying action — F1). Same-rotation RotateTile is accepted and costs 1 AP. Win check runs immediately (if `exitRevealed`). The active player may rotate ANY player-placed non-zombie, non-level, non-exit tile regardless of who originally placed it (co-op shared board).

- [ ] **AC-v2-27 — MoveCharacter valid:** Given `activeSeat` dispatches `MoveCharacter { toCoord }`, the player's character exists, is non-eliminated, and the connection rule is satisfied in both directions between the character's current cell and `toCoord`, then: character `pos` is updated; `actionPointsRemaining` decremented by 1; `qualifyingActionsThisTurn` incremented by 1 (MoveCharacter is a qualifying action). Co-location elimination check runs BEFORE win check — if `toCoord` contains a zombie, the character is eliminated and win check is NOT run for that character. Win check runs immediately after (if `exitRevealed`).

- [ ] **AC-v2-28 — MoveCharacter before first tile placed rejected:** If a player has not yet placed their first tile (character has no position), `MoveCharacter` is rejected with `"Your character has not been placed yet."`.

### Win condition

- [ ] **AC-v2-29a — Win condition — definition:** Win = `exitRevealed == true` AND at least one placed non-eliminated character exists AND ALL placed non-eliminated characters have `pos == exitCell`. Physical co-location on `exitCell` is the sole win trigger. BFS connectivity (`exitConnectedCount`) does NOT gate the win.

- [ ] **AC-v2-29b — Unplaced players do not block win:** A player who has never placed their first tile (character `pos` is null) is NOT IN PLAY and is ignored for win evaluation. They neither satisfy nor block the win condition.

- [ ] **AC-v2-29c — Win fires — effect (SF-4):** On win: `phase` → `GameOver`; `outcome` → `Escaped`; emit `GameOverEffect(winnerId: null)`; RETURN immediately from `Handle`. ZombieMovement is NOT run; round boundary is NOT advanced. The handler MUST short-circuit on win — it must NOT fall through to the round-boundary sequence or the zombie cascade. (Note: v1 `HexEscapeModule.FinishAction` checked win and then fell through to increment the threat counter — v2 must not repeat this bug.)

- [ ] **AC-v2-29d — Win skipped before exit revealed:** While `exitRevealed == false`, the win check is skipped entirely.

### Loss condition

- [ ] **AC-v2-30a — Loss condition — definition:** Loss = at least one character was ever placed AND ALL placed characters are `eliminated: true`. Unplaced characters (null `pos`) are ignored for loss evaluation.

- [ ] **AC-v2-30b — Loss fires — effect:** On loss: `phase` → `GameOver`; `outcome` → `Overrun`; emit `GameOverEffect(winnerId: null)`.

- [ ] **AC-v2-30c — Loss check timing:** Loss is checked AFTER zombie movement completes (end of the ZombieMovement phase, after all zombie moves and all resulting eliminations including horde spawn eliminations). Loss is NOT checked during the Actions phase.

### Co-location elimination

- [ ] **AC-v2-31a — Elimination on player move:** When a player moves their character to `toCoord` via `MoveCharacter` and `toCoord` is occupied by one or more zombies, the character is eliminated IMMEDIATELY. Elimination occurs BEFORE the win check. A player moving to `exitCell` while zombies are there is eliminated and does NOT win.

- [ ] **AC-v2-31b — Elimination on zombie move:** When a zombie moves to a cell occupied by one or more characters during ZombieMovement, each such character is eliminated immediately after that zombie's move resolves.

- [ ] **AC-v2-31c — Elimination on zombie spawn (MF-3):** When a zombie spawns on a cell (via `PlaceZombieTile`, break-out spawn from D1/AC-v2-32c, or horde spawn from D2b/AC-v2-32e) that is occupied by one or more characters, each character on that cell is eliminated immediately at the moment of spawn. No zombie may ever be spawned onto an exit-zone cell: `PlaceZombieTile` is forbidden there (AC-v2-17); D1 break-out spawn skips exit-zone candidates (AC-v2-32c); D2b horde spawn skips exit-zone candidates (AC-v2-32e).

- [ ] **AC-v2-31d — Two zombies may stack:** Two or more zombie tokens may occupy the same cell simultaneously. This is valid state; no special rule applies to stacked zombies beyond their individual movement.

### Round boundary and zombie movement

- [ ] **AC-v2-32a — Round boundary triggers when all seats done:** "All seat indices" means all indices 0..players.Count-1, regardless of whether those players have placed their character or have been eliminated. When the last seated player's turn ends (all seat indices in `seatsActedThisRound`), in the SAME `Handle` invocation, the following phases execute in order with no client interaction between them.

- [ ] **AC-v2-32b — Phase 1 — transition to ZombieMovement:** `phase` transitions to `ZombieMovement`.

- [ ] **AC-v2-32c — Phase 2 — containment evaluation (D1) — frozen snapshot (MF-2):** Before iterating any zombie, a FROZEN SNAPSHOT of the current grid tile rotations and zombie positions is taken at the START of this phase. The set of zombies to iterate is also fixed at phase start (frozen id list). All containment decisions and break-out rotation lookups read from this frozen snapshot. Mutations from earlier zombies in the same pass (break-out rotations, newly spawned zombies) do NOT affect containment decisions or spawn targets for later zombies in the same pass. Zombies spawned during this phase are NOT themselves evaluated for containment in this same pass.

  For each zombie id in the frozen list (iterated in stable id order): determine if the zombie was CONTAINED at phase start (reading from the frozen snapshot) — a zombie is contained if NONE of the 6 directions yields a connection-rule-valid move in the frozen snapshot (every direction is either off-grid or a closed edge on either side). For each contained zombie:

  **(a) Break-out rotation:** The server rotates the zombie's own tile ("the zombie's own tile" = the tile at the zombie's current cell) in LIVE STATE to an orientation that opens at least one edge toward an existing in-grid neighbour (neighbour existence checked against frozen snapshot). Pick the lowest direction index d (0–5) such that a neighbour cell exists in the grid at offset[d] in the frozen snapshot; set the zombie tile's rotation so that it has an open edge in direction d. If no in-grid neighbour exists in any direction, skip the rotation. CLARIFICATION: "fixed/non-rotatable" for D1 break-out means: if the zombie's own tile is a PRE-PLACED LEVEL TILE (fixed by level definition), skip the rotation sub-step entirely and proceed directly to (b) spawn. Zombie-placed tiles are NOT fixed against D1 break-out and MAY be rotated by it. The exit tile is fixed and also cannot be a zombie's tile (exit zone is zombie-spawn-forbidden). (C2)

  **(b) Spawn:** Spawn one new zombie on an adjacent in-grid tiled cell (live state). "Adjacent in-grid tiled cell" means an in-grid cell that HAS A PLACED TILE (tile-less cells are excluded — C4). Pick the lowest direction index d (0–5) such that an in-grid cell with a placed tile exists at offset[d] in the frozen snapshot AND the cell is NOT in `exitZoneCells`; spawn the new zombie there with a new stable id. Numeric ordering for direction selection is index order 0–5. If no such cell exists, skip the spawn. Co-location elimination check runs for each spawned zombie (AC-v2-31c). MaxZombies cap applies before spawn.

  Non-contained zombies receive no break-out or spawn.

- [ ] **AC-v2-32d — Phase 3 — per-zombie d6 roll and move (F4, D5):** Phase 3 iterates ALL zombies alive at the START of Phase 3, in stable id order. This includes zombies spawned during Phase 2 (break-out spawns) — they are alive at the start of Phase 3 and therefore receive a d6 roll. (Phase 2's frozen-id-list applies only to Phase 2 containment evaluation; it does not exclude Phase-2-spawned zombies from Phase 3.) For each zombie in this list: roll `Random.Shared.Next(1, 7)` (1–6), map die face to direction (die face mod 6). Check the full connection rule AND exit-zone exclusion: a move is valid if the zombie's tile has an open edge in that direction AND the neighbour exists in the grid AND the neighbour's tile has an open edge in the opposite direction AND the neighbour cell is NOT in `exitZoneCells`. If all conditions are satisfied, move the zombie to the neighbour; otherwise zombie stays. **No zombie may ever move into an exit-zone cell** (D5 — code now enforces this). Store `{ zombieId, dieFace, direction, moved }` in `lastZombieRolls`. Co-location elimination check runs after each zombie move (AC-v2-31b). Phase 3 reads the LIVE post-Phase-2 grid — break-out rotations applied in Phase 2 ARE visible to Phase 3 (SF-5 two-snapshot rule: Phase 2 containment uses frozen pre-phase snapshot; Phase 3 reads live post-Phase-2 state). **Tester assertion note (D5):** a unit test must assert that a zombie whose d6 roll maps to an exit-zone neighbour stays in place and `moved: false` is recorded, even when the connection rule would otherwise permit the move.

- [ ] **AC-v2-32e — Phase 4 — horde spawn (D2b) (MF-3):** Spawn `HordeRatePerRound[playerCount]` new zombie(s) from `hordeOriginCells`. Deterministic pick: for each zombie to spawn, select the first `hordeOriginCells` entry (lowest index = lowest q, then lowest r — H6 numeric ordering) that is an in-grid cell NOT occupied by a zombie AND NOT in `exitZoneCells` AND HAS A PLACED TILE (C4 unified spawn rule); spawn there. If that cell is zombie-occupied, tile-less, or in the exit zone, try the next entry; if all are unavailable, skip the spawn for this round. Co-location elimination check runs for each horde-spawned zombie (AC-v2-31c). MaxZombies cap applies before each spawn. Note: `hordeOriginCells ∩ exitZoneCells = ∅` is asserted by catalogue validation (AC-v2-5), so the exit-zone guard is a defensive invariant.

- [ ] **AC-v2-32f — Phase 5 — loss check and round advance:** Loss check runs. If loss condition met (AC-v2-30a), game ends. Otherwise: `roundNumber`++; `seatsActedThisRound` → `[]`; `activeSeat` → `null`; `actionPointsRemaining` → 0; `qualifyingActionsThisTurn` → 0; `phase` → `Actions`.

- [ ] **AC-v2-33 — Zombie movement once per round:** Zombies move exactly once per round, at the round boundary. Zombie movement is NOT triggered by individual AP actions.

- [ ] **AC-v2-34 — Eliminated player still participates:** An eliminated player's character no longer counts toward win/loss tracking per AC-v2-29a and AC-v2-30a, but that player still takes turns and may spend AP on DrawTile, PlaceTile, and RotateTile to help the team (co-op friend-group context). They may NOT dispatch MoveCharacter (rejected per AC-v2-45). They must still satisfy `MinActionsPerTurn` (subject to the escape hatch in AC-v2-10 if no legal actions remain). Their seat must still reach turn-end for the round boundary to advance.

### Validation (rejection cases)

- [ ] **AC-v2-35 — Action from non-active seat rejected:** A player whose seat is not `activeSeat` and is not claiming a free turn (either `activeSeat` is already occupied by another seat, or the player's seat is already in `seatsActedThisRound`) dispatching any action is rejected with `"It is not your turn."`.

- [ ] **AC-v2-36 — AP exhausted action rejected:** Any atomic action dispatched by `activeSeat` when `actionPointsRemaining == 0` is rejected with `"No action points remaining."`.

- [ ] **AC-v2-37 — PlaceTile coord not on board:** Rejected with `"Cell is not on the board."`.

- [ ] **AC-v2-38 — PlaceTile on occupied cell:** Rejected with `"Cell is already occupied."`. Zombie tokens do NOT count as occupying a cell for tile placement purposes — "occupied" for PlaceTile means the cell has a tile.

- [ ] **AC-v2-39 — PlaceTile in exit zone:** Rejected with `"Cannot place tiles in the exit zone."`.

- [ ] **AC-v2-40 — PlaceTile with 0 of that type in hand:** Rejected with `"No tiles of that type remaining."`.

- [ ] **AC-v2-41 — Invalid rotation:** `PlaceTile` or `RotateTile` with rotation outside 0–5 rejected with `"Invalid rotation."`.

- [ ] **AC-v2-42 — RotateTile on empty cell:** Rejected with `"No tile to rotate."`.

- [ ] **AC-v2-43 — RotateTile on fixed tile:** Rejected with `"Cannot rotate a fixed tile."` when the target cell contains a pre-placed level tile, a zombie-placed tile, or the exit tile. (Players may rotate only player-placed non-zombie, non-level, non-exit tiles.)

- [ ] **AC-v2-44 — MoveCharacter along closed edge:** Rejected with `"No open path to that cell."`.

- [ ] **AC-v2-45 — MoveCharacter by eliminated character:** Rejected with `"Your character has been eliminated."`.

- [ ] **AC-v2-46 — Forced zombie tile action ordering (F5):** While a player holds a zombie tile (and has not yet placed it), dispatching any action other than `PlaceZombieTile` (or the no-legal-cell forced discard path) is rejected with `"You must place your zombie tile first."` This includes `EndTurn` — the zombie-tile obligation takes precedence over everything else on that player's turn, including the qualifying-actions check (see AC-v2-9 which enforces this ordering). There is no mechanism by which a player can skip or defer the zombie-tile obligation to a subsequent turn.

- [ ] **AC-v2-47 — EndTurn rejected before minimum qualifying actions (DD1, F1):** If `activeSeat` dispatches `EndTurn` and `qualifyingActionsThisTurn < min(MinActionsPerTurn, numberOfQualifyingActionsAvailableThisTurn)` (per AC-v2-10), the action is rejected with `"You must take at least {MinActionsPerTurn} actions this turn."` Note: RotateTile does not increment `qualifyingActionsThisTurn`, so a player who used all AP on rotations but had qualifying actions available is still subject to this rejection.

- [ ] **AC-v2-48 — PlaceTile targeting reserved spawn cell rejected:** See AC-v2-13b. Rejected with `"That cell is reserved for another player's spawn."`.

- [ ] **AC-v2-49 — PlaceZombieTile on tile-less cell rejected:** PlaceZombieTile targeting an in-grid cell that has no placed tile is rejected with `"Cannot place zombie on a cell without a tile."` (C4 — all zombie spawn sources require a tiled target cell).

### State projection

- [ ] **AC-v2-50 — Projection hides other players' hands:** Given `HasStateProjection = true` and `ProjectStateForPlayer` is called for player P, then: P's own hand is returned in full (including any forced tile flags); every other player's hand is returned as an empty list; `handSizes: { playerId → int }` exposes each player's true tile count; `deck` is returned as an empty list; `deckSize: int` exposes the true deck count. Board, zombies, characters, phase, roundNumber, lastZombieRolls, exitRevealed, exitCell (once revealed), exitConnectedCount, discard count, `activeSeat`, `actionPointsRemaining`, and `reservedSpawnCells` are all returned unmasked.

- [ ] **AC-v2-51 — Projection is pure:** `ProjectStateForPlayer` never mutates the input state.

- [ ] **AC-v2-52 — Phase-3 zombie iteration includes Phase-2 spawns (F4):** A unit test crafts a state with a contained zombie that breaks out and spawns a new zombie during Phase 2. Assert that Phase 3 iterates both the original zombie AND the newly spawned zombie (i.e., `lastZombieRolls` contains entries for both). This confirms that Phase 2's frozen-id-list applies only within Phase 2 and that Phase-2-spawned zombies receive a d6 roll in Phase 3.

- [ ] **AC-v2-53 — qualifyingActionsThisTurn in state (F1):** The state field previously named `actionsThisTurn` is renamed to `qualifyingActionsThisTurn`. It counts only qualifying actions (DrawTile, PlaceTile, PlaceZombieTile, MoveCharacter). RotateTile never increments it. The field is reset to 0 at seat claiming and turn-end. TypeScript `types.ts` must mirror this rename (`qualifyingActionsThisTurn: number`). The frontend EndTurn-disabled tooltip must compare `qualifyingActionsThisTurn` (not the old `actionsThisTurn`) against `MinActionsPerTurn`.

- [ ] **AC-v2-54 — Eliminated player qualifying-actions escape (F1):** A unit test confirms that an eliminated player whose deck is empty, hand is full (HandSize tiles in hand), and has no legal PlaceTile target (all in-grid non-exit-zone cells occupied) and cannot MoveCharacter (character eliminated) has 0 qualifying actions available and may dispatch `EndTurn` immediately without taking any qualifying actions (escape hatch fires, required minimum = 0).

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

v2 flips `HasStateProjection` from `false` (v1) to `true`. `ProjectStateForPlayer` follows the LoveLetter template: own hand is returned in full; other players' hands are masked to empty lists with real counts in `handSizes`; deck is stripped to an empty list with `deckSize` exposed. Board/grid, zombies, characters, phase, roundNumber, lastZombieRolls, exitRevealed, exitCell, exitConnectedCount, discard count, `activeSeat`, `actionPointsRemaining`, and `reservedSpawnCells` are all public. Projection must be pure (deserialize / `with` / reserialize — never mutate input or project off live references).

### AD-OB-4: State shape

Top-level fields:

- `characters: [{ playerId, pos?, eliminated }]` — `pos` is nullable; null until the player places their first tile in the spawn zone. `eliminated` applies to the character only. Characters with null `pos` are NOT IN PLAY for win/loss.
- `players` — pure identity slots, unchanged from platform convention.
- `hands: { playerId → HeldTile[] }` — `HeldTile { tileType, rotation?, isZombieTile, isExitTile }`.
- `deck: DeckEntry[]` — `DeckEntry { tileType, isZombieTile, isExitTile }`. Present in full server-side; stripped to `[]` with `deckSize` in projection. Exactly one entry has `isExitTile: true`; its tileType is `cross` and rotation is 0. Deck is constructed per AC-v2-1b (safe opening band, zombie middle band, exit bottom band). The deck array in state IS the authoritative order.
- `discardPile: DeckEntry[]` — present but inert in v2; retained as a hook for follow-up.
- `zombies: [{ id, pos }]` — stable string ids for animation continuity across rounds. Two zombies may share a cell (stacking is valid).
- `roundNumber: int`.
- `phase: HexEscapePhase`.
- `lastZombieRolls: [{ zombieId, dieFace, direction, moved }]` — reset each ZombieMovement run.
- `seatsActedThisRound: int[]` — distinct list, not `HashSet<int>` (clean JSON round-trip).
- `activeSeat: int?` — the seat index currently taking its turn; null between turns and before the first turn is claimed each round.
- `actionPointsRemaining: int` — AP remaining for `activeSeat`. 0 when `activeSeat` is null.
- `qualifyingActionsThisTurn: int` — count of qualifying actions (DrawTile, PlaceTile, PlaceZombieTile, MoveCharacter) taken by `activeSeat` this turn; reset to 0 at seat claiming and turn-end. RotateTile does NOT increment this counter. Used to enforce `MinActionsPerTurn` per AC-v2-10 (F1).
- `exitConnectedCount: int` — derived BFS count from exit cell outward; broadcast as a UI hint only. 0 when `exitRevealed == false`. Does NOT gate the win condition.
- `exitRevealed: bool` — false until the exit tile is drawn and server-placed.
- `exitCell: string?` — null until `exitRevealed` becomes true; set to `"q,r"` by the server when the exit tile is drawn.
- `outcome: HexEscapeOutcome?` — null until GameOver.
- `reservedSpawnCells: { playerId → string }` — maps each player's id to their assigned spawn cell (`spawnZoneCells[seatIndex]`); set at `CreateInitialState`; cleared once a player has placed (or kept for reference — implementation choice; must be in projection).
- `handSizes: { playerId → int }` — populated by projection only; not stored in authoritative server state.
- `deckSize: int` — populated by projection only; not stored in authoritative server state.

Removed from v2: `characterStartCells` is no longer a level or state field. No `HeldExitTile` obligation (exit placement is a server side-effect of drawing).

Level shape fields:

- `spawnZoneCells: string[]` — ordered list of `"q,r"` keys on ONE side of the board. `Count >= MaxPlayers` required. Seat index N is assigned `spawnZoneCells[N]` as their reserved spawn cell. No pre-placed level tiles in this zone (H9).
- `exitZoneCells: string[]` — set of `"q,r"` keys on the OPPOSITE side of the board. RESERVED: no player tile, zombie tile, or starting zombie may be placed here. The server places the exit tile on one of these cells when it is drawn. No pre-placed level tiles in this zone (H9).
- `hordeOriginCells: string[]` — set of `"q,r"` keys defining the zombie origin zone. Must be non-empty. Adjacent to but NOT within the spawn zone (`hordeOriginCells ∩ spawnZoneCells = ∅`, asserted by AC-v2-5). All horde origin cells must have pre-placed tiles (since horde spawn requires a tiled target per C4/AC-v2-32e).
- No `exitCell` in level definition — the exit emerges from the deck and is placed by the server.

### AD-OB-5: D1 — Anti-turtle: contained zombies break out and multiply

A zombie is **contained** at the round boundary (before rolling) if NONE of the 6 directions yields a connection-rule-valid move — every direction is either off-grid or a closed edge on either side.

A contained zombie does BOTH in sequence:
- **(a) Break-out rotation:** The server rotates the zombie's own tile (= the tile at the zombie's current cell) to open at least one edge toward an in-grid neighbour. Pick the lowest direction index d (0–5) such that a neighbour cell exists in the grid at offset[d] (checked against frozen snapshot); set the zombie tile's rotation so that it has an open edge in that direction. EXCEPTION: if the zombie's own tile is a PRE-PLACED LEVEL TILE (fixed by level definition), skip sub-step (a) entirely — do NOT rotate it. Proceed directly to (b). Zombie-placed tiles (tiles spawned via PlaceZombieTile or D1 itself) are NOT fixed and MAY be rotated by break-out. The exit tile cannot be a zombie's tile (exit zone is zombie-spawn-forbidden). (C2)
- **(b) Spawn:** Spawn one new zombie on an adjacent in-grid tiled cell (live state). "In-grid tiled cell" = an in-grid cell with a placed tile (tile-less cells excluded — C4). Pick the lowest direction index d (0–5) such that an in-grid tiled non-exit-zone cell exists at offset[d] in the frozen snapshot; spawn the new zombie there with a new stable id. If no such non-exit-zone tiled cell exists, skip the spawn. Co-location elimination runs immediately on spawn. MaxZombies cap applies.

Containment is checked per-zombie in stable id order before the d6 roll phase. Zombies that are merely unable to move on a single d6 roll (normal stall) are NOT contained — full containment requires every direction to be blocked.

**Frozen-snapshot rule (MF-2):** Containment evaluation for Phase 2 uses a FROZEN SNAPSHOT of grid tile rotations and zombie positions taken at the START of Phase 2. The set of zombies iterated is also frozen at phase start. Containment decisions, break-out direction selection, and spawn-target selection all read from this frozen snapshot. Mutations from earlier zombies in the same pass (break-out rotation changes, newly spawned zombies) do NOT influence decisions for later zombies. Zombies spawned during Phase 2 are NOT re-evaluated for containment in the same pass.

**Two distinct snapshot rules by phase (SF-5):** Phase 2 containment uses the frozen pre-phase snapshot (above). Phase 3 zombie d6 movement reads the LIVE post-Phase-2 grid — break-out rotations applied in Phase 2 ARE visible to Phase 3 movement. This is intentional: break-out frees the zombie this round.

### AD-OB-6: D2 — Minimum actions and escalating horde (DD1, DD2 — replaces old mandatory-draw)

Two mechanisms enforce continuous pressure:

**(a) Minimum qualifying actions per turn (DD1, F1 — replaces mandatory draw):** On each turn, a player must take at least `min(MinActionsPerTurn, numberOfQualifyingActionsAvailableThisTurn)` **qualifying actions** (DrawTile, PlaceTile, PlaceZombieTile, MoveCharacter) before `EndTurn` is legal. RotateTile always costs AP but never counts toward the minimum and never blocks EndTurn. This resolves rotate-spam griefing and eliminated-player busy-work simultaneously. If 0 qualifying actions are available, EndTurn is accepted immediately. See AC-v2-10 for the full escape-hatch logic and short-circuit guidance. This replaces both the old mandatory-draw rule and the old broader "real actions" wording. The counter tracking this is `qualifyingActionsThisTurn` (renamed from `actionsThisTurn`). AC-v2-10 and AC-v2-47 encode the updated rule.

**(b) Escalating horde (scales by player count — DD2):** At each round boundary (after all zombie d6 moves, before loss check), `HordeRatePerRound[playerCount]` new zombie(s) spawn from `hordeOriginCells`. See Tunable Constants for the per-count table. Deterministic pick per AC-v2-32e. Co-location elimination runs immediately on horde spawn.

### AD-OB-7: D3 — Directional layout, reserved exit zone, and exit tile definition (C3, D5)

Levels define a directional layout with `spawnZoneCells` on ONE side and `exitZoneCells` on the OPPOSITE side. Exit-zone cells are RESERVED from ALL sources. **No zombie may ever occupy an exit-zone cell, by spawn OR movement** (D5):

- `PlaceTile` — forbidden (AC-v2-17, AC-v2-39).
- `PlaceZombieTile` — forbidden (AC-v2-17).
- D1 break-out spawn — exit-zone candidates skipped (AC-v2-32c, MF-3).
- D2b horde spawn — exit-zone candidates skipped (AC-v2-32e, MF-3).
- Phase-3 d6 zombie movement — moves whose target is an exit-zone cell are skipped entirely (AC-v2-32d, D5). This is a hard invariant: the connection rule alone does not prevent movement into the exit zone, so the exit-zone check is applied as a separate guard AFTER the connection rule check.

**Tester assertion (D5):** A unit test must assert that a zombie adjacent to an exit-zone cell whose d6 roll resolves to a move into that cell instead stays in place (`moved: false`).

**Exit tile definition (C3):** The exit tile is always `tileType = cross, rotation = 0` (all edges open, reachable from any direction). It is placed fixed/non-rotatable by the server (not player-rotatable). This is encoded in the deck entry (`isExitTile: true, tileType: cross`) and in the server exit-placement step (AC-v2-19).

**No-soft-lock invariant (MF-3):** Because exit-zone cells are excluded from ALL tile placement and ALL zombie spawning, and `exitZoneCells` has no pre-placed level tiles (H9), at least one empty exit-zone cell always exists when the exit tile is drawn. This eliminates the no-legal-cell soft-lock for server exit placement. Catalogue validation (AC-v2-5) enforces `hordeOriginCells ∩ exitZoneCells = ∅` and `spawnZoneCells ∩ exitZoneCells = ∅`.

**Numeric ordering for tie-breaks (H6):** In all placement algorithms and tiebreakers throughout this spec, "lowest lexicographic key" is replaced with numeric ordering: lowest q first; for equal q, lowest r. This applies to: MF-1 forced placement, server exit placement (AC-v2-19), D1 spawn (AC-v2-32c), horde spawn (AC-v2-32e), and any other deterministic selection.

When the exit tile is drawn, the SERVER places it deterministically (per AC-v2-19). The player who drew the exit tile does NOT choose placement. The exit tile does NOT enter the player's hand. No forced-placement obligation is created. The draw costs 1 AP and increments `qualifyingActionsThisTurn`.

### AD-OB-8: D4 — Disconnect handling deferred (known v2 limitation)

A disconnected player's seat stalls the round — all other players must wait for it to act. This is accepted as a known v2 limitation. Implementation adds a `[Fact(Skip="v2 known limitation: disconnected seat stalls round; auto-skip / turn-timer deferred to follow-up")]` test that documents the gap. A follow-up must add auto-skip or a turn timer before public play.

### AD-OB-9: AP turn model — `activeSeat`, `actionPointsRemaining`, and `qualifyingActionsThisTurn` (F1)

`activeSeat: int?`, `actionPointsRemaining: int`, and `qualifyingActionsThisTurn: int` are top-level state fields. A turn is claimed by the first action from any unacted seat when `activeSeat == null`. On claiming: `activeSeat` = that seat index, `actionPointsRemaining` = `ApPoolSize[playerCount]`, `qualifyingActionsThisTurn` = 0. Only `activeSeat` may act. When the turn ends (AP hits 0 or `EndTurn`): `activeSeat` → null, `actionPointsRemaining` → 0, `qualifyingActionsThisTurn` → 0, seat added to `seatsActedThisRound`. RotateTile costs AP but does not increment `qualifyingActionsThisTurn` (F1).

`ApPoolSize` is a per-count table (see Tunable Constants). Default gives higher AP at low counts (solo/duo) to compensate for fewer players contributing to deck/board work.

### AD-OB-10: Win and loss ordering

WIN is checked after each accepted AP action in the Actions phase, but ONLY when `exitRevealed == true`. Co-location elimination (AC-v2-31a) runs BEFORE win check — a character moving onto `exitCell` while zombies are there is eliminated, not victorious.

LOSS is checked after all zombie movement and spawning completes at the round boundary (after horde spawn). LOSS is never checked during the Actions phase.

Win and loss cannot resolve in the same `Handle` call because they are checked in different phases.

Win definition: `exitRevealed == true` AND all placed non-eliminated characters have `pos == exitCell` AND at least one placed non-eliminated character exists. Unplaced players are NOT IN PLAY and do not block win.

Loss definition: at least one character was ever placed AND all placed characters are eliminated.

### AD-OB-11: Co-location elimination timing

A character is eliminated IMMEDIATELY whenever it comes to share a cell with a zombie, in ALL three cases:
- (a) Player moves their character onto a zombie's cell (`MoveCharacter`).
- (b) A zombie moves onto a character's cell during ZombieMovement.
- (c) A zombie spawns on a character's cell (via `PlaceZombieTile`, D1 break-out spawn, or D2b horde spawn).

Elimination check runs before the win check in all cases. No "end of phase" batching for case (a).

### AD-OB-12: Deck composition scaling by player count (DD2, D2 — balance TBD)

The proportion of zombie tiles and total deck size scale with player count. The table below is the starting point — all numbers are explicitly **balance TBD by playtest** and are tunable constants in one place. `StartingHandSize` tiles are dealt to each player at init (from the safe-opening top band) and are not counted in the deck totals below (totals represent the in-deck counts after dealing starting hands).

| Players | Zombie tiles | Total deck size (post-deal) | Exit tile | Normal tiles |
|---------|-------------|----------------------------|-----------|--------------|
| 1       | 3           | 30                         | 1         | 26           |
| 2       | 5           | 40                         | 1         | 34           |
| 3       | 7           | 50                         | 1         | 42           |
| 4       | 10          | 60                         | 1         | 49           |
| 5       | 12          | 70                         | 1         | 57           |
| 6       | 15          | 80                         | 1         | 64           |

**Band math worked examples (D2, D3 — verified with StartingHandSize=1, SafeOpeningFraction=0.20, updated ExitBandFraction, and corrected spacing gate):**

For each player count n, verify: (a) safeRemaining ≥ 0, (b) middleBand ≥ zombieTileCount, (c) exitBand ≥ 1, (d) safeRemaining + middleBand + exitBand = postDealSize, (e) no negative or overlapping bands, (f) **middleBand ≥ minSlotsRequired (corrected)** where `minSlotsRequired = zombieCount + (zombieCount − 1) × (ZombieTileMinSpacing + 1)` = `zombieCount + (zombieCount − 1) × 3` for `ZombieTileMinSpacing=2`. This corrected gate matches the placement loop's actual step of `ZombieTileMinSpacing + 1 = 3` positions per zombie advance.

| Players | postDealSize | rawPoolSize | safeCount | safeRemaining | exitBandFrac | exitBandStart | exitBand | middleBand | zombieTiles | minSlotsRequired (corrected) = n+(n−1)×3 | safeRemaining ≥ 0? | middleBand ≥ zombieTiles? | middleBand ≥ minSlotsRequired? |
|---------|-------------|-------------|-----------|---------------|--------------|---------------|----------|------------|-------------|------------------------------------------|-------------------|--------------------------|-------------------------------|
| 1       | 30          | 31          | 6         | 5             | 0.50         | 15            | 15       | 10         | 3           | 3+(2×3)=9                                | 5 ≥ 0 ✓           | 10 ≥ 3 ✓                 | 10 ≥ 9 ✓                      |
| 2       | 40          | 42          | 8         | 6             | 0.40         | 24            | 16       | 18         | 5           | 5+(4×3)=17                               | 6 ≥ 0 ✓           | 18 ≥ 5 ✓                 | 18 ≥ 17 ✓                     |
| 3       | 50          | 53          | 10        | 7             | 0.40         | 30            | 20       | 23         | 7           | 7+(6×3)=25                               | 7 ≥ 0 ✓           | 23 ≥ 7 ✓                 | 23 ≥ 25 FAIL — see note       |
| 4       | 60          | 64          | 12        | 8             | 0.35         | 39            | 21       | 31         | 10          | 10+(9×3)=37                              | 8 ≥ 0 ✓           | 31 ≥ 10 ✓                | 31 ≥ 37 FAIL — see note       |
| 5       | 70          | 75          | 15        | 10            | 0.32         | 48            | 22       | 38         | 12          | 12+(11×3)=45                             | 10 ≥ 0 ✓          | 38 ≥ 12 ✓                | 38 ≥ 45 FAIL — see note       |
| 6       | 80          | 86          | 17        | 11            | 0.30         | 56            | 24       | 45         | 15          | 15+(14×3)=57                             | 11 ≥ 0 ✓          | 45 ≥ 15 ✓                | 45 ≥ 57 FAIL — see note       |

**Note on spacing-gate results (D3):** Player counts 3–6 fail constraint (f) under the corrected gate formula with current constants. This means the `ZombieTileMinSpacing=2` spacing guarantee CANNOT be honoured by the placement loop for these counts — the middle band is smaller than the corrected minimum required. The implementation will correctly fall back to uniform random distribution and emit the WARNING log for these counts. This is the **accepted behaviour**: the gate (corrected in D3) now truthfully reports the constraint cannot be met rather than under-counting and silently allowing the loop to run out of bounds. The spacing guarantee is therefore best-effort across all counts; it is only reliably honoured at 1p (10 ≥ 9) and 2p (18 ≥ 17, one slot of margin). The tester WARNING-log assertion must be updated accordingly: the WARNING fires for player counts 3–6. **Balance note:** `ZombieTileMinSpacing` may be reduced to 1 in a follow-up balance pass, which would give `minSlotsRequired = n + (n−1)×2` and restore guaranteed spacing at more counts; this is explicitly balance TBD by playtest. The existing constraint (b) `middleBand ≥ zombieTileCount` still holds for all 6 counts — zombie tiles always fit in the band; only the spacing guarantee is best-effort. All counts pass constraints (a)–(e).

**Balance notes (D2, D3):** `ApPoolSize[1] = 5` is higher than for 2–6 players (all now 4) — solo gets 1 extra AP. `ExitBandFraction` raised for 2–6p so the exit surfaces earlier across all multi-player counts; the critical fix is 3p (0.30→0.40), which was unwinnable because teams were overrun before the exit appeared. All values are balance TBD by playtest. **Spacing-gate finding (D3):** the corrected gate (`minSlotsRequired = n + (n−1)×3` for `ZombieTileMinSpacing=2`) reveals that counts 3–6 cannot satisfy guaranteed zombie-tile spacing with current band sizes — these counts always fall back to uniform random and the WARNING logs at startup. This is accepted; spacing is best-effort. A follow-up balance pass may lower `ZombieTileMinSpacing` to 1 (giving `minSlotsRequired = n + (n−1)×2`) which would satisfy the corrected gate for all 6 counts.

### AD-OB-12b: Design note — draw-fast mitigation (D3, updated D1/round-5)

DrawTile still counts as a qualifying action (no rule change). The draw-fast-to-surface-the-exit incentive is a known dominant strategy in early play: players draw as fast as possible to reveal the exit early, then race to it. This incentive is countered by two structural changes — no new action rule is added:

1. **Route starting zombies on Cross tiles (D1, round-5):** Tutorial-01 places 2–3 starting zombies along the path between the spawn zone and the exit zone, on pre-placed Cross r0 tiles (edges {0,1,2,3}). Surfacing the exit early is useless if characters cannot path through these zombies. **Cross tiles (not Straight) are used because on Straight (E/W-only) tiles, zombies block just one corridor row and players could route around them on an adjacent row, making route zombies bypassable and defeating the draw-fast counter. With Cross tiles, a zombie has edges toward adjacent rows and cannot be trivially skipped.** The environmental counter is therefore robust to lateral detours.
2. **Action-economy tuning (D2):** With `StartingHandSize=1` and `SafeOpeningFraction=0.20`, the opening is tighter; with `ExitBandFraction` raised for mid counts, the exit surfaces at a point where zombie pressure is already meaningful.

DrawTile remaining a qualifying action (counting toward `MinActionsPerTurn=2`) ensures players cannot simply rotate tiles all turn and pass — they must engage with the deck or the board. But the deterrent to drawing fast is environmental, not procedural.

**Accepted trade-off (D1, round-5):** A zombie sitting on a Cross tile is essentially never "contained" in the Phase-2 sense — Cross tiles have 4 open base edges, so at least one will face an in-grid neighbour, and the containment condition (ALL 6 directions blocked) almost never fires on a Cross-tiled cell. This means the D1 anti-turtle break-out mechanic **will rarely trigger on zombie-hosting or horde-origin cells in normal play**. The break-out rule remains in the rulebook and code as an edge case (e.g. a zombie on a zombie-placed tile whose narrow edges are all walled by other placed tiles), but it is no longer expected to fire on the pre-placed Cross cells the spec originally relied on for reliable break-out. The owner prioritises "zombies cannot be skipped" over reliable break-out. Any prior text claiming that Straight horde-origin tiles make break-out fire reliably is retracted — those cells are now Cross tiles and break-out on them is not a normal-play expectation.

### AD-OB-13: First-cut scope trims (locked for v2)

These are deliberately constrained to keep v2 shippable. Each trim leaves a field or constant hook so the follow-up is purely additive:

- No discard reshuffle — deck empties and stays empty; `discardPile` field is present.
- No multi-hex sprint — one hex per MoveCharacter; constant `MaxMoveDistance = 1`.
- No zombie-tile overwrite — zombie tiles may only target in-grid tiled non-zombie-occupied cells.
- Fixed hand cap 3 — constant `HandSize = 3`.
- Min qualifying actions per turn 2 — constant `MinActionsPerTurn = 2` (replaces old single-draw rule; RotateTile does not count toward minimum — F1).
- Horde rate and AP pool scale by player count — see Tunable Constants and AD-OB-12. Solo `ApPoolSize[1] = 5`; all other counts `ApPoolSize = 4` (raised from 3 for 4–6p — D2). `StartingHandSize = 1` (reduced from 2 — D2). `SafeOpeningFraction = 0.20` (reduced from 0.30 — D2).
- Ship one v2 level (the tutorial) — level loader and SetupOptions dropdown remain wired; more levels are a follow-up. Tutorial-01 level design reworked: zombie-hosting and horde-origin pre-placed tiles are Cross r0 (round-5 D1 — changed from Straight so route zombies cannot be bypassed by adjacent-row detours); exit zone set back from the east wall to a small interior cluster (round-5 D2 — disallows wall-flush exit zone); ≥2 reachable interior approaches validated against the single deterministic exit cell; route-blocking starting zombies along main path.
- Exactly one exit tile per game; tileType always `cross` at rotation 0.
- Win reachability is the players' problem: nothing in the rules guarantees the placed exit tile will be pipe-connected to characters' positions.
- Rotate-spam is bounded by AP cost + MinActionsPerTurn + horde pressure. No special anti-spam rule.
- No forced-obligation carry-over across turns — a zombie tile drawn as the last AP is resolved atomically by the server (see AC-v2-8b/MF-1).
- **MaxZombies soft-cap (SF-2):** Constant `MaxZombies = 200`. Before spawning any new zombie (via `PlaceZombieTile`, D1 spawn, or D2b horde), check `zombies.Count < MaxZombies`. If at cap, skip the spawn (log server WARNING). Safety rail only, never reached in normal play.
- **Optional D1 trim:** If D1 break-out rotation proves too complex in v2, only spawn (b) may be implemented; add `[Fact(Skip="D1 break-out rotation deferred")]` to document the gap.

### AD-OB-14: No database changes

All state lives in the JSONB blob. No `DbContext`, no tables, no migrations. Contract impact: zero changes to `IGameModule`, `IGameHandler`, `GameContext`, `GameResult`, or the TypeScript `GameModule`/`GameContext` interfaces. The only opt-in change is flipping `HasStateProjection` from `false` to `true`.

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
- Zombie tile overwrite of occupied or tile-less cells
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
- Pre-defined character start cells (reserved spawn cells replace fixed per-player starts)
- Player-chosen exit tile placement (server places deterministically on exit zone — see AD-OB-7)
- Exit tile held-obligation / carry-over across turns (eliminated by D3)
- BFS connectivity as a win gate (`exitConnectedCount` is a UI hint only)
- Loss grace period (replaced by DD3 safe-opening deck construction and starting hands)

---

## Known limitations (v2)

### KL-1: Disconnected seat stalls round (deferred — D4)

If a player disconnects during their turn, no other player can act until the disconnected seat dispatches `EndTurn`. There is no auto-skip or turn timer. This is accepted as a known v2 limitation. A follow-up must add auto-skip or a per-seat turn timer before the game is opened for public play.

Two distinct sub-cases must both be covered by the deferred `[Fact(Skip=...)]` test:
1. **Disconnect between turns:** Player disconnects after their turn ends (`activeSeat` is null). Other seats can still act; the stall only manifests when the round boundary tries to advance and the disconnected seat has not acted.
2. **Mid-turn claim-then-disconnect (worse case):** Player claims a turn (`activeSeat` is pinned to the disconnected seat, `actionPointsRemaining > 0`), then disconnects. This blocks ALL other seats immediately — no other seat can claim while `activeSeat` is occupied.

The `[Fact(Skip=...)]` annotation must read: `[Fact(Skip="v2 known limitation: disconnected seat stalls round (both mid-turn and between-turns variants); auto-skip / turn-timer deferred to follow-up")]`. The test body must cover both sub-cases.

### KL-2: Eliminated player Draw/Place/Rotate as griefing surface (accepted — F8)

Eliminated players retain access to DrawTile, PlaceTile, and RotateTile to help their teammates (co-op design intent, AC-v2-34). In a friend-group context this is cooperative assistance. In adversarial or anonymous-player contexts, an eliminated player could deliberately rotate tiles to break connections, draw tiles to drain the deck, or place tiles to block paths. This is accepted as a social-contract limitation for v2, not a rules concern. It is not a bug and will not be fixed in v2. A follow-up may add an optional "adversarial mode" flag that restricts eliminated players to zero actions. The deferred `[Fact(Skip=...)]` test from KL-1 need not cover this case; it is out of scope for v2 testing.

---

## Implementation hints

### Backend (rewrite module / models / levels)

- Implement `HexEscapeModule` implementing both `IGameModule` and `IGameHandler` (AD-1), following `FThatModule.cs` as template.
- Port pure-geometry tests (OpenEdges rotation, opposite-edge, connection rule, BFS reachability) into the new test file BEFORE deleting `HexEscapeModuleTests.cs`.
- All randomness via `Random.Shared` inside `Handle`/`Apply` — no seed in state (AD-OB-2). Deck construction per AC-v2-1b uses `Random.Shared` for zombie band shuffle and exit tile position within exit band; the resulting deck array order is stored authoritative.
- `HasStateProjection = true`; implement `ProjectStateForPlayer` as pure deserialize/with/reserialize (AD-OB-3).
- Author the tutorial level first — it is the fallback target. Tutorial level must define `spawnZoneCells` (ordered, count >= 6), `exitZoneCells`, and `hordeOriginCells`. `exitZoneCells` must be on the opposite side from `spawnZoneCells`. No pre-placed tiles in `spawnZoneCells` or `exitZoneCells`. All starting zombie positions must have pre-placed tiles. **Tutorial-01 level-design intent (D1 round-4 rework, updated round-5 D1 + D2):** (a) Use SPARSER pre-placed tiles; prefer NON-Cross types (Elbow, Tee) for general interior tiles — but see (b) and (c) for the specific zombie-hosting and horde-origin tiles; (b) place CROSS r0 tiles (base edges {0,1,2,3}) at ALL zombie-hosting cells (starting zombie positions) AND at ALL `hordeOriginCells` — this is the round-5 reversal from the v7 Straight tiles; on Straight (E/W-only) tiles, zombies blocked only one corridor row and players routed around them on adjacent rows, defeating the draw-fast counter; Cross tiles open edges toward adjacent rows so the zombie cannot be trivially skipped; the accepted trade-off is that D1 break-out almost never fires on these Cross cells — see AD-OB-12b for the full trade-off discussion; (c) the exit zone MUST be set back from the east wall — do NOT use a wall-flush exit zone (a full edge column) as it eliminates in-grid non-exit-zone neighbours of the exit cell and leaves at most one approach; **recommended concrete layout:** board q∈{−4..4}, r∈{−2..2}; exit zone = interior cluster `{(3,−1),(3,0),(3,1)}`; deterministic exit cell = `(3,0)` (centroid-closest); Cross r0 at (3,0) opens edges toward (4,0) [E], (4,−1) [NE], (3,−1) [N — in-zone, excluded], (2,0) [W] — yielding three in-grid non-exit-zone interior approaches at (4,0), (4,−1), and (2,0), all reachable from the playable interior through the q=4 column; the backend may choose other coordinates but MUST verify ≥2 reachable interior approaches to the actual deterministic exit cell per AC-v2-5; (d) place 2–3 starting zombies on pre-placed Cross r0 tiles along the route between the spawn zone and the exit zone (NOT adjacent to spawn cells) so players must navigate or clear them — this counters the draw-fast-to-exit strategy; starting zombie count of 2–3 is the design target, positioned to threaten the main path. All other AC-v2-5 invariants (zones disjoint, ApPoolSize[n] ≥ MinActionsPerTurn, rotation-aware solvability, starting zombies not adjacent to spawn) apply.
- Every enum (`HexEscapePhase`, `HexEscapeOutcome`, tile type enums) must carry `[JsonConverter(typeof(JsonStringEnumConverter))]`.
- `seatsActedThisRound` as `List<int>` with distinct guard; all logic uses `.Contains()`, never index arithmetic (AD-11).
- `activeSeat: int?`, `actionPointsRemaining: int`, and `qualifyingActionsThisTurn: int` tracked in state. Seat claiming sets `activeSeat`, resets AP to `ApPoolSize[playerCount]`, resets `qualifyingActionsThisTurn` to 0. RotateTile costs 1 AP but does not increment `qualifyingActionsThisTurn` (F1).
- Win check: after each AP action IF `exitRevealed` AND in Actions phase. Co-location elimination runs before win check on MoveCharacter. Loss check: after ZombieMovement phase (after all moves, spawns, and eliminations).
- Win check skipped entirely when `exitRevealed == false`.
- **SF-4 — Win must short-circuit (single most likely implementation bug):** When win fires, the handler must `return` IMMEDIATELY after setting `phase = GameOver`, `outcome = Escaped`, and emitting `GameOverEffect`. It must NOT fall through to the round-boundary sequence or zombie cascade. Add a unit test asserting the zombie cascade is NOT triggered in the same `Handle` call that produces a win.
- **MF-1 — DrawTile-as-last-AP atomic resolution (D4):** When `DrawTile` spends the last AP and the drawn tile is a zombie tile, resolve forced placement/discard ATOMICALLY in the same `Handle` call. Target selection (D4 character-free preference): among legal candidate cells (in-grid, tiled, non-exit-zone, not zombie-occupied), PREFER character-free cells; only use a character-occupied cell if ALL candidates are character-occupied. Deterministic tiebreak within the preferred subset: lowest q, then lowest r (H6 numeric ordering). If no valid candidate exists, discard. The state machine must never enter a configuration where `actionPointsRemaining == 0` AND a forced zombie tile is in the player's hand.
- Round-boundary sequence (all in one `Handle` invocation): ZombieMovement transition → D1 containment evaluation (Phase 2, frozen snapshot) → d6 rolls + moves (Phase 3, live post-Phase-2 grid) → D2b horde spawn → loss check → round advance.
- **MF-2 — Phase 2 frozen snapshot:** Before iterating any zombie in Phase 2, take an immutable snapshot of tile rotations and zombie positions. Read all containment decisions, break-out direction selection, and spawn-target selection from this snapshot. Apply mutations (rotation changes, new zombie spawns) to live state only. Newly spawned zombies are not re-evaluated in Phase 2.
- **SF-5 — Two distinct snapshot rules by phase:** Phase 2 uses frozen pre-phase snapshot. Phase 3 reads LIVE post-Phase-2 grid (break-out rotations ARE visible to Phase 3).
- D1 break-out: rotate zombie tile (live state) to open lowest-index direction toward an in-grid neighbour (checked against frozen snapshot) — UNLESS the zombie's own tile is a pre-placed level tile, in which case skip rotation (C2). D1 spawn: new zombie on lowest-index in-grid TILED non-exit-zone neighbour (C4). Both are deterministic — no RNG. MaxZombies cap applies before each spawn.
- **MF-3 — Exit-zone exclusion from all zombie sources:** D1 break-out spawn and D2b horde spawn must both skip any candidate cell in `exitZoneCells`. Log server DEBUG when a candidate is skipped.
- **C4 — Tiled-cell spawn rule (unified):** ALL zombie spawn sources (PlaceZombieTile, D1 break-out spawn, D2b horde spawn) target only IN-GRID cells WITH A PLACED TILE, not zombie-occupied, not in exitZoneCells. Catalogue validation asserts every starting zombie position has a pre-placed tile. Defensive rule: a zombie on a cell with no tile cannot move and is treated as not-contained (no break-out); validation makes this unreachable.
- D2b horde: pick lowest-index non-zombie-occupied, non-exit-zone, tiled `hordeOriginCells` entry. Horde rate = `HordeRatePerRound[playerCount]`. MaxZombies cap applies.
- **SF-2 — MaxZombies soft-cap:** Define `MaxZombies = 200`. Before any zombie spawn (PlaceZombieTile, D1, D2b), check `zombies.Count < MaxZombies`; if at cap skip spawn and emit server WARNING.
- Server exit placement: when exit tile is drawn, place as `cross r0 fixed` on closest-to-centre empty `exitZoneCells` entry (tie-break: lowest q, then lowest r — H6). Set `exitRevealed = true`, `exitCell` to that coord. Run win check immediately (AC-v2-20).
- MinActionsPerTurn enforcement: track `qualifyingActionsThisTurn` in state (counts DrawTile, PlaceTile, PlaceZombieTile, MoveCharacter only — RotateTile does not count). On `EndTurn`: first reject if player holds zombie tile (AC-v2-9, F5); then check `qualifyingActionsThisTurn >= min(MinActionsPerTurn, numberOfQualifyingActionsAvailableThisTurn)` (AC-v2-10, F1). Short-circuit: if deck non-empty OR hand non-full, DrawTile is available → minimum is at least 1, skip further enumeration. Reject if minimum not met. (F1 implementation guidance.)
- `exitConnectedCount`: recomputed after any action that changes board connectivity or character positions, when `exitRevealed`. Renamed from `connectedCharacters` — TypeScript must use `exitConnectedCount`. TypeScript must also rename `actionsThisTurn` → `qualifyingActionsThisTurn` (F1, AC-v2-53).
- `CreateInitialState`: use the two-quantity deck construction (F2, AC-v2-1b): compute `rawPoolSize = postDealSize + (StartingHandSize × playerCount)`; `safeCount = floor(rawPoolSize × SafeOpeningFraction)`; deal `StartingHandSize` tiles per player from the top `safeCount` raw-pool slots; band boundaries (`exitBandStart`, zombie band) computed on `postDealSize`; assign reserved spawn cells (`reservedSpawnCells[playerId] = spawnZoneCells[seatIndex]`).
- Phase enum no longer includes `Drawing` — remove it.
- Expose `ResolveZombieMove` as `internal static` (with `InternalsVisibleTo` for the test project).
- No `HexEscapeDbContext`; no EF migrations.
- All tunable constants in one `HexEscapeConstants` class: `MinActionsPerTurn`, `HandSize`, `MaxMoveDistance`, `MaxZombies`, `StartingHandSize`, `SafeOpeningFraction`, `ApPoolSize[]`, `HordeRatePerRound[]`, `ExitBandFraction[]`, `ZombieTileMinSpacing`, deck composition table.

### Frontend (rewrite types.ts and Game.tsx; keep HexBoard)

- Keep `HexBoard` axial-to-pixel rendering and geometry unchanged.
- Rewrite `types.ts` to mirror the v2 state shape (AD-OB-4) in camelCase. Include `exitRevealed: boolean`, `exitCell: string | null`, `activeSeat: number | null`, `actionPointsRemaining: number`, `qualifyingActionsThisTurn: number`, `exitConnectedCount: number`, `reservedSpawnCells: Record<string, string>`. Remove `connectedCharacters` and `actionsThisTurn` (F1, AC-v2-53).
- Remove `Drawing` from the `HexEscapePhase` string union.
- Add zombie token layer and character token layer over `HexBoard`.
- Render per-player hands, deck size, round number, phase indicator, and AP counter for `activeSeat`.
- Show `qualifyingActionsThisTurn` vs `MinActionsPerTurn` so players know when EndTurn becomes legal (F1, AC-v2-53).
- Highlight each player's reserved spawn cell until they have placed their first tile.
- Highlight exit zone cells as reserved (distinct visual treatment).
- Show AP remaining for the active seat. Disable action buttons when AP is 0 or seat is not `activeSeat`.
- Show exit cell highlight once `exitRevealed` is true.
- Animate `lastZombieRolls` — show dice result and movement arrow per zombie.
- Result screen: `outcome === 'Escaped'` → "Escaped!"; `outcome === 'Overrun'` → "Overrun!". Never render null winner.
- Highlight forced-tile obligation: block other action buttons with tooltip when player holds zombie tile.
- Show EndTurn button disabled with tooltip `"Take at least {MinActionsPerTurn} actions first"` when `qualifyingActionsThisTurn < MinActionsPerTurn` (F1). Note: RotateTile does not count toward this display threshold.
- TypeScript enums mirror as PascalCase string unions; all field names camelCase.

### Tester

- Port pure-geometry tests first, before the old test file is deleted.
- New test suite covers: deck construction uses `rawPoolSize` and `postDealSize` correctly (F2): `safeCount` computed on rawPoolSize; starting hands drawn from top `safeCount` raw-pool slots; `exitBandStart` computed on `postDealSize`; post-deal deck length equals `postDealSize`; top `(safeCount − StartingHandSize × playerCount)` deck entries are zombie/exit-free (F2 invariant); exit tile in exit band for each player count; zombie tiles in middle band only; deck has exactly one exit tile; exit tile is `cross r0`; catalogue validation (AC-v2-5, F3) passes for all authored levels; every starting zombie position has a pre-placed tile; every `hordeOriginCell` has a pre-placed level tile (F3); `ApPoolSize[n] >= MinActionsPerTurn` for all n (F3); exit-connectivity check is rotation-aware for cross r0 exit tile (F3); `spawnZoneCells ∩ hordeOriginCells = ∅`; no pre-placed tiles in spawnZoneCells or exitZoneCells; reserved spawn cell assigned per seat; first tile must target reserved spawn cell; non-first PlaceTile blocked from other players' reserved cells; PlaceTile accepted on occupied-by-zombie cell (zombie does not block tile placement); DrawTile costs 1 AP; RotateTile costs 1 AP but does NOT increment `qualifyingActionsThisTurn` (F1); AP exhausted rejects further actions; EndTurn rejected before `MinActionsPerTurn` qualifying actions (F1); EndTurn accepted after `MinActionsPerTurn` qualifying actions (F1); EndTurn rejected while holding zombie tile regardless of `qualifyingActionsThisTurn` (F5, AC-v2-9); escape hatch allows EndTurn when 0 qualifying actions available (F1, AC-v2-10); eliminated player with empty deck, full hand, no legal PlaceTile or MoveCharacter has 0 qualifying actions → EndTurn accepted (AC-v2-54, F1); unplaced player with full hand cannot use escape hatch (PlaceTile to reserved cell always available); hand cap rejects draw when full; exit-zone rejection for PlaceTile and PlaceZombieTile; PlaceZombieTile rejected on tile-less cell; character spawns on first tile; server exit placement sets exitRevealed and exitCell, tile is cross r0 fixed; server exit not in hand; win check skipped before exitRevealed; win fires when all placed non-eliminated on exitCell; unplaced players do not block win; win fires immediately after server exit if all characters already there; zombie tile discard with no legal in-grid tiled cell; MF-1 atomic resolution: drawn zombie tile on last AP increments `qualifyingActionsThisTurn` by 1 and resolves without leaving zombie tile in hand (F6, AC-v2-8b); ZombieMovement runs once at round boundary; round-boundary sequence correct; Phase-3 zombie iteration includes Phase-2-spawned zombies — `lastZombieRolls` contains entries for both original and break-out-spawned zombies (AC-v2-52, F4); D1 break-out skips rotation for pre-placed level tile (C2); D1 break-out rotates zombie-placed tile; D1 spawn targets tiled cells only (C4); D1 spawn eliminated character if present; D2b horde rate = `HordeRatePerRound[playerCount]`; horde skips tile-less cells and zombie-occupied cells; horde skips exit-zone cells; AP pool = `ApPoolSize[playerCount]`; solo `ApPoolSize[1] = 5` (raised F7); solo exit band fraction `ExitBandFraction[1] = 0.50` > 6-player fraction (F7); activeSeat set and cleared correctly; `qualifyingActionsThisTurn` resets to 0 at seat claiming and turn-end; eliminated character not counted in win check; eliminated player may still DrawTile/PlaceTile/RotateTile; eliminated player cannot MoveCharacter; loss requires ALL placed characters eliminated; unplaced characters ignored for loss; round-boundary includes all seat indices 0..N-1; projection hides other players' hands and deck; reservedSpawnCells in projection; two zombies may stack on one cell.
- Port unhappy-path rejection tests from v1 (occupied cell, not-on-board, invalid rotation, repeat action) updating for v2 action set.
- Disconnected seat stalls round: `[Fact(Skip="v2 known limitation: disconnected seat stalls round (both mid-turn and between-turns variants); auto-skip / turn-timer deferred to follow-up")]`. Test body covers both sub-cases.
- **SF-4 — Win short-circuit:** Assert when win fires, `Handle` return has `phase == GameOver` AND `zombies` list unchanged AND `lastZombieRolls` empty.
- **MF-1 — Atomic zombie resolution on last AP (D4):** Assert drawn zombie tile on last AP leaves no zombie tile in hand, zombie in board (or discardPile if no legal cell), `actionPointsRemaining == 0`. Assert that when character-free cells exist among legal candidates, the placed zombie lands on a character-free cell (D4 character-free preference). Assert that when ALL legal candidates are character-occupied the server falls back to a character-occupied cell rather than discarding (D4 fallback). Assert tiebreak within character-free subset uses lowest q then lowest r (H6). Assert that a state with zero candidates produces a discard rather than a zombie spawn.
- **MF-2 — Frozen-snapshot containment:** Crafted state with multiple contained zombies; assert A's break-out rotation does not affect B's containment decision.
- **MF-3 — Exit-zone never receives zombie (D5):** Assert D1 spawn and D2b horde never place zombie on exit-zone cell even when exit-zone cells are lowest-index candidates. Assert Phase-3 d6 movement never moves a zombie to an exit-zone cell even when the connection rule would permit the move — the zombie stays in place and `moved: false` is recorded (D5, AC-v2-32d).
- **DD1/F1 — Min qualifying actions:** Assert EndTurn rejected at 0 qualifying actions (when ≥2 qualifying actions were available), rejected at 1 qualifying action (when ≥2 were available), accepted at 2 qualifying actions. Assert RotateTile does not increment `qualifyingActionsThisTurn`. Assert escape hatch triggers when 0 qualifying actions remain (required minimum = 0). Assert escape hatch triggers when exactly 1 qualifying action was available and the player took it (required minimum = 1 and was met). Assert escape hatch does NOT apply when qualifying actions remain untaken.
- **DD2 — Scaling:** Parameterised tests across all 6 player counts for `ApPoolSize`, `HordeRatePerRound`, `ExitBandFraction`.
- **DD3/F2 — Safe start (D2):** Assert no zombie or exit tile in top `(safeCount − StartingHandSize × playerCount)` post-deal deck positions; `safeCount` computed on `rawPoolSize` (not `postDealSize`); starting hands non-empty and non-zombie/non-exit; post-deal deck length = `postDealSize`; `StartingHandSize = 1` (single tile per player); `SafeOpeningFraction = 0.20`; round-1 play does not produce instant elimination from zombie horde.
- **D2/D3 — Zombie-tile spacing and corrected gate:** The corrected construction gate uses `minSlotsRequired = zombieCount + (zombieCount − 1) × (ZombieTileMinSpacing + 1)`. With current constants (`ZombieTileMinSpacing=2`), only 1p (middleBand=10, minSlotsRequired=9) and 2p (middleBand=18, minSlotsRequired=17) pass the corrected gate — player counts 3–6 fail it and therefore ALWAYS trigger the WARNING log and fall back to uniform random spacing. Assert: (a) for 1p and 2p, zombie tiles in a freshly constructed deck are separated by at least `ZombieTileMinSpacing=2` positions in the middle band; (b) for 3p through 6p, the WARNING log `"HexEscape: zombie tile spacing constraint could not be satisfied for {playerCount}p — band too small"` is emitted at deck construction; (c) a unit test with overridden constants that force `bandWidth < minSlotsRequired` (using the corrected gate formula) also triggers the WARNING; (d) a unit test using the OLD (incorrect) gate formula `zombieCount + (zombieCount-1) × ZombieTileMinSpacing` would NOT trigger the WARNING for 3p–6p — assert the new code uses the corrected formula (verify by inspection or by confirming the WARNING fires where expected). Balance note: `ZombieTileMinSpacing` may be reduced in a follow-up to restore guaranteed spacing at more counts.
- **H10 — Reserved spawn:** Assert no permanently-unplaced softlock possible; two players cannot take each other's reserved spawn cells.
- Expose `ResolveZombieMove` as `internal static` for unit tests.

### DevOps

No migration and no new DbContext — no CI action required for v2.

---

## Story review and playtest history

**Revision 8 — round-5 zombie mobility & exit-approach fix**
**Status:** Refined v8 — zombie mobility & exit-approach fix (round-5)

### Revision history

| Rev | Date | Summary |
|-----|------|---------|
| v2r2 | 2026-06-16 | Exit-placement player choice, no anti-turtle rule, no mandatory draw |
| v3 | 2026-06-16 | Story-review: D1/D2/D3/D4 added; 46→49 ACs; 7 blockers closed |
| v4 | 2026-06-16 | Architect pre-implementation review: MF-1/2/3 applied; 49→63 ACs |
| v5 | 2026-06-16 | 3-agent playtest hardening + DD1–DD4 owner decisions; 63→76 ACs |
| v6 | 2026-06-16 | Round-2 playtest convergence: F1–F8 applied; 76→79 ACs; all prior critical/high issues resolved |
| v7 | 2026-06-16 | Round-4 balance & level pass: D1–D5 applied; 79→82 ACs; band math re-verified; 3p now winnable |
| v8 | 2026-06-16 | Round-5 zombie mobility & exit-approach fix: 3 owner-approved deltas; AC count unchanged (82); AD-OB-12 table updated with corrected spacing gate; band math re-verified |

### Round-5 playtest summary (v8)

Round-5 playtest identified three issues requiring owner decisions, all resolved and applied in this revision.

**Delta 1 — Route zombies on Straight tiles were bypassable (REVERSES part of v7 D1):**
In the v7 tutorial layout, starting zombies and horde-origin zombies sat on Straight (E/W-only) pre-placed tiles. Players discovered they could route characters on adjacent rows above or below the Straight tile, bypassing the zombie entirely. The Straight tile only blocked one corridor row; the adjacent rows were open. This defeated the spatial pressure the draw-fast counter relied on. Owner decision: ALL zombie-hosting cells and horde-origin cells must use Cross r0 pre-placed tiles (edges {0,1,2,3}). A zombie on a Cross tile can connect to adjacent rows, preventing lateral bypass. Accepted trade-off: a zombie on a Cross tile is essentially never "contained" (Cross tiles almost never satisfy the all-6-directions-blocked containment condition), so D1 break-out will rarely fire on these cells in normal play. The break-out mechanic remains in rules and code as an edge case. AC-v2-5 and the Tutorial-01 level-design intent updated. AD-OB-12b updated to document the Cross-tile counter and the accepted trade-off.

**Delta 2 — The v7 exit-approach fix was illusory (exit zone miscounted):**
v7's AC-v2-5 required "≥2 distinct rotation-aware approach cells" but counted those cells against THREE DIFFERENT exit-zone cells independently. At runtime, exactly ONE exit-zone cell receives the exit tile (the server picks the centroid-closest empty cell deterministically). For the v7 tutorial layout the deterministic exit cell was always (4,0) — a wall-flush east-edge cell with only one in-grid non-exit-zone neighbour, (3,0). The live game therefore had a single approach funnel, the very near-stall the criterion was supposed to prevent. The multi-cell counting in v7 was wrong. Owner decision: AC-v2-5 now requires ≥2 approaches validated against the SINGLE DETERMINISTIC EXIT CELL (computed by the same algorithm as AC-v2-19), not against every exit-zone cell. The exit zone must be set back from the board edge — wall-flush exit zones that eliminate interior neighbours are explicitly disallowed. Recommended concrete layout: board q∈{−4..4}, r∈{−2..2}; exit zone = `{(3,−1),(3,0),(3,1)}`; deterministic exit cell = (3,0); Cross r0 at (3,0) opens toward (4,0), (4,−1), and (2,0) — three reachable interior approaches. Backend may choose other coordinates but must verify ≥2 reachable interior approaches to the actual deterministic exit cell.

**Delta 3 — Latent spacing-gate under-count (potential future crash):**
AC-v2-1b's construction gate used `minSlotsRequired = zombieCount + (zombieCount−1)×ZombieTileMinSpacing`. The placement loop actually advances by `ZombieTileMinSpacing + 1` positions per zombie (placing zombie i at position `i × (ZombieTileMinSpacing + 1)` from the band start, meaning `ZombieTileMinSpacing` empty slots between adjacent zombie tiles). The gate therefore under-counted the required band width, and a future constant change could drive the middle-band index out of range without triggering the WARNING or fallback. Corrected gate: `minSlotsRequired = zombieCount + (zombieCount−1)×(ZombieTileMinSpacing+1)`. With current constants, the corrected gate reveals that player counts 3–6 ALREADY exceed the band capacity for guaranteed spacing — these counts will always fall back to uniform random distribution and emit the WARNING. Only 1p (10 vs 9) and 2p (18 vs 17) satisfy the corrected gate. This is accepted behaviour — the spacing guarantee is now correctly described as best-effort. AD-OB-12 band table updated with a "minSlotsRequired (corrected)" column. Tester WARNING assertion updated: the WARNING is expected for 3p–6p with current constants, not just when the band is "artificially forced too small."

AC count: 82 (unchanged — D1 and D2 update existing AC-v2-5 text; D3 updates the gate formula within AC-v2-1b; no new AC IDs added).
AD count: 14 numbered ADs plus AD-OB-12b (unchanged count — AD-OB-12, AD-OB-12b, AD-OB-13 updated in place).

### Round-4 playtest summary (v7)

Round-4 playtest ran full playthroughs with balance and rule-interaction focus. Fixed the zombie-on-exit logic bug (code path now enforces exit-zone exclusion in Phase-3 d6 movement — D5). Reworked the tutorial level intent (sparser tiles, Straight horde-origin tiles, ≥2 exit approaches, route-blocking starting zombies — D1). Tuned balance constants: `StartingHandSize` reduced from 2 to 1; `SafeOpeningFraction` reduced from 0.30 to 0.20; `ApPoolSize` raised for 4–6p from 3 to 4; `ExitBandFraction` raised for 2–6p so the exit surfaces earlier. 3-player game is now winnable (exit was appearing too late relative to zombie pressure). MF-1 forced placement now prefers character-free cells (D4) to remove unavoidable teammate elimination. Zombie-tile minimum spacing added to deck construction (D2) to prevent difficulty cliffs from clustered zombie draws. Band math re-verified for all 6 player counts with the new constants — all constraints satisfied.

Key fixes from round-4:
- **Zombie-on-exit bug (D5):** Phase-3 d6 movement was not excluding exit-zone cells as move targets. Code now blocks these moves. AC-v2-32d and AD-OB-7 updated.
- **Tutorial level rework (D1):** All-Cross pre-placed tiles were preventing containment and making D1 break-out near-impossible to trigger. Replaced with sparser Straight/Elbow/Tee tiles; Straight tiles at horde-origin cells. Added 2–3 route-blocking starting zombies. Exit zone now requires ≥2 approach cells.
- **3p unwinnable (D2):** `ExitBandFraction[3]` raised from 0.30 to 0.40 — exit now surfaces before overrun threshold. All mid counts raised.
- **6p near-zero choice (D2):** `ApPoolSize[4–6]` raised from 3 to 4 — players now have 2 discretionary AP above `MinActionsPerTurn=2`.
- **MF-1 teammate elimination (D4):** Forced placement now prefers character-free cells. Deterministic self-elimination of a teammate is no longer unavoidable.
- **Difficulty cliff from zombie clusters (D2):** `ZombieTileMinSpacing=2` added to deck construction to space zombie tiles in the middle band.

Remaining open items: none. All round-4 items applied. Spec is ready for implementation.

### Round-2 playtest summary (v6)

Round-2 playtest converged. A full 2-player game was won in simulation (escape achieved within a reasonable round count using the v6 balance constants). All prior critical and high issues are resolved:

- **Rotate-spam griefing (F1):** Resolved. RotateTile costs AP but does not count as a qualifying action. A player who spends all AP on rotations still needs qualifying actions for EndTurn to be legal (or runs out of AP, auto-ending the turn). This eliminates the rotate-spam EndTurn bypass without penalising legitimate rotations.
- **Eliminated-player busy-work (F1):** Resolved. If an eliminated player has 0 qualifying actions available (empty deck, full hand, no legal placement, no MoveCharacter), EndTurn is accepted immediately (required minimum = 0). No one is forced to take meaningless actions.
- **1-action escape-hatch hole (F1):** Resolved. The old "fewer than MinActionsPerTurn legal actions available" wording had a gap when exactly 1 qualifying action was available — a player could claim the hatch after taking 0. The new `min(MinActionsPerTurn, numberOfQualifyingActionsAvailableThisTurn)` formula closes this: if 1 qualifying action is available the player must take that 1, not 0.
- **Deck construction pre/post-deal ambiguity (F2):** Resolved. Two distinct quantities (`rawPoolSize`, `postDealSize`) are now defined with a clear invariant. Implementors cannot miscompute band boundaries by applying SafeOpeningFraction to the wrong total.
- **HordeOriginCell with no pre-placed tile (F3):** Resolved. Catalogue validation now asserts every `hordeOriginCell` has a pre-placed level tile, mirroring the existing starting-zombie-position assertion.
- **Phase-3 zombie iteration scope (F4):** Resolved. AC-v2-32d now explicitly states Phase 3 iterates ALL zombies alive at the start of Phase 3, including Phase-2 break-out spawns.
- **Forced zombie tile + EndTurn ordering (F5):** Resolved. AC-v2-9 now enforces zombie-tile obligation check before qualifying-actions check. AC-v2-46 also notes this ordering explicitly.
- **MF-1 atomic resolution counter (F6):** Resolved. The atomic forced placement increments `qualifyingActionsThisTurn` by 1 and does not push AP below 0. Minimum obligation is always satisfied after MF-1 resolution.
- **Balance constants (F7):** `ApPoolSize[1]` raised to 5 (from 4); `ExitBandFraction[1]` raised to 0.50 (from 0.40); `ExitBandFraction[5]` 0.26 (from 0.27). All remain TBD by playtest.
- **Known limitations (F8):** KL-2 added documenting eliminated-player griefing as a social-contract limitation accepted for v2.

Remaining open items: none. All round-2 items applied. Spec is ready for implementation.

### Playtest findings and resolutions (v5)

| Finding | Description | Resolution |
|---------|-------------|------------|
| C2 | Break-out rotation ambiguous for fixed/pre-placed tiles; "zombie's own tile" undefined | AD-OB-5 and AC-v2-32c: "zombie's own tile" = tile at zombie's current cell. Break-out rotation skipped for PRE-PLACED LEVEL TILES; zombie-placed tiles are rotatable. |
| C3 | Exit tile type and rotation undefined | AC-v2-18, AC-v2-19, AD-OB-7: exit tile is always `cross r0`, placed fixed/non-rotatable. |
| C4 | Zombie spawn on tile-less cell undefined; could create pathological state | AC-v2-23, AC-v2-32c, AC-v2-32e, AC-v2-49, AD-OB-5: all zombie spawn sources (PlaceZombieTile, D1, horde) require in-grid TILED cells. Catalogue validation asserts every starting zombie position has a pre-placed tile. Defensive rule documented. |
| H6 | Lexicographic "q,r" key ordering is string-order (pathological for negative coords) | AD-OB-7 and all relevant ACs: replaced with numeric ordering (lowest q; for equal q, lowest r) everywhere. |
| H7 | Horde origin cells could overlap spawn zone; zombie spawns could threaten spawn zone immediately | AC-v2-5: catalogue asserts `hordeOriginCells ∩ spawnZoneCells = ∅`. AD-OB-4 level shape note: horde origin adjacent to but not within spawn zone. |
| H9 | Pre-placed level tiles in spawn/exit zones created contradictions | AC-v2-5: catalogue asserts no pre-placed level tiles in spawnZoneCells or exitZoneCells. AD-OB-4 level shape updated. |
| H10 | Spawn-zone-full permanent softlock: if all spawn cells taken, unplaced player can never place | AC-v2-5b, AC-v2-13, AC-v2-13b, AC-v2-16: each player assigned reserved spawn cell at CreateInitialState; reserved cells protected until used; softlock eliminated. |
| DD1 | Full-hand permanent softlock: player with full hand, no legal placement, couldn't draw but had nothing to do; mandatory-draw rule couldn't unblock them | AC-v2-10, AC-v2-47, AD-OB-6: mandatory-draw replaced with MinActionsPerTurn = 2 with escape hatch. Unplaced player with full hand always has PlaceTile available (reserved cell); cannot escape via hatch. |
| DD2 | Solo unwinnable: HordeRatePerRound fixed at 1 but solo has less collective AP/deck coverage | AD-OB-12, AD-OB-9, Tunable Constants: ApPoolSize, HordeRatePerRound, ExitBandFraction all per-count tables. Solo gets more AP and earlier exit reveal. Balance TBD by playtest. |
| DD3 | One-early-death-ends-everyone: first few draws could pull zombie tiles, round-1 elimination before anyone placed | AC-v2-1, AC-v2-1b: safe opening fraction (top ~30% of deck guaranteed zombie-free); starting hands dealt from safe-opening tiles. Round 1 is structurally safe. No separate grace-period rule needed. |

### Softlock closure confirmation

**Full-hand permanent softlock (DD1):** Closed. An unplaced player with a full hand cannot EndTurn doing nothing — their only legal action is PlaceTile (reserved spawn cell), so the escape hatch does NOT apply, and the min-actions requirement forces them to place. Once placed, win/loss can resolve. A placed player with a full hand and no legal placement actions can use remaining AP on RotateTile or MoveCharacter; if truly no actions remain, the escape hatch applies and EndTurn is accepted.

**Solo-unwinnable (DD2):** Addressed structurally. `ApPoolSize[1]` is higher than for 6 players (more AP per turn solo); `ExitBandFraction[1]` is larger (exit surfaces sooner); `HordeRatePerRound[1]` is equal to higher counts (threat exists but is lower-rated relative to AP). Final balance is TBD by playtest (DD4).

### Story-review history (prior to v5)

**v4 architect verdict:** Ready for implementation (architect-confirmed; 3 must-fix items applied). The proposed round-boundary sequence is correct and complete. D1 break-out rotation is unambiguous and requires no new state fields. D2b horde spawn via `hordeOriginCells` index ordering is sufficient for v2. `activeSeat` as a nullable int in state is the right approach. MF-1, MF-2, and MF-3 are applied.

### Key edge cases to implement carefully

1. D1 containment check must run BEFORE d6 rolls.
2. D1 Phase 2 uses a FROZEN SNAPSHOT — mutations from earlier zombies do not affect later zombies' containment decisions (MF-2).
3. Phase 3 d6 movement reads the LIVE post-Phase-2 grid — break-out rotations from Phase 2 ARE visible to Phase 3 (SF-5).
4. Co-location elimination on MoveCharacter runs BEFORE win check — moving to exitCell occupied by a zombie eliminates, does not win.
5. Win must short-circuit immediately — do NOT fall through to round-boundary or zombie cascade (SF-4).
6. DrawTile-as-last-AP with a zombie tile: resolve forced placement ATOMICALLY before returning (MF-1).
7. Server exit placement fires as a side-effect of DrawTile — exit tile never enters the player's hand; exit tile is always `cross r0` fixed.
8. Unplaced characters (null pos) are ignored for BOTH win and loss.
9. Round-boundary sequence: transition → containment evaluation (frozen snapshot) → d6 rolls (live grid) → horde spawn → loss check → round advance (all in one Handle call).
10. `exitConnectedCount` rename from `connectedCharacters` must be applied in both C# and TypeScript.
11. No zombie may ever land on an exit-zone cell — enforced at PlaceZombieTile, D1 spawn, and D2b horde spawn (MF-3).
12. All zombie spawn sources require in-grid TILED cells — tile-less cells are never valid spawn targets (C4).
13. Break-out rotation is skipped for pre-placed level tiles; zombie-placed tiles are rotatable (C2).
14. "All seats" for round boundary = all indices 0..players.Count-1, regardless of placement or elimination status.
15. Numeric tiebreaker everywhere: lowest q, then lowest r (not string lexicographic order) (H6).
16. EndTurn requires `min(MinActionsPerTurn, numberOfQualifyingActionsAvailableThisTurn)` QUALIFYING actions; RotateTile is not qualifying; escape hatch fires when 0 qualifying actions available (DD1, F1).
17. Forced zombie tile on EndTurn: check zombie-tile obligation BEFORE qualifying-actions check — zombie tile must be placed first, regardless of `qualifyingActionsThisTurn` (F5).
18. MF-1 atomic resolution increments `qualifyingActionsThisTurn` by 1 and does not push AP below 0 (F6).
19. Phase 3 zombie iteration includes Phase-2 break-out spawns — they receive a d6 roll in Phase 3 (F4).
20. Deck construction: `safeCount` uses `rawPoolSize` (pre-deal); `exitBandStart` uses `postDealSize` (post-deal) — these are different quantities (F2). `StartingHandSize=1`, `SafeOpeningFraction=0.20` (D2).
21. Every `hordeOriginCell` must have a pre-placed level tile (catalogue validation — F3). Tutorial-01 horde-origin tiles AND zombie-hosting tiles must be Cross r0 (round-5 D1 — changed from Straight so zombies cannot be bypassed laterally; see AD-OB-12b for trade-off). Exit zone must be set back from the board edge — wall-flush exit zones are disallowed (round-5 D2). The ≥2 exit-approach check in AC-v2-5 is validated against the single deterministic exit cell, not against every exit-zone cell independently (round-5 D2).
22. MF-1 atomic resolution selects character-free cells first; only falls back to character-occupied cells if ALL candidates are character-occupied. Deterministic tiebreak (lowest q, then lowest r) applies within the preferred (character-free) subset (D4, AC-v2-8b).
23. Phase-3 d6 movement skips any move whose target is an exit-zone cell — even if the connection rule would otherwise permit the move. No zombie may ever occupy an exit-zone cell by spawn OR movement (D5, AC-v2-32d, AD-OB-7).
24. Zombie tiles in the middle band must be spaced with a minimum gap of `ZombieTileMinSpacing=2` deck positions; distribution is interval-based with small random jitter, not fully uniform random. Fall back to uniform if band too small, and emit a WARNING (D2, AC-v2-1b).
