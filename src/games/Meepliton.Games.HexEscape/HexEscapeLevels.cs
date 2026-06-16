using Meepliton.Games.HexEscape.Models;

namespace Meepliton.Games.HexEscape;

/// <summary>
/// Static catalogue of all Hex Escape v2 (Outbreak) levels.
///
/// Direction offsets (axial, q right, r down-left):
///   dir0=(+1, 0)=E   dir1=(+1,-1)=NE  dir2=(0,-1)=N
///   dir3=(-1, 0)=W   dir4=(-1,+1)=SW  dir5=(0,+1)=S
///   Opposite of dir d = (d+3)%6.
///
/// Tile base edges (before rotation):
///   Straight {0,3}=E+W   Elbow {0,1}=E+NE   Tee {0,1,2}=E+NE+N
///   Cross {0,1,2,3}=E+NE+N+W   Deadend {0}=E
/// Rotation k maps base edge e to (e+k)%6.
///
/// All authored levels satisfy AC-v2-5 catalogue invariants:
///   - spawnZoneCells.Count >= 6 (MaxPlayers)
///   - exitZoneCells non-empty
///   - hordeOriginCells non-empty
///   - no pre-placed tiles in spawnZoneCells or exitZoneCells (H9)
///   - every starting zombie position has a pre-placed tile (C4)
///   - every hordeOriginCell has a pre-placed tile (F3)
///   - hordeOriginCells ∩ spawnZoneCells = ∅ (H7)
///   - hordeOriginCells ∩ exitZoneCells = ∅
///   - spawnZoneCells ∩ exitZoneCells = ∅
///   - structurally solvable (exit cross r0 connects to ≥2 non-exit-zone approach cells)
///   - ApPoolSize[n] >= MinActionsPerTurn for all n (checked in tests)
///
/// "tutorial-01" MUST remain the first entry — it is the AC-v2-2 fallback target.
/// </summary>
public static class HexEscapeLevels
{
    private static string C(int q, int r) => $"{q},{r}";

    // ── tutorial-01 — "The Outbreak" (v7 rework — D1) ───────────────────────
    //
    // Board: q ∈ {-4,-3,-2,-1,0,1,2,3,4}, r ∈ {-2,-1,0,1,2} = 45 cells.
    //
    // ── Zone layout ──────────────────────────────────────────────────────────
    //
    //   Spawn zone (left side): q=-4 (all r={-2,-1,0,1,2}) + (-3,-2) = 6 cells.
    //     No pre-placed tiles in spawn zone (H9). All 6 cells are empty at start.
    //
    //   Exit zone (right side): q=4 (all r={-2,-1,0,1,2}) = 5 cells.
    //     No pre-placed tiles in exit zone (H9). Server places exit tile here.
    //
    //   Horde origin: (-3,-1), (-3,0), (-3,1) — NOT in spawn zone (spawn uses q=-4
    //     and q=-3,r=-2 only). NOT in exit zone. All three have pre-placed STRAIGHT
    //     tiles (Straight r0 = edges {E(0),W(3)}). Per D1 design intent: Straight tiles
    //     at horde-origin cells allow a second zombie piling onto these cells to become
    //     contained (only 2 open edges), triggering the D1 anti-turtle break-out mechanic.
    //     If Cross tiles were used, zombies would never be contained (4 open edges).
    //
    // ── Zone disjointness (AC-v2-5) ──────────────────────────────────────────
    //
    //   spawnZoneCells = {(-4,-2),(-4,-1),(-4,0),(-4,1),(-4,2),(-3,-2)}
    //   exitZoneCells  = {(4,-2),(4,-1),(4,0),(4,1),(4,2)}
    //   hordeOriginCells = {(-3,-1),(-3,0),(-3,1)}
    //   spawn ∩ exit   = ∅ ✓
    //   spawn ∩ horde  = ∅ (horde uses q=-3 r={-1,0,1}; spawn uses q=-4 and (-3,-2)) ✓
    //   horde ∩ exit   = ∅ ✓
    //
    // ── Pre-placed tiles (v7: sparser, mostly non-Cross — D1) ────────────────
    //
    //   Horde-origin tiles (Straight r0):
    //     (-3,-1): Straight r0 — edges {E(0),W(3)}. Horde origin, starts empty of zombies.
    //     (-3, 0): Straight r0 — edges {E(0),W(3)}. Horde origin, starts empty of zombies.
    //     (-3, 1): Straight r0 — edges {E(0),W(3)}. Horde origin, starts empty of zombies.
    //
    //   Route-blocking zombie tiles (Straight r0 — provides a tiled cell for starting zombies):
    //     ( 0, 0): Straight r0 — edges {E(0),W(3)}. Starting zombie #1 here.
    //              Not adjacent to spawn zone (spawn zone is q=-4 and q=-3,r=-2;
    //              (0,0)'s neighbours are (1,0),(1,-1),(0,-1),(-1,0),(-1,1),(0,1) — none in spawn) ✓
    //     ( 2, 0): Straight r0 — edges {E(0),W(3)}. Starting zombie #2 here.
    //              Neighbours: (3,0),(3,-1),(2,-1),(1,0),(1,1),(2,1) — none in spawn ✓
    //
    //   Near-exit approach tiles (Straight r0 — provide the ≥2 exit approach cells):
    //     ( 3,-1): Straight r0 — edges {E(0),W(3)}.
    //     ( 3, 0): Straight r0 — edges {E(0),W(3)}.
    //     ( 3, 1): Straight r0 — edges {E(0),W(3)}.
    //
    // ── Starting zombies (2 route-blocking zombies — D1) ─────────────────────
    //
    //   (0,0) and (2,0): both on the main E–W route between spawn and exit,
    //   both have pre-placed Straight tiles (C4), neither is adjacent to any
    //   spawn-zone cell. Players must navigate or clear these zombies.
    //
    // ── Structural solvability and ≥2 exit approach cells (AC-v2-5) ──────────
    //
    //   Board centroid = (0,0) (9-column × 5-row grid, symmetric).
    //   Server places exit tile at the centroid-closest empty exit-zone cell,
    //   tie-break lowest q then r. All exit-zone cells are at q=4; distances:
    //     (4,0): sqrt(16+0) = 4.0   ← minimum
    //     (4,±1): sqrt(16+1) ≈ 4.12
    //     (4,±2): sqrt(16+4) ≈ 4.47
    //   So exit tile is placed at (4,0), which is Cross r0 (edges {E(0),NE(1),N(2),W(3)}).
    //
    //   Exit approach cells: a non-exit-zone in-grid cell C adjacent to exit-zone cell E
    //   is a valid approach if: the exit tile (Cross r0) at E has an open edge in direction
    //   d toward C, AND C has a pre-placed tile with edge (d+3)%6 open.
    //
    //   From (4,0) [exit tile = Cross r0]:
    //     dir E(0)→(5,0): off-board.
    //     dir NE(1)→(5,-1): off-board.
    //     dir N(2)→(4,-1): in exit zone — skip.
    //     dir W(3)→(3,0): in-grid, non-exit-zone. (3,0) has Straight r0, edge E(0)=(dir 0)=(d+3)%6
    //       where d=3, (3+3)%6=0. Straight r0 has E(0) ✓ → approach cell A1.
    //     dir SW(4): Cross r0 has no edge 4. skip.
    //     dir S(5): Cross r0 has no edge 5. skip.
    //
    //   From (4,-1) [could also receive exit if (4,0) occupied; or for ≥2 check]:
    //     dir W(3)→(3,-1): in-grid, non-exit-zone. (3,-1) has Straight r0, edge E(0)=(0+3)%6=no wait:
    //       d=3, (d+3)%6=0. Straight r0 has E(0) ✓ → approach cell A2.
    //
    //   From (4,1):
    //     dir W(3)→(3,1): in-grid, non-exit-zone. (3,1) has Straight r0, edge E(0) ✓ → approach cell A3.
    //
    //   Approach cells: A1=(3,0), A2=(3,-1), A3=(3,1) — 3 distinct cells ≥ 2 required ✓
    //   Each is adjacent to a reachable exit-zone cell, each has a pre-placed Straight r0
    //   tile with E(0) open, satisfying the rotation-aware exit-connectivity check.

    public static readonly HexEscapeLevel Tutorial01 = new(
        Id:   "tutorial-01",
        Name: "The Outbreak",
        Cells:
        [
            // q = -4 (spawn zone column — no pre-placed tiles per H9)
            C(-4, -2), C(-4, -1), C(-4,  0), C(-4,  1), C(-4,  2),
            // q = -3 (horde origins at r={-1,0,1}; spawn overflow at r=-2)
            C(-3, -2), C(-3, -1), C(-3,  0), C(-3,  1), C(-3,  2),
            // q = -2
            C(-2, -2), C(-2, -1), C(-2,  0), C(-2,  1), C(-2,  2),
            // q = -1
            C(-1, -2), C(-1, -1), C(-1,  0), C(-1,  1), C(-1,  2),
            // q = 0
            C( 0, -2), C( 0, -1), C( 0,  0), C( 0,  1), C( 0,  2),
            // q = 1
            C( 1, -2), C( 1, -1), C( 1,  0), C( 1,  1), C( 1,  2),
            // q = 2
            C( 2, -2), C( 2, -1), C( 2,  0), C( 2,  1), C( 2,  2),
            // q = 3
            C( 3, -2), C( 3, -1), C( 3,  0), C( 3,  1), C( 3,  2),
            // q = 4 (exit zone — no pre-placed tiles per H9)
            C( 4, -2), C( 4, -1), C( 4,  0), C( 4,  1), C( 4,  2),
        ],
        PrePlacedTiles:
        [
            // Horde origin cells (q=-3, r={-1,0,1}): Straight r=0 per D1 design intent.
            // Straight tiles allow containment → break-out mechanic fires when a second zombie
            // arrives here. All-Cross would prevent containment entirely.
            new PrePlacedTile(C(-3, -1), HexTileType.Straight, 0),
            new PrePlacedTile(C(-3,  0), HexTileType.Straight, 0),
            new PrePlacedTile(C(-3,  1), HexTileType.Straight, 0),
            // Route-blocking starting zombie cells: Straight r=0 on main E-W route.
            // Starting zombies are placed here (below), forcing players to navigate/clear them.
            new PrePlacedTile(C( 0,  0), HexTileType.Straight, 0),
            new PrePlacedTile(C( 2,  0), HexTileType.Straight, 0),
            // Near-exit approach tiles: Straight r=0 at q=3. Each has E(0) open,
            // providing ≥2 rotation-aware exit approach cells (see geometry comment above).
            new PrePlacedTile(C( 3, -1), HexTileType.Straight, 0),
            new PrePlacedTile(C( 3,  0), HexTileType.Straight, 0),
            new PrePlacedTile(C( 3,  1), HexTileType.Straight, 0),
        ],
        // Spawn zone: q=-4 (5 cells) + q=-3, r=-2 (1 cell) = 6 cells (>= MaxPlayers=6)
        // q=-3, r=-2 has no pre-placed tile (H9) and is NOT a horde origin cell.
        SpawnZoneCells:
        [
            C(-4, -2), C(-4, -1), C(-4,  0), C(-4,  1), C(-4,  2),
            C(-3, -2),
        ],
        // Exit zone: q=4 (5 cells) — server places exit tile here when drawn.
        // No pre-placed tiles here (H9). Server picks centroid-closest cell → (4,0).
        ExitZoneCells:
        [
            C( 4, -2), C( 4, -1), C( 4,  0), C( 4,  1), C( 4,  2),
        ],
        // Horde origin: q=-3, r={-1,0,1}. All have pre-placed Straight r=0 tiles (F3).
        // Not in spawn zone (spawn uses q=-4 and q=-3,r=-2 only). Not in exit zone.
        HordeOriginCells:
        [
            C(-3, -1), C(-3,  0), C(-3,  1),
        ],
        // Starting zombies: 2 on main E–W route, not adjacent to spawn zone (D1, AC-v2-8b design note).
        // (0,0) and (2,0) both have pre-placed Straight tiles (C4 requirement).
        StartingZombies:
        [
            new StartingZombie(C( 0,  0)),
            new StartingZombie(C( 2,  0)),
        ],
        // Normal tile pool: weights defining deck composition.
        // Straight and Elbow tiles dominate for varied path-building;
        // some Tee for branching; minimal Cross and Deadend to keep board complexity manageable.
        NormalTilePool:
        [
            new TilePoolEntry(HexTileType.Straight, 30),
            new TilePoolEntry(HexTileType.Elbow,    30),
            new TilePoolEntry(HexTileType.Tee,      25),
            new TilePoolEntry(HexTileType.Cross,    10),
            new TilePoolEntry(HexTileType.Deadend,   5),
        ]
    );

    // ── Public catalogue ──────────────────────────────────────────────────────

    /// <summary>
    /// All authored levels indexed by ID.
    /// "tutorial-01" must be first (and always present) — it is the AC-v2-2 fallback.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, HexEscapeLevel> All =
        new Dictionary<string, HexEscapeLevel>
        {
            { Tutorial01.Id, Tutorial01 },
        };

    /// <summary>
    /// Levels in display order (tutorial first). Used to render the host's level
    /// picker deterministically, independent of dictionary enumeration order.
    /// </summary>
    public static readonly IReadOnlyList<HexEscapeLevel> Ordered = [Tutorial01];
}
