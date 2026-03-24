using System.Text.Json.Serialization;

namespace Meepliton.Games.FThat.Models;

// ── Phase ─────────────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FThatPhase { Playing, GameOver }

// ── Action types ──────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FThatActionType { Take, Pass }

// ── State (server-side canonical; never sent directly to clients) ──────────────

public record FThatState(
    FThatPhase          Phase,
    List<FThatPlayer>   Players,
    int                 CurrentPlayerIndex,
    List<int>           Deck,           // remaining face-down cards (23 at start)
    int                 FaceUpCard,     // current card on offer
    int                 ChipsOnCard,    // chips resting on the face-up card
    List<FThatScore>?   Scores,         // null during Playing; populated at GameOver
    List<string>?       Winners         // player IDs in seat order; null during Playing
);

public record FThatPlayer(
    string      Id,
    string      DisplayName,
    string?     AvatarUrl,
    int         SeatIndex,
    int         Chips,      // FULL server-side value; stripped in projected state for opponents
    List<int>   Cards       // collected cards, insertion order (not sorted)
);

public record FThatScore(
    string PlayerId,
    int    CardScore,   // sum of chain minimums
    int    Chips,       // chips remaining at game end
    int    Total        // cardScore - chips; lowest wins
);

// ── Actions ──────────────────────────────────────────────────────────────────

public record FThatAction(FThatActionType Type);

// ── Options ──────────────────────────────────────────────────────────────────

public record FThatOptions(int StartingChips = 11);

// ── Projected view (broadcast to each player) ────────────────────────────────

public record FThatView(
    FThatPhase              Phase,
    List<FThatPlayerView>   Players,
    int                     CurrentPlayerIndex,
    int                     DeckCount,      // deck.Count — card order not exposed
    int                     FaceUpCard,
    int                     ChipsOnCard,
    List<FThatScore>?       Scores,
    List<string>?           Winners
);

public record FThatPlayerView(
    string      Id,
    string      DisplayName,
    string?     AvatarUrl,
    int         SeatIndex,
    int         Chips,        // exact value for self; -1 for all opponents
    bool        ChipsHidden,  // false for self; true for all opponents
    List<int>   Cards         // always visible (collected cards are public)
);
