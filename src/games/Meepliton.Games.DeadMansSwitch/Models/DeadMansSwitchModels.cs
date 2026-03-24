using System.Text.Json.Serialization;

namespace Meepliton.Games.DeadMansSwitch.Models;

// ── Platform read-only views (keyless, no migrations) ─────────────────────────

public record RoomView(string Id, string GameId, string HostId, string JoinCode, string Status);
public record RoomPlayerView(string Id, string RoomId, string UserId, int SeatIndex);
public record UserView(string Id, string DisplayName, string? AvatarUrl, string Email);

// ── Phase ─────────────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeadMansSwitchPhase
{
    Placing,
    Bidding,
    Revealing,
    DiscardChoice,   // Challenger hit own skull; awaiting Challenger's DiscardDisc
    RoundOver,
    Finished
}

// ── Disc type ─────────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiscType { Rose, Skull }

// ── State ─────────────────────────────────────────────────────────────────────

public record DeadMansSwitchState(
    DeadMansSwitchPhase Phase,
    List<DevicePlayer>  Players,
    int                 CurrentPlayerIndex,        // whose turn it is in all active phases
    int                 CurrentBid,                // 0 when not in Bidding/Revealing
    int                 TotalDiscsOnTable,          // cached sum of all StackCounts
    string?             ChallengerId,              // set when phase enters Bidding
    int                 NextRoundFirstPlayerIndex, // index into Players
    FlipLog?            LastFlip,                  // last disc flipped (for animation)
    string?             Winner,                    // userId, set in Finished
    int                 RoundNumber
);

public record DevicePlayer(
    string         Id,
    string         DisplayName,
    string?        AvatarUrl,
    int            SeatIndex,
    List<DiscSlot> Stack,       // ordered top-to-bottom; projected to [] for opponents
    int            StackCount,  // always == Stack.Count; always visible to all
    int            RosesOwned,  // permanent rose count (decrements on discard)
    bool           SkullOwned,  // permanent skull ownership (false if skull discarded)
    int            PointsWon,   // 0, 1, or 2
    bool           Active,      // false = eliminated
    bool           Passed       // true during Bidding if this player passed
);

public record DiscSlot(
    DiscType Type,    // visible only to owner and after Challenger flips (Flipped = true)
    bool     Flipped  // true after Challenger reveals this disc
);

public record FlipLog(
    string   FlippedByPlayerId,
    string   StackOwnerId,
    DiscType Result,
    int      FlipNumber    // 1-indexed count toward bid
);

// ── Actions ──────────────────────────────────────────────────────────────────

public record DeadMansSwitchAction(
    string    Type,
    int?      TargetCount    = null,
    int?      NewBid         = null,
    string?   TargetPlayerId = null,
    DiscType? DiscType       = null
);
