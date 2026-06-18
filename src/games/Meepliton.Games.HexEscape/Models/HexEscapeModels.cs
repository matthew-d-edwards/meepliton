using System.Text.Json.Serialization;

namespace Meepliton.Games.HexEscape.Models;

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Game phase. Phase has NO "Drawing" value (v2 removes it — see AC-v2-53).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HexEscapePhase { Actions, ZombieMovement, GameOver }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HexEscapeOutcome { Escaped, Overrun }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HexTileType { Straight, Elbow, Tee, Cross, Deadend }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HexActionType
{
    DrawTile,
    PlaceTile,
    PlaceZombieTile,
    RotateTile,
    MoveCharacter,
    EndTurn
}

// ── Tunable constants ─────────────────────────────────────────────────────────

/// <summary>
/// All balance values in one place (spec requires a single HexEscapeConstants class).
/// Balance values are TBD by playtest except where noted as structurally fixed.
/// Per-count arrays are 1-indexed: index 0 unused; use count 1–6.
/// </summary>
public static class HexEscapeConstants
{
    /// <summary>Minimum qualifying actions per turn before EndTurn is legal (DD1, F1). Balance TBD.</summary>
    public const int MinActionsPerTurn = 2;

    /// <summary>
    /// Maximum tiles in hand. Raised 3→5 (v10) so a bad draw (e.g. a Dead End) no longer deadlocks
    /// the road and the player has room to stock tiles and pre-plan an opening before committing.
    /// </summary>
    public const int HandSize = 5;

    /// <summary>
    /// Per-action movement cap. v10: a MoveCharacter action now slides the character the FULL
    /// clear length of the connected pipe network (zombies block the tunnel), so distance is no
    /// longer capped at one hex — this constant is retained only for wire/back-compat and is not
    /// consulted by the movement rule. The asymmetry is deliberate: the player travels far on one
    /// AP while a zombie advances a single tile per round.
    /// </summary>
    public const int MaxMoveDistance = 1;

    /// <summary>JSONB growth safety cap. Structurally fixed for v2.</summary>
    public const int MaxZombies = 200;

    /// <summary>
    /// Non-zombie, non-exit tiles dealt per player at CreateInitialState (DD3, D2). Raised 1→3 (v10)
    /// so the player opens with enough tiles to pre-plan a route instead of placing blind. Dealt from
    /// the guaranteed-safe top slots, so the opening hand is always zombie/exit-free. Balance TBD.
    /// </summary>
    public const int StartingHandSize = 3;

    /// <summary>Top fraction of raw pool guaranteed zombie/exit-free (DD3, F2, D2). Balance TBD.</summary>
    public const double SafeOpeningFraction = 0.20;

    /// <summary>
    /// AP granted per turn; index = player count (1–6). Higher for solo (DD2, F7, D2).
    /// Raised 4–6p from 3 to 4 so every player count has ≥2 discretionary AP above MinActionsPerTurn=2.
    /// Balance TBD by playtest.
    /// </summary>
    public static readonly int[] ApPoolSize = [0, 5, 4, 4, 4, 4, 4];

    /// <summary>
    /// Zombies spawned per round boundary; index = player count (1–6).
    /// Balance TBD by playtest.
    /// </summary>
    public static readonly int[] HordeRatePerRound = [0, 1, 1, 1, 2, 2, 2];

    /// <summary>
    /// First round whose boundary spawns the horde. Round boundaries before this
    /// spawn no horde zombies, giving players a safe window to lay an opening path
    /// before pressure ramps. This complements the deck's <see cref="SafeOpeningFraction"/>:
    /// the safe opening only guarantees the tiles you DRAW are zombie-free — it does
    /// nothing about horde spawns, which previously began at the very first boundary
    /// right beside the spawn zone (no real safe window). RoundNumber starts at 1 and is
    /// the round being closed out (pre-increment), so a value of 3 keeps the round-1 and
    /// round-2 boundaries horde-free and the first horde appears entering round 4.
    /// This is the primary early-difficulty dial — balance TBD by playtest.
    /// </summary>
    public const int HordeStartRound = 3;


    /// <summary>
    /// Exit tile placed randomly in the last X fraction of the deck (post-deal);
    /// higher fraction = earlier exit for low counts (DD2, F7, D2).
    /// Raised mid counts so exit surfaces earlier (3p fix: 0.30→0.40).
    /// Index = player count (1–6). Balance TBD by playtest.
    /// </summary>
    public static readonly double[] ExitBandFraction = [0.0, 0.50, 0.40, 0.40, 0.35, 0.32, 0.30];

    /// <summary>
    /// Minimum deck-position gap between any two zombie tiles in the middle band during
    /// construction (D2, AC-v2-1b, D3/v8). Prevents difficulty cliffs from clustered zombie draws.
    /// Reduced from 2→1: with the corrected gate formula (minSlotsRequired = z+(z-1)*(spacing+1)),
    /// spacing=1 satisfies the constraint for all six player counts (1p–6p all PASS).
    /// spacing=2 was unsatisfiable for 3p–6p under the corrected formula, making the spacing
    /// guarantee effectively a no-op for those counts. Balance TBD by playtest.
    /// </summary>
    public const int ZombieTileMinSpacing = 1;

    // ── Deck composition table (AD-OB-12) ────────────────────────────────────
    // postDealSize[count], zombieTiles[count] — indexed by player count 1–6.

    /// <summary>Deck size (post-deal = after starting hands dealt). Index = player count 1–6.</summary>
    public static readonly int[] PostDealSize = [0, 30, 40, 50, 60, 70, 80];

    /// <summary>Zombie tile count in the deck (post-deal). Index = player count 1–6.</summary>
    public static readonly int[] ZombieTileCount = [0, 3, 5, 7, 10, 12, 15];
}

// ── Hex cell ─────────────────────────────────────────────────────────────────

/// <summary>
/// A tile placed on the hex board.
/// "fixed" is a reserved keyword in C# so we use JsonPropertyName for the wire key.
/// </summary>
public record HexCell(
    HexTileType TileType,
    int Rotation,
    [property: JsonPropertyName("fixed")] bool Fixed,
    bool IsZombieTile = false
);

// ── Deck / hand entries ───────────────────────────────────────────────────────

/// <summary>A tile in the deck. Only type and flags are stored (no rotation until placed).</summary>
public record DeckEntry(
    HexTileType TileType,
    bool IsZombieTile,
    bool IsExitTile
);

/// <summary>A tile held in a player's hand.</summary>
public record HeldTile(
    HexTileType TileType,
    bool IsZombieTile,
    bool IsExitTile
);

// ── Zombie token ──────────────────────────────────────────────────────────────

/// <summary>A zombie token on the board with a stable string id for animation continuity.</summary>
public record ZombieToken(string Id, string Pos);

// ── Character ─────────────────────────────────────────────────────────────────

/// <summary>A player character. Pos is null until the player places their first tile.</summary>
public record CharacterState(
    string PlayerId,
    string? Pos,
    bool Eliminated
);

// ── Last zombie roll ──────────────────────────────────────────────────────────

/// <summary>Result of a single zombie d6 roll during ZombieMovement phase.</summary>
public record ZombieRoll(
    string ZombieId,
    int DieFace,
    int Direction,
    bool Moved
);

// ── Player slot ───────────────────────────────────────────────────────────────

public record PlayerSlot(
    string Id,
    string DisplayName,
    string? AvatarUrl,
    int SeatIndex
);

// ── State ─────────────────────────────────────────────────────────────────────

public record HexEscapeState(
    HexEscapePhase Phase,
    HexEscapeOutcome? Outcome,
    string LevelId,
    string LevelName,
    // Board geometry
    List<string> Cells,                              // ALL valid board cell keys "q,r"
    Dictionary<string, HexCell> Grid,               // occupied cells only, key "q,r"
    // Deck / hands / discard
    List<DeckEntry> Deck,
    Dictionary<string, List<HeldTile>> Hands,       // playerId → hand
    List<DeckEntry> DiscardPile,
    // Zombies and characters
    List<ZombieToken> Zombies,
    List<CharacterState> Characters,
    // AP turn model
    int? ActiveSeat,
    int ActionPointsRemaining,
    int QualifyingActionsThisTurn,
    List<int> SeatsActedThisRound,                  // distinct set — List with guard, NOT HashSet
    // Round state
    int RoundNumber,
    List<ZombieRoll> LastZombieRolls,
    // Exit
    bool ExitRevealed,
    string? ExitCell,
    int ExitConnectedCount,
    // Players
    List<PlayerSlot> Players,
    // Level broadcast info (safe to expose)
    List<string> SpawnZoneCells,
    List<string> ExitZoneCells,
    List<string> HordeOriginCells,
    // Reserved spawn cell per player (playerId → "q,r")
    Dictionary<string, string> ReservedSpawnCells,
    // Zombie ID counter for stable unique ids
    int NextZombieId,
    // Projection-only fields (populated by ProjectStateForPlayer, 0/null in authoritative state)
    Dictionary<string, int>? HandSizes = null,
    int? DeckSize = null
);

// ── Actions ──────────────────────────────────────────────────────────────────

/// <summary>
/// Single action record covering all six action types (AD-OB-4 wire contract).
/// Nullable fields are only set for the relevant action type.
/// </summary>
public record HexEscapeAction(
    HexActionType Type,
    string? Coord = null,        // PlaceTile, PlaceZombieTile, RotateTile, MoveCharacter (toCoord)
    HexTileType? TileType = null, // PlaceTile
    int? Rotation = null         // PlaceTile, RotateTile
);

// ── Options ──────────────────────────────────────────────────────────────────

public record HexEscapeOptions(string? LevelId = null);

// ── Level definition ──────────────────────────────────────────────────────────

/// <summary>A tile that is pre-placed at level start (fixed, non-rotatable by players).</summary>
public record PrePlacedTile(string Coord, HexTileType TileType, int Rotation);

/// <summary>A starting zombie at a pre-placed cell defined by the level (C4).</summary>
public record StartingZombie(string Coord);

/// <summary>
/// Pool of normal tile types and their relative weights used for deck construction.
/// </summary>
public record TilePoolEntry(HexTileType TileType, int Weight);

public record HexEscapeLevel(
    string Id,
    string Name,
    List<string> Cells,                  // ALL valid board cell keys "q,r"
    List<PrePlacedTile> PrePlacedTiles,  // tiles placed at level start (fixed, non-rotatable by players)
    List<string> SpawnZoneCells,         // ordered; seat index N → SpawnZoneCells[N]; count >= MaxPlayers
    List<string> ExitZoneCells,          // reserved for server exit tile placement
    List<string> HordeOriginCells,       // horde spawns from here each round
    List<StartingZombie> StartingZombies,
    List<TilePoolEntry> NormalTilePool   // relative weights for normal tile distribution in deck
);
