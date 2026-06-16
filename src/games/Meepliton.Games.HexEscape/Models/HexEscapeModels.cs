using System.Text.Json.Serialization;

namespace Meepliton.Games.HexEscape.Models;

// ── Enums ─────────────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HexEscapePhase { Playing, GameOver }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HexEscapeOutcome { Escaped, Overrun }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HexTileType { Straight, Elbow, Tee, Cross, Deadend }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HexActionType { PlaceTile, RotateTile, Pass }

// ── Hex cell ─────────────────────────────────────────────────────────────────

/// <summary>
/// A tile placed on the hex board. "fixed" is a reserved keyword in C# so we
/// use JsonPropertyName to ensure the wire key is exactly "fixed".
/// </summary>
public record HexCell(
    HexTileType TileType,
    int Rotation,
    [property: JsonPropertyName("fixed")] bool Fixed
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
    List<string> Cells,                         // ALL valid board cell keys "q,r"
    List<string> Walls,                         // impassable cells
    Dictionary<string, HexCell> Grid,           // OCCUPIED cells only, key "q,r"
    List<string> SurvivorStartCells,
    string ExitCell,
    Dictionary<HexTileType, int> HandCounts,    // remaining count per tile type
    int ThreatCounter,
    int ThreatThreshold,
    int ConnectedSurvivors,
    int TotalSurvivors,
    List<int> SeatsActedThisRound,              // distinct set — use List with guard, NOT HashSet
    List<PlayerSlot> Players
);

// ── Actions ──────────────────────────────────────────────────────────────────

public record HexEscapeAction(
    HexActionType Type,
    string? Coord = null,
    HexTileType? TileType = null,
    int? Rotation = null
);

// ── Options ──────────────────────────────────────────────────────────────────

public record HexEscapeOptions(string? LevelId = null);

// ── Level definition ──────────────────────────────────────────────────────────

public record PrePlacedTile(string Coord, HexTileType TileType, int Rotation);

public record HexEscapeLevel(
    string Id,
    string Name,
    List<string> Cells,
    List<string> Walls,
    List<PrePlacedTile> PrePlacedTiles,
    List<string> SurvivorStartCells,
    string ExitCell,
    Dictionary<HexTileType, int> TileHandCounts,
    int ThreatThreshold
);
