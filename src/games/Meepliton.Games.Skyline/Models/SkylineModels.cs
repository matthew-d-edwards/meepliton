namespace Meepliton.Games.Skyline.Models;

// ── State ────────────────────────────────────────────────────────────────────

public record SkylineState(
    List<PlayerState> Players,
    List<List<int?>> Board,       // Board[row][col] — null = empty
    string CurrentPlayerId,
    SkylinePhase Phase,
    int Turn,
    string? WinnerId
);

public record PlayerState(
    string Id,
    string DisplayName,
    string? AvatarUrl,
    int SeatIndex,
    int Score,
    List<int> Hand        // tile values in hand
);

public enum SkylinePhase { PlacingTile, GameOver }

// ── Actions ──────────────────────────────────────────────────────────────────

public record SkylineAction(string Type, PlaceTilePayload? PlaceTile = null);
public record PlaceTilePayload(int Row, int Col, int TileValue);

// ── Supplementary DB tables ──────────────────────────────────────────────────

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

// ── Platform read-only views (no migrations generated) ───────────────────────

public record RoomView(string Id, string GameId, string HostId, string JoinCode, string Status);
public record RoomPlayerView(string Id, string RoomId, string UserId, int SeatIndex);
public record UserView(string Id, string DisplayName, string? AvatarUrl, string Email);
