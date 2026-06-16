using Meepliton.Games.HexEscape.Models;

namespace Meepliton.Games.HexEscape;

/// <summary>
/// Static catalogue of all Hex Escape levels.
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
/// Levels are guaranteed solvable and NOT pre-won.
/// "tutorial-01" MUST remain the first entry — it is the AD-10 fallback target.
/// </summary>
public static class HexEscapeLevels
{
    private static string C(int q, int r) => $"{q},{r}";

    // ── tutorial-01 — "The Straight Path" ────────────────────────────────────
    //
    // Five cells in a horizontal row.
    //   S = (-2,0) pre-placed Straight r=0 → edges {E,W}
    //   [-1,0] [0,0] [1,0]  empty — players fill with Straight r=0
    //   X = (2,0)  pre-placed Straight r=0 → edges {E,W}
    //
    // BFS from exit (2,0): W→(1,0)[E+W]→W→(0,0)[E+W]→W→(-1,0)[E+W]→W→(-2,0) SURVIVOR ✓
    // Not pre-won: middle 3 cells are empty at start, BFS cannot traverse them.

    public static readonly HexEscapeLevel Tutorial01 = new(
        Id:   "tutorial-01",
        Name: "The Straight Path",
        Cells: [C(-2, 0), C(-1, 0), C(0, 0), C(1, 0), C(2, 0)],
        Walls: [],
        PrePlacedTiles:
        [
            new PrePlacedTile(C(-2, 0), HexTileType.Straight, 0),
            new PrePlacedTile(C(2,  0), HexTileType.Straight, 0),
        ],
        SurvivorStartCells: [C(-2, 0)],
        ExitCell: C(2, 0),
        TileHandCounts: new Dictionary<HexTileType, int>
        {
            { HexTileType.Straight, 3 },
            { HexTileType.Elbow,    0 },
            { HexTileType.Tee,      0 },
            { HexTileType.Cross,    0 },
            { HexTileType.Deadend,  0 },
        },
        ThreatThreshold: 5
    );

    // ── medium-01 — "The Bend" ────────────────────────────────────────────────
    //
    // An L-shaped corridor: straight segment then a 90° turn.
    //   S = (-2,0) pre-placed Straight r=0 → edges {E,W}
    //   [-1,0] empty — player places Straight r=0 → {E,W}
    //   [0,0]  empty — player places Elbow r=2  → {N,W}
    //     Elbow base {0,1}+2 → {(0+2)%6,(1+2)%6} = {2,3} = N+W
    //   [0,-1] empty — player places Straight r=2 → {N,S}
    //     Straight base {0,3}+2 → {(0+2)%6,(3+2)%6} = {2,5} = N+S
    //   X = (0,-2) pre-placed Straight r=2 → edges {N,S}
    //
    // BFS from exit (0,-2) Straight r=2 → {N(2),S(5)}:
    //   S(5)→(0,-1): (0,-1) needs N(2). Straight r=2 has N(2). ✓ Visit (0,-1).
    //   (0,-1) Straight r=2 {N,S}: S(5)→(0,-2) visited; N(2)→(0,0): (0,0) needs S(5).
    //     Elbow r=2 → {2,3}=N+W. Does NOT have S(5). But wait — from (0,-1) going N(2)
    //     we arrive at (0,-2)? No: dir2=(0,-1), so (0,-1)+dir2=(0,-2). That's the exit,
    //     already visited. Going S means dir5=(0,+1), so (0,-1)+dir5=(0,0). (0,0) needs
    //     open edge in (5+3)%6=2=N. Elbow r=2 → {2,3}: has N(2) ✓. Visit (0,0).
    //   (0,0) Elbow r=2 {N,W}: N(2)→(0,-1) visited; W(3)→(-1,0): (-1,0) needs E(0).
    //     Straight r=0 → {0,3}: has E(0) ✓. Visit (-1,0).
    //   (-1,0) Straight r=0 {E,W}: E(0)→(0,0) visited; W(3)→(-2,0): (-2,0) needs E(0).
    //     Straight r=0 → {0,3}: has E(0) ✓. Visit (-2,0) = SURVIVOR ✓.
    //   connectedSurvivors=1=totalSurvivors → WIN ✓
    //
    // Not pre-won: (-1,0), (0,0), (0,-1) are empty at start.

    public static readonly HexEscapeLevel Medium01 = new(
        Id:   "medium-01",
        Name: "The Bend",
        Cells: [C(-2, 0), C(-1, 0), C(0, 0), C(0, -1), C(0, -2)],
        Walls: [],
        PrePlacedTiles:
        [
            new PrePlacedTile(C(-2, 0), HexTileType.Straight, 0),
            new PrePlacedTile(C(0, -2), HexTileType.Straight, 2),
        ],
        SurvivorStartCells: [C(-2, 0)],
        ExitCell: C(0, -2),
        TileHandCounts: new Dictionary<HexTileType, int>
        {
            { HexTileType.Straight, 2 },
            { HexTileType.Elbow,    1 },
            { HexTileType.Tee,      0 },
            { HexTileType.Cross,    0 },
            { HexTileType.Deadend,  0 },
        },
        ThreatThreshold: 4
    );

    // ── hard-01 — "Two Roads" ─────────────────────────────────────────────────
    //
    // Two survivors must both reach the exit. They share a junction cell (0,0)
    // that requires a Cross tile, and three straight segments.
    //
    // Cells:
    //   (-2,0) survivor A  pre-placed Straight r=0 → {E,W}
    //   (-1,0) empty       player places Straight r=0 → {E,W}
    //   (0,0)  empty       player places Cross r=0 → {E,NE,N,W}
    //     Cross base {0,1,2,3}+0 → {0,1,2,3}=E+NE+N+W
    //     NE edge goes to (1,-1) which is a wall → dead end (not an error per spec).
    //   (1,0)  empty       player places Straight r=0 → {E,W}
    //   (2,0)  survivor B  pre-placed Straight r=0 → {E,W}
    //   (0,-1) empty       player places Straight r=2 → {N,S}
    //   (0,-2) exit        pre-placed Straight r=2 → {N,S}
    //   (1,-1) wall        on-board but impassable — no tile may be placed
    //
    // BFS from exit (0,-2) Straight r=2 {N,S}:
    //   S(5)→(0,-1): needs N(2). Straight r=2 has N(2). ✓ Visit (0,-1).
    //   (0,-1) {N,S}: S(5)→(0,0): (0,0) needs (5+3)%6=2=N. Cross r=0 has N(2). ✓ Visit (0,0).
    //   (0,0) Cross r=0 {E,NE,N,W}:
    //     N(2)→(0,-1) visited
    //     NE(1)→(1,-1) is wall — wall cells have no tile, so no open edges. Dead end.
    //     E(0)→(1,0): (1,0) needs (0+3)%6=3=W. Straight r=0 has W(3). ✓ Visit (1,0).
    //     W(3)→(-1,0): (-1,0) needs (3+3)%6=0=E. Straight r=0 has E(0). ✓ Visit (-1,0).
    //   (1,0) {E,W}: E(0)→(2,0): (2,0) needs W(3). Straight r=0 has W(3). ✓ Visit (2,0)=SURVIVOR B ✓
    //   (-1,0) {E,W}: W(3)→(-2,0): (-2,0) needs E(0). Straight r=0 has E(0). ✓ Visit (-2,0)=SURVIVOR A ✓
    //   connectedSurvivors=2=totalSurvivors → WIN ✓
    //
    // Not pre-won: (-1,0),(0,0),(1,0),(0,-1) are empty at start.
    //
    // Required tiles: 3 Straight (for -1,0 and 1,0 and 0,-1) + 1 Cross (for 0,0) = 4 tiles total.
    // Threat threshold 3 means 3 full rounds max (tight for 1–2 players, achievable for groups).

    public static readonly HexEscapeLevel Hard01 = new(
        Id:   "hard-01",
        Name: "Two Roads",
        Cells:
        [
            C(-2, 0), C(-1, 0), C(0, 0), C(1, 0), C(2, 0),
            C(0, -1), C(0, -2),
            C(1, -1),
        ],
        Walls: [C(1, -1)],
        PrePlacedTiles:
        [
            new PrePlacedTile(C(-2, 0), HexTileType.Straight, 0),
            new PrePlacedTile(C(2,  0), HexTileType.Straight, 0),
            new PrePlacedTile(C(0, -2), HexTileType.Straight, 2),
        ],
        SurvivorStartCells: [C(-2, 0), C(2, 0)],
        ExitCell: C(0, -2),
        TileHandCounts: new Dictionary<HexTileType, int>
        {
            { HexTileType.Straight, 3 },
            { HexTileType.Elbow,    0 },
            { HexTileType.Tee,      0 },
            { HexTileType.Cross,    1 },
            { HexTileType.Deadend,  0 },
        },
        ThreatThreshold: 3
    );

    // ── Public catalogue ──────────────────────────────────────────────────────

    /// <summary>
    /// All authored levels indexed by ID.
    /// "tutorial-01" must be first (and always present) — it is the AD-10 fallback.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, HexEscapeLevel> All =
        new Dictionary<string, HexEscapeLevel>
        {
            { Tutorial01.Id, Tutorial01 },
            { Medium01.Id,   Medium01   },
            { Hard01.Id,     Hard01     },
        };
}
