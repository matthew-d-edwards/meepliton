namespace Meepliton.Games.LiarsDice.Models;

// ── Platform read-only views (keyless, no migrations) ─────────────────────────

public record RoomView(string Id, string GameId, string HostId, string JoinCode, string Status);
public record RoomPlayerView(string Id, string RoomId, string UserId, int SeatIndex);
public record UserView(string Id, string DisplayName, string? AvatarUrl, string Email);

// ── Phase ─────────────────────────────────────────────────────────────────────

public enum LiarsDicePhase
{
    Bidding,  // normal bidding in progress
    Reveal,   // Liar called; dice visible; result shown; waiting for StartNextRound
    Finished  // game over
}

// ── State ─────────────────────────────────────────────────────────────────────

public record LiarsDiceState(
    LiarsDicePhase   Phase,
    List<DicePlayer> Players,
    int              CurrentPlayerIndex,
    Bid?             CurrentBid,
    int              RoundNumber,
    bool             PalificoActive,      // is this a Palifico round?
    string?          LastChallengeResult, // human-readable result shown after reveal
    RevealSnapshot?  LastReveal,          // populated during Reveal phase, null otherwise
    string?          Winner               // userId set in Finished phase
);

public record DicePlayer(
    string    Id,
    string    DisplayName,
    string?   AvatarUrl,
    int       SeatIndex,
    List<int> Dice,           // each element 1–6; empty when eliminated
    int       DiceCount,      // always == Dice.Count for active players; 0 when eliminated
    bool      Active,         // false = eliminated
    bool      HasUsedPalifico
);

public record Bid(int Quantity, int Face);

public record RevealSnapshot(
    List<PlayerReveal> Players,
    Bid                ChallengedBid,
    int                ActualCount,  // dice matching the bid (accounting for wilds if applicable)
    string             LoserId       // userId of the player who lost a die this round
);

public record PlayerReveal(string PlayerId, List<int> Dice);

// ── Actions ──────────────────────────────────────────────────────────────────

public record LiarsDiceAction(
    string      Type,
    BidPayload? BidData         = null,
    bool?       DeclarePalifico = null
);

public record BidPayload(int Quantity, int Face);

// ── Options ──────────────────────────────────────────────────────────────────

public record LiarsDiceOptions(int StartingDice = 5);
