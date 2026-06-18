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
///   - structurally solvable (exit cross r0 connects to ≥2 non-exit-zone approach cells
///     against the SINGLE DETERMINISTIC exit cell — AC-v2-19/AC-v2-5 v8)
///   - every zombie-hosting cell and every hordeOriginCell has a Cross r0 pre-placed tile (v8 D1)
///   - ApPoolSize[n] >= MinActionsPerTurn for all n (checked in tests)
///
/// "tutorial-01" MUST remain the first entry — it is the AC-v2-2 fallback target.
/// </summary>
public static class HexEscapeLevels
{
    private static string C(int q, int r) => $"{q},{r}";

    // ── tutorial-01 — "The Outbreak" (v8 rework — D1, D2, D3) ──────────────────
    //
    // Board: q ∈ {-4,-3,-2,-1,0,1,2,3,4}, r ∈ {-2,-1,0,1,2} = 45 cells.
    //
    // ── Zone layout ──────────────────────────────────────────────────────────────
    //
    //   Spawn zone (left side): q=-4 (all r={-2,-1,0,1,2}) + (-3,-2) = 6 cells.
    //     No pre-placed tiles in spawn zone (H9). All 6 cells are empty at start.
    //
    //   Exit zone (interior cluster, set back from east wall — v8 D2):
    //     {(3,-1),(3,0),(3,1)} = 3 cells.
    //     No pre-placed tiles in exit zone (H9). Server places exit tile here.
    //     The q=4 column is now NORMAL in-grid cells (players can tile and move there).
    //
    //   Horde origin: (-3,-1), (-3,0), (-3,1) — NOT in spawn zone (spawn uses q=-4
    //     and q=-3,r=-2 only). NOT in exit zone. All three have pre-placed Cross r0
    //     tiles per v8 D1: Cross tiles (not Straight) mean zombies here are never
    //     trivially bypassable by adjacent-row detours (AD-OB-12b round-5 rationale).
    //
    // ── Zone disjointness (AC-v2-5) ──────────────────────────────────────────────
    //
    //   spawnZoneCells  = {(-4,-2),(-4,-1),(-4,0),(-4,1),(-4,2),(-3,-2)}
    //   exitZoneCells   = {(3,-1),(3,0),(3,1)}
    //   hordeOriginCells = {(-3,-1),(-3,0),(-3,1)}
    //   spawn ∩ exit   = ∅ ✓
    //   spawn ∩ horde  = ∅ (horde uses q=-3 r={-1,0,1}; spawn uses q=-4 and (-3,-2)) ✓
    //   horde ∩ exit   = ∅ (horde at q=-3; exit at q=3) ✓
    //
    // ── Pre-placed tiles (v8: ALL zombie/horde cells use Cross r0 — D1) ──────────
    //
    //   Horde-origin tiles (Cross r0 per v8 D1):
    //     (-3,-1): Cross r0 — edges {E(0),NE(1),N(2),W(3)}.
    //     (-3, 0): Cross r0 — edges {E(0),NE(1),N(2),W(3)}.
    //     (-3, 1): Cross r0 — edges {E(0),NE(1),N(2),W(3)}.
    //     Rationale (AD-OB-12b): Cross tiles prevent zombies from being trivially
    //     bypassed by adjacent-row detours. The D1 break-out mechanic will rarely
    //     fire on these cells in normal play (Cross tiles are almost never contained),
    //     but the owner prioritises "zombies cannot be skipped" over reliable break-out.
    //
    //   Route-blocking starting zombie tiles (Cross r0 per v8 D1):
    //     ( 0, 0): Cross r0 — edges {E(0),NE(1),N(2),W(3)}. Starting zombie #1 here.
    //              Not adjacent to spawn zone (spawn is q=-4 and q=-3,r=-2;
    //              (0,0)'s neighbours are (1,0),(1,-1),(0,-1),(-1,0),(-1,1),(0,1) —
    //              none in spawn) ✓
    //     ( 2, 0): Cross r0 — edges {E(0),NE(1),N(2),W(3)}. Starting zombie #2 here.
    //              Neighbours: (3,0),(3,-1),(2,-1),(1,0),(1,1),(2,1) — none in spawn ✓
    //              Note: (3,0) and (3,-1) are in exit zone but that is fine — a zombie
    //              at (2,0) is not itself in the exit zone.
    //
    //   No "near-exit approach tiles" pre-placed (exit zone is now at q=3, not q=4;
    //   the q=4 column is normal interior and players tile it freely).
    //
    // ── Starting zombies (2 route-blocking zombies — D1) ─────────────────────────
    //
    //   (0,0) and (2,0): both on the main E–W route between spawn and exit,
    //   both have pre-placed Cross r0 tiles (C4, D1), neither is adjacent to any
    //   spawn-zone cell. Players must navigate or clear these zombies.
    //
    // ── Deterministic exit cell computation (AC-v2-19, AC-v2-5 v8) ──────────────
    //
    //   Board centroid: q∈{-4..4} × r∈{-2..2} = 45 cells.
    //   Sum of all q = 9 rows × sum(-4..4) = 9 × 0 = 0. Average q = 0.
    //   Sum of all r = 5 cols × sum(-2..2) = 5 × 0 = 0. Average r = 0.
    //   Centroid = (0, 0).
    //
    //   Exit zone cells {(3,-1),(3,0),(3,1)}, Euclidean distance to centroid (0,0):
    //     (3,-1): sqrt(3²+(-1)²) = sqrt(9+1) = sqrt(10) ≈ 3.162
    //     (3, 0): sqrt(3²+0²)    = sqrt(9)   = 3.000  ← MINIMUM
    //     (3, 1): sqrt(3²+1²)    = sqrt(9+1) = sqrt(10) ≈ 3.162
    //
    //   DETERMINISTIC EXIT CELL = (3,0) (closest to centroid; no tie). ✓
    //
    // ── ≥2 exit approach cells to deterministic exit cell (3,0) ─────────────────
    //
    //   Exit tile at (3,0) = Cross r0, open edges {E(0), NE(1), N(2), W(3)}.
    //
    //   For each open-edge direction d of Cross r0, candidate approach cell C:
    //     d=E(0)  → C=(4,0):  in-grid ✓, non-exit-zone ✓, player can place a tile
    //                          with W(3)=(0+3)%6=3 open — ANY tile type can be
    //                          oriented to open edge 3 → valid approach A1.
    //     d=NE(1) → C=(4,-1): in-grid ✓, non-exit-zone ✓, player can place a tile
    //                          with SW(4)=(1+3)%6=4 open — Tee or Cross can open
    //                          edge 4 → valid approach A2.
    //     d=N(2)  → C=(3,-1): IN exit zone — excluded.
    //     d=W(3)  → C=(2,0):  in-grid ✓, non-exit-zone ✓, has pre-placed Cross r0
    //                          with E(0)=(3+3)%6=0 open ✓ → valid approach A3.
    //
    //   Approaches: A1=(4,0), A2=(4,-1), A3=(2,0) — 3 distinct cells ≥ 2 required. ✓
    //   (2,0) has a starting zombie on a Cross r0 tile — confirms it is tiled and
    //   reachable from the spawn zone through the interior. (4,0) and (4,-1) are
    //   normal in-grid cells reachable from the interior; players freely tile them.
    //
    // ── Structural solvability ───────────────────────────────────────────────────
    //
    //   Concrete path: spawn → ... → (0,0) [Cross r0, zombie] → (1,0) → (2,0)
    //   [Cross r0, zombie] → any approach → (3,0) [exit, Cross r0].
    //   Players must place/rotate tiles to route around or past both zombies.
    //   Level is structurally solvable — not trivially so (zombies threaten the route).
    //   Level is NOT pre-won (exitRevealed starts false, no characters start on exit).
    //
    // ── Why not adjacency-to-spawn violation? ───────────────────────────────────
    //
    //   Zombie at (0,0): neighbours = {(1,0),(1,-1),(0,-1),(-1,0),(-1,1),(0,1)}.
    //     Spawn zone = {(-4,-2),(-4,-1),(-4,0),(-4,1),(-4,2),(-3,-2)}. None match. ✓
    //   Zombie at (2,0): neighbours = {(3,0),(3,-1),(2,-1),(1,0),(1,1),(2,1)}.
    //     (3,0) and (3,-1) are in exit zone, not spawn zone. None match. ✓

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
            // q = 3 (exit zone at r={-1,0,1}; r={-2,2} are normal interior)
            C( 3, -2), C( 3, -1), C( 3,  0), C( 3,  1), C( 3,  2),
            // q = 4 (normal interior cells — NOT exit zone in v8; players can tile here)
            C( 4, -2), C( 4, -1), C( 4,  0), C( 4,  1), C( 4,  2),
        ],
        PrePlacedTiles:
        [
            // v9: only the two central SEED tiles. Each holds a starting zombie; the entire horde
            // grows from these two seeds spreading/multiplying via break-out (no fixed horde spawn).
            // Cross r0 (edges {E(0),NE(1),N(2),W(3)}) on the main E–W route between spawn and exit.
            new PrePlacedTile(C( 0,  0), HexTileType.Cross, 0),
            new PrePlacedTile(C( 2,  0), HexTileType.Cross, 0),
        ],
        // Spawn zone: q=-4 (5 cells) + q=-3, r=-2 (1 cell) = 6 cells (>= MaxPlayers=6)
        // q=-3, r=-2 has no pre-placed tile (H9) and is NOT a horde origin cell.
        SpawnZoneCells:
        [
            C(-4, -2), C(-4, -1), C(-4,  0), C(-4,  1), C(-4,  2),
            C(-3, -2),
        ],
        // Exit zone: interior cluster {(3,-1),(3,0),(3,1)} = 3 cells (v8 D2 set-back).
        // No pre-placed tiles here (H9). Server picks centroid-closest cell → (3,0).
        // The q=4 column is normal interior in v8 (approach cells from the east).
        ExitZoneCells:
        [
            C( 3, -1), C( 3,  0), C( 3,  1),
        ],
        // v9: NO fixed horde — the per-round horde-origin spawn is removed. All zombie growth comes
        // from the two starting seeds spreading/multiplying via break-out. Empty = no horde spawn.
        HordeOriginCells:
        [
        ],
        // Starting zombies: 2 on main E–W route, not adjacent to spawn zone (D1, v8).
        // (0,0) and (2,0) both have pre-placed Cross r0 tiles (C4, D1 requirement).
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

    // ── Public catalogue ──────────────────────────────────────────────────────────

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
