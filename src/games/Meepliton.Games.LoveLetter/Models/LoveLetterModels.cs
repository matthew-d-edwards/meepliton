using System.Text.Json.Serialization;

namespace Meepliton.Games.LoveLetter.Models;

// ── Platform read-only views (keyless, no migrations) ─────────────────────────

public record RoomView(string Id, string GameId, string HostId, string JoinCode, string Status);
public record RoomPlayerView(string Id, string RoomId, string UserId, int SeatIndex);
public record UserView(string Id, string DisplayName, string? AvatarUrl, string Email);

// ── Constants ─────────────────────────────────────────────────────────────────

public static class LoveLetterConstants
{
    public const int MinPlayers = 2;
    public const int MaxPlayers = 4;

    /// <summary>Token win thresholds by player count.</summary>
    public static int TokensToWin(int playerCount) => playerCount switch
    {
        2 => 7,
        3 => 5,
        _ => 4  // 4 players
    };

    /// <summary>Card names in order of value (index+1 = value).</summary>
    public static readonly string[] CardNames =
    [
        "Guard",     // 1
        "Priest",    // 2
        "Baron",     // 3
        "Handmaid",  // 4
        "Prince",    // 5
        "King",      // 6
        "Countess",  // 7
        "Princess",  // 8
    ];

    /// <summary>Returns the numeric value (1–8) for a card name.</summary>
    public static int CardValue(string card) => card switch
    {
        "Guard"    => 1,
        "Priest"   => 2,
        "Baron"    => 3,
        "Handmaid" => 4,
        "Prince"   => 5,
        "King"     => 6,
        "Countess" => 7,
        "Princess" => 8,
        _          => 0
    };

    /// <summary>Full 16-card deck definition.</summary>
    public static List<string> BuildDeck()
    {
        var deck = new List<string>();
        // Guard x5, Priest x2, Baron x2, Handmaid x2, Prince x2, King x1, Countess x1, Princess x1
        for (int i = 0; i < 5; i++) deck.Add("Guard");
        for (int i = 0; i < 2; i++) deck.Add("Priest");
        for (int i = 0; i < 2; i++) deck.Add("Baron");
        for (int i = 0; i < 2; i++) deck.Add("Handmaid");
        for (int i = 0; i < 2; i++) deck.Add("Prince");
        deck.Add("King");
        deck.Add("Countess");
        deck.Add("Princess");
        return deck;
    }
}

// ── Phase ─────────────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LoveLetterPhase
{
    Waiting,   // lobby — awaiting StartGame
    Playing,   // round in progress
    RoundEnd,  // round finished; awaiting StartNextRound
    GameOver   // game finished; winner determined
}

// ── State ─────────────────────────────────────────────────────────────────────

public record LoveLetterState(
    LoveLetterPhase        Phase,
    List<LoveLetterPlayer> Players,
    List<string>           Deck,
    string?                SetAsideCard,
    List<string>           FaceUpSetAside,
    int                    CurrentPlayerIndex,
    int                    Round,
    RoundResult?           LastRoundResult,
    PriestReveal?          PendingPriestReveal,
    string?                Winner,
    int                    DeckSize
);

public record LoveLetterPlayer(
    string       Id,
    string       DisplayName,
    string?      AvatarUrl,
    int          SeatIndex,
    string?      HandCard,
    List<string> DiscardPile,
    int          Tokens,
    bool         Active,
    bool         Handmaid
);

public record RoundResult(
    List<string>           WinnerIds,
    string                 Reason,
    List<PlayerHandReveal> Reveals
);

public record PlayerHandReveal(string PlayerId, string? Card);

public record PriestReveal(
    string ViewerId,
    string TargetId,
    string Card
);

// ── Actions ──────────────────────────────────────────────────────────────────

public record LoveLetterAction(
    string  Type,
    string? CardPlayed  = null,
    string? TargetId    = null,
    string? GuessedCard = null
);
