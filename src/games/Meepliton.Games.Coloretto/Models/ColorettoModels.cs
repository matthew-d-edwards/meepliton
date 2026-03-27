using System.Text.Json.Serialization;

namespace Meepliton.Games.Coloretto.Models;

// ── Platform read-only views (keyless, no migrations) ─────────────────────────

public record RoomView(string Id, string GameId, string HostId, string JoinCode, string Status);
public record RoomPlayerView(string Id, string RoomId, string UserId, int SeatIndex);
public record UserView(string Id, string DisplayName, string? AvatarUrl, string Email);

// ── Phase ─────────────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ColorettoPhase
{
    Waiting,
    Playing,
    Finished
}

// ── State ─────────────────────────────────────────────────────────────────────

public record ColorettoState(
    ColorettoPhase          Phase,
    List<ColorettoPlayer>   Players,
    List<string>            Deck,
    List<ColorettoRow>      Rows,
    int                     CurrentPlayerIndex,
    bool                    EndGameTriggered,
    RoundScoreResult?       FinalScores,
    string?                 Winner
);

public record ColorettoPlayer(
    string                   Id,
    string                   DisplayName,
    string?                  AvatarUrl,
    int                      SeatIndex,
    Dictionary<string, int>  Collection,   // colour -> count
    bool                     HasTakenThisRound
);

public record ColorettoRow(
    int          RowIndex,
    List<string> Cards
);

public record RoundScoreResult(
    List<PlayerScore> Scores
);

public record PlayerScore(
    string                  PlayerId,
    Dictionary<string, int> Collection,
    List<string>            TopColors,
    Dictionary<string, int> ColorScores,
    int                     Total
);

// ── Actions ───────────────────────────────────────────────────────────────────

public record ColorettoAction(
    string  Type,
    int?    RowIndex = null
);
