using System.Text.Json.Serialization;

namespace Meepliton.Games.Coup.Models;

// ── Platform read-only views (keyless, no migrations) ─────────────────────────

public record RoomView(string Id, string GameId, string HostId, string JoinCode, string Status);
public record RoomPlayerView(string Id, string RoomId, string UserId, int SeatIndex);
public record UserView(string Id, string DisplayName, string? AvatarUrl, string Email);

// ── Constants ─────────────────────────────────────────────────────────────────

public static class CoupConstants
{
    public const int MinPlayers = 2;
    public const int MaxPlayers = 6;
    public static readonly IReadOnlyList<string> Characters =
        ["Duke", "Assassin", "Captain", "Ambassador", "Contessa"];
}

// ── Phase ─────────────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CoupPhase
{
    Waiting,            // pre-game, waiting for StartGame
    AwaitingResponses,  // active player's turn (Pending=null) or waiting for responses (Pending!=null)
    InfluenceLoss,      // a player must choose which card to reveal
    Exchange,           // Ambassador player choosing cards to keep
    Finished            // game over
}

// ── PendingStep ───────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PendingStep
{
    ActionResponses, // waiting for players to challenge/block/pass the declared action
    BlockResponses   // waiting for players to challenge/pass the blocker's claim
}

// ── State ─────────────────────────────────────────────────────────────────────

public record CoupState(
    CoupPhase           Phase,
    List<CoupPlayer>    Players,
    List<string>        Deck,
    int                 ActivePlayerIndex,
    PendingAction?      Pending,
    string?             Winner
);

public record CoupPlayer(
    string              Id,
    string              DisplayName,
    string?             AvatarUrl,
    int                 SeatIndex,
    List<InfluenceCard> Influence,
    int                 Coins,
    bool                Active
);

public record InfluenceCard(string Character, bool Revealed);

public record PendingAction(
    string        ActionType,
    string        ActorId,
    string?       TargetId,
    PendingStep   Step,
    List<string>  PassedPlayers,
    string?       BlockerId,
    string?       ChallengerId,
    List<string>? ExchangeOptions,
    string?       InfluenceLossPlayerId
);

// ── Actions ───────────────────────────────────────────────────────────────────

public record CoupAction(
    string        Type,
    string?       TargetId        = null,
    string?       Character       = null,
    string?       InfluenceToLose = null,
    List<string>? KeepCards       = null
);
