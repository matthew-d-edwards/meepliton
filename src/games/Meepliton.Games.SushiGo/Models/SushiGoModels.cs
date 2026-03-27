using System.Text.Json.Serialization;

namespace Meepliton.Games.SushiGo.Models;

// ── Platform read-only views (keyless, no migrations) ─────────────────────────

public record RoomView(string Id, string GameId, string HostId, string JoinCode, string Status);
public record RoomPlayerView(string Id, string RoomId, string UserId, int SeatIndex);
public record UserView(string Id, string DisplayName, string? AvatarUrl, string Email);

// ── Card constants ─────────────────────────────────────────────────────────────

public static class SushiGoCards
{
    public const string Tempura      = "Tempura";
    public const string Sashimi      = "Sashimi";
    public const string Dumpling     = "Dumpling";
    public const string Maki1        = "Maki1";
    public const string Maki2        = "Maki2";
    public const string Maki3        = "Maki3";
    public const string SalmonNigiri = "SalmonNigiri";
    public const string SquidNigiri  = "SquidNigiri";
    public const string EggNigiri    = "EggNigiri";
    public const string Pudding      = "Pudding";
    public const string Wasabi       = "Wasabi";
    public const string Chopsticks   = "Chopsticks";

    // Chopsticks pick marker: "CHOP:{pick1}:{pick2}"
    public const string ChopsticsMarkerPrefix = "CHOP:";
}

// ── Phase ─────────────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SushiGoPhase
{
    Waiting,   // lobby, waiting for StartGame
    Picking,   // players choosing cards
    Revealing, // all picked, cards being revealed (transient — moved to Picking or Scoring immediately)
    Scoring,   // round complete, scores shown, waiting for AdvanceRound
    Finished   // game over
}

// ── State ─────────────────────────────────────────────────────────────────────

public record SushiGoState(
    SushiGoPhase         Phase,
    List<SushiGoPlayer>  Players,
    int                  Round,
    int                  Turn,
    List<string>         Deck,
    List<List<string>>   Hands,
    List<string?>        PendingPicks,   // null = not yet picked; card name or "CHOP:pick1:pick2"
    string?              Winner,
    List<int>?           HandSizes       // populated during projection; null in canonical state
);

public record SushiGoPlayer(
    string        Id,
    string        DisplayName,
    string?       AvatarUrl,
    int           SeatIndex,
    List<string>  Tableau,
    List<int>     RoundScores,
    int           PuddingCount,
    bool          HasPicked,
    bool          UsingChopsticks
);

// ── Actions ──────────────────────────────────────────────────────────────────

public record SushiGoAction(
    string  Type,
    string? Pick          = null,
    string? Pick2         = null,
    bool?   UseChopsticks = null
);
