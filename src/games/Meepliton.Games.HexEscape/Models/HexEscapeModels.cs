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

    /// <summary>Maximum tiles in hand. Structurally fixed for v2.</summary>
    public const int HandSize = 3;

    /// <summary>Hexes per MoveCharacter action. Structurally fixed for v2.</summary>
    public const int MaxMoveDistance = 1;

    /// <summary>JSONB growth safety cap. Structurally fixed for v2.</summary>
    public const int MaxZombies = 200;

    /// <summary>Non-zombie, non-exit tiles dealt per player at CreateInitialState (DD3). Balance TBD.</summary>
    public const int StartingHandSize = 2;

    /// <summary>Top fraction of raw pool guaranteed zombie/exit-free (DD3, F2). Balance TBD.</summary>
    public const double SafeOpeningFraction = 0.30;

    /// <summary>
    /// AP granted per turn; index = player count (1–6). Higher for low counts (DD2, F7).
    /// Balance TBD by playtest.
    /// </summary>
    public static readonly int[] ApPoolSize = [0, 5, 4, 4, 3, 3, 3];

    /// <summary>
    /// Zombies spawned per round boundary; index = player count (1–6).
    /// Balance TBD by playtest.
    /// </summary>
    public static readonly int[] HordeRatePerRound = [0, 1, 1, 1, 2, 2, 2];

    /// <summary>
    /// Exit tile placed randomly in the last X fraction of the deck (post-deal);
    /// higher fraction = earlier exit for low counts (DD2, F7).
    /// Index = player count (1–6). Balance TBD by playtest.
    /// </summary>
    public static readonly double[] ExitBandFraction = [0.0, 0.50, 0.35, 0.30, 0.28, 0.26, 0.25];

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
