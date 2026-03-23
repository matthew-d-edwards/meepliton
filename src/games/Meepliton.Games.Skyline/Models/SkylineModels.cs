namespace Meepliton.Games.Skyline.Models;

// ── State ─────────────────────────────────────────────────────────────────────

public record SkylineState(
    List<PlayerState> Players,
    int CurrentPlayer,              // index into Players
    Dictionary<string, string> Board, // tileId -> "neutral" | hotelName
    Dictionary<string, ChainState> Chains,
    Dictionary<string, int> StockBank,
    List<string> Bag,
    List<string> Log,
    bool GameOver,
    string? Winner,
    List<int>? RankedOrder,         // player indices sorted by net worth desc
    string Phase,                   // "place"|"found"|"merge"|"dispose"|"buy"|"draw"
    PendingState? Pending
);

public record PlayerState(
    string Id,
    string Name,
    string Color,
    int Cash,
    Dictionary<string, int> Stocks, // hotelName -> count
    List<string> Hand               // tileIds like "A1", "B7"
);

public record ChainState(
    bool Active,
    int Size,
    List<string> Tiles
);

public record PendingState(
    string Type,                    // "found" | "merge"
    // found
    List<string>? Tiles,
    string? Chosen,
    // merge
    string? Tid,
    List<string>? Hotels,
    List<string>? Survivors,
    string? Survivor,
    List<string>? Defunct,
    bool? SurvivorChosen,
    Dictionary<string, int>? DefunctSizes,
    // dispose
    List<DisposeQueueItem>? DisposeQueue,
    int? DisposeIdx,
    Dictionary<string, DisposeDecision>? DisposeDecisions
);

public record DisposeQueueItem(string Defunct, int PlayerIdx);

public record DisposeDecision(int Sell, int Trade);

// ── Actions ───────────────────────────────────────────────────────────────────

public record SkylineAction(
    string Type,                    // "PlaceTile"|"FoundHotel"|"ChooseSurvivor"|"ConfirmSurvivor"|"Dispose"|"BuyStocks"|"EndTurn"|"EndGame"
    string? TileId,
    string? Hotel,
    int Sell,
    int Trade,
    Dictionary<string, int>? Purchases
);

// ── Supplementary DB tables (unchanged) ──────────────────────────────────────

public record SkylineGameResult
{
    public long   Id          { get; init; }
    public string RoomId      { get; init; } = string.Empty;
    public Dictionary<string, int> FinalScores { get; init; } = new();
    public string? WinnerId   { get; init; }
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
}

public record SkylinePlayerStats
{
    public string Id          { get; init; } = Guid.NewGuid().ToString();
    public string UserId      { get; init; } = string.Empty;
    public int    GamesPlayed { get; init; }
    public int    GamesWon    { get; init; }
    public long   TotalScore  { get; init; }
    public DateTimeOffset? LastPlayedAt { get; init; }
}

// ── Platform read-only views (no migrations generated) ────────────────────────

public record RoomView(string Id, string GameId, string HostId, string JoinCode, string Status);
public record RoomPlayerView(string Id, string RoomId, string UserId, int SeatIndex);
public record UserView(string Id, string DisplayName, string? AvatarUrl, string Email);
