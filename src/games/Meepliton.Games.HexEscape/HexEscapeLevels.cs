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
///   - structurally solvable (exit cross r0 connects to at least one non-exit-zone cell)
///   - ApPoolSize[n] >= MinActionsPerTurn for all n (checked in tests)
///
/// "tutorial-01" MUST remain the first entry — it is the AC-v2-2 fallback target.
/// </summary>
public static class HexEscapeLevels
{
    private static string C(int q, int r) => $"{q},{r}";

    // ── tutorial-01 — "The Outbreak" ─────────────────────────────────────────
    //
    // Layout: a rectangular region of hexes spanning q = -4..4, r = -2..2.
    // Total cells: 9 columns × 5 rows = 45 cells, laid out so that:
    //
    //   Spawn zone (left side, q = -4):  r ∈ {-2,-1,0,1,2} → 5 spawn cells BUT
    //   we need 6, so we use q=-4 and q=-3 partially to get 6+ spawn cells.
    //
    // Revised layout for simplicity and testability:
    //
    // SPAWN ZONE (q = -3): 6 cells at r = -2,-1,0,1,2 — only 5, need one more.
    // Let's use a wider board:
    //
    // Board: q ∈ {-4,-3,-2,-1,0,1,2,3,4}, r ∈ {-2,-1,0,1,2} = 45 cells
    //
    // Spawn zone: q = -4, r ∈ {-2,-1,0,1,2} + q = -3, r = -2 → 6 cells
    //   BUT spec says no pre-placed tiles in spawn zone (H9) — all 6 are empty.
    //
    // Exit zone: q = 4, r ∈ {-2,-1,0,1,2} → 5 cells, all empty (H9).
    //
    // Horde origin: q = -3, r ∈ {-1,0,1,2} — must have pre-placed tiles, not in spawn zone.
    //   - Use r ∈ {-1,0,1} for 3 horde origin cells (must have pre-placed tiles).
    //   - NOT in spawn zone (spawn zone uses q=-4 all rows, plus q=-3 r=-2).
    //   - So q=-3, r ∈ {-1,0,1} is valid (not in spawn zone, not in exit zone).
    //
    // Pre-placed tiles (must cover starting zombie positions AND horde origins):
    //   - q=-3, r={-1,0,1}: pre-placed Cross r=0 (horde origin; zombie can move any dir)
    //   - q=0, r=0: pre-placed Cross r=0 (mid-board junction; also starting zombie location)
    //   - q=3, r={-1,0,1}: pre-placed Cross r=0 (near exit zone)
    //   The exit tile (Cross r0) will be placed by server in exit zone (q=4).
    //
    // Starting zombies: at pre-placed tile positions. We put 1 zombie at (0,0).
    //
    // Normal-tile pool: mostly Straight/Elbow/Tee with some Cross.
    //
    // Structural solvability: exit zone (q=4) gets Cross r0 (exit tile).
    //   Cross r0 has edges {E(0), NE(1), N(2), W(3)}.
    //   W(3) direction from q=4 leads to q=3. q=3 cells have pre-placed Cross r0.
    //   Cross r0 at (3,r) has W(3) edge open → connection check:
    //     (4,r) edge W(3) connects to (3,r) if (3,r) has edge E(0). Cross has E(0). ✓
    //   So (3,r) cells are reachable from exit → at least one non-exit-zone cell connects ✓
    //
    // Spawn zone cells: q=-4 all r values + q=-3, r=-2 → 6 cells
    // NOT in exit zone, NOT in spawn zone: ✓

    public static readonly HexEscapeLevel Tutorial01 = new(
        Id:   "tutorial-01",
        Name: "The Outbreak",
        Cells:
        [
            // q = -4 (spawn zone column — no pre-placed tiles per H9)
            C(-4, -2), C(-4, -1), C(-4,  0), C(-4,  1), C(-4,  2),
            // q = -3 (horde origins + first spawn overflow)
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
            // Horde origin cells: q=-3, r={-1,0,1} — must have pre-placed tiles (F3)
            // Use Cross r0 so zombies can move in multiple directions
            new PrePlacedTile(C(-3, -1), HexTileType.Cross, 0),
            new PrePlacedTile(C(-3,  0), HexTileType.Cross, 0),
            new PrePlacedTile(C(-3,  1), HexTileType.Cross, 0),
            // Mid-board pre-placed tiles — starting zombie location and path anchors
            new PrePlacedTile(C( 0,  0), HexTileType.Cross, 0),
            // Near-exit pre-placed tiles: connect to the exit zone
            new PrePlacedTile(C( 3, -1), HexTileType.Cross, 0),
            new PrePlacedTile(C( 3,  0), HexTileType.Cross, 0),
            new PrePlacedTile(C( 3,  1), HexTileType.Cross, 0),
        ],
        // Spawn zone: q=-4 (5 cells) + q=-3, r=-2 (1 cell) = 6 cells total (>= MaxPlayers=6)
        // Note: q=-3, r=-2 is NOT a horde origin (horde origins are q=-3 r={-1,0,1})
        // Note: q=-3, r=-2 has NO pre-placed tile (H9 — no pre-placed tiles in spawn zone)
        SpawnZoneCells:
        [
            C(-4, -2), C(-4, -1), C(-4,  0), C(-4,  1), C(-4,  2),
            C(-3, -2),
        ],
        // Exit zone: q=4 (5 cells) — server will place exit tile here when drawn
        ExitZoneCells:
        [
            C( 4, -2), C( 4, -1), C( 4,  0), C( 4,  1), C( 4,  2),
        ],
        // Horde origin: q=-3, r={-1,0,1} — all have pre-placed Cross r=0
        // NOT in spawn zone (spawn zone uses q=-4 and q=-3 r=-2 only)
        // NOT in exit zone
        HordeOriginCells:
        [
            C(-3, -1), C(-3,  0), C(-3,  1),
        ],
        // Starting zombies: one at (0,0) which has a pre-placed Cross tile (C4)
        StartingZombies:
        [
            new StartingZombie(C(0, 0)),
        ],
        // Normal tile pool: weights defining distribution across the deck
        // Mostly Straight/Elbow/Tee for varied path-building; some Cross for junctions
        NormalTilePool:
        [
            new TilePoolEntry(HexTileType.Straight, 30),
            new TilePoolEntry(HexTileType.Elbow,    25),
            new TilePoolEntry(HexTileType.Tee,      25),
            new TilePoolEntry(HexTileType.Cross,    15),
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
