using System.Text.Json;
using Meepliton.Contracts;
using Meepliton.Games.FThat.Models;

namespace Meepliton.Games.FThat;

/// <summary>
/// F'That — card-avoidance game for 3–7 players (re-skin of No Thanks).
/// Players either take the face-up card (and its chips) or pass by paying
/// one chip. The deck runs out automatically, ending the game.
/// Lowest score wins.
/// </summary>
public class FThatModule : IGameModule, IGameHandler
{
    public string GameId      => "fthat";
    public string Name        => "F'That";
    public string Description => "Avoid terrible cards at all costs. Pay chips to pass — but you only have so many.";
    public int    MinPlayers  => 3;
    public int    MaxPlayers  => 7;
    public bool   AllowLateJoin => false;
    public bool   SupportsAsync => false;
    public bool   SupportsUndo  => false;
    public string? ThumbnailUrl => null;
    public bool HasStateProjection => true;

    // ── IGameHandler ──────────────────────────────────────────────────────────

    public GameResult Handle(GameContext ctx)
    {
        var state  = Deserialize<FThatState>(ctx.CurrentState);
        var action = Deserialize<FThatAction>(ctx.Action);
        var error  = Validate(state, action, ctx.PlayerId);
        if (error is not null) return new GameResult(ctx.CurrentState, error);
        var (newState, effects) = Apply(state, action);
        var newStateDoc = Serialize(newState);
        return new GameResult(newStateDoc, Effects: effects);
    }

    // ── Per-player state projection ───────────────────────────────────────────

    JsonDocument? IGameModule.ProjectStateForPlayer(JsonDocument fullState, string playerId)
    {
        var state = Deserialize<FThatState>(fullState);
        if (state is null) return null;
        var view = BuildView(state, playerId);
        return Serialize(view);
    }

    private static FThatView BuildView(FThatState state, string playerId)
    {
        var playerViews = state.Players.Select(p => new FThatPlayerView(
            Id:          p.Id,
            DisplayName: p.DisplayName,
            AvatarUrl:   p.AvatarUrl,
            SeatIndex:   p.SeatIndex,
            Chips:       p.Id == playerId ? p.Chips : -1,
            ChipsHidden: p.Id != playerId,
            Cards:       p.Cards
        )).ToList();

        return new FThatView(
            Phase:              state.Phase,
            Players:            playerViews,
            CurrentPlayerIndex: state.CurrentPlayerIndex,
            DeckCount:          state.Deck.Count,
            FaceUpCard:         state.FaceUpCard,
            ChipsOnCard:        state.ChipsOnCard,
            Scores:             state.Scores,
            Winners:            state.Winners
        );
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    JsonDocument IGameModule.CreateInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options)
    {
        var opts = options is null ? null : Deserialize<FThatOptions>(options);
        return Serialize(CreateInitialState(players, opts));
    }

    public FThatState CreateInitialState(IReadOnlyList<PlayerInfo> players, FThatOptions? options)
    {
        int startingChips = Math.Clamp(options?.StartingChips ?? 11, 7, 15);

        // Build full deck [3..35] = 33 cards
        var fullDeck = Enumerable.Range(3, 33).ToList(); // 3, 4, ..., 35

        // Shuffle
        Shuffle(fullDeck);

        // Remove 9 at random (discard, don't store)
        var playable = fullDeck.Skip(9).ToList(); // 24 cards remain

        // First card = faceUpCard; remaining 23 = deck
        int faceUpCard = playable[0];
        var deck = playable.Skip(1).ToList(); // 23 cards

        var gamePlayers = players.Select(p => new FThatPlayer(
            Id:          p.Id,
            DisplayName: p.DisplayName,
            AvatarUrl:   p.AvatarUrl,
            SeatIndex:   p.SeatIndex,
            Chips:       startingChips,
            Cards:       []
        )).ToList();

        return new FThatState(
            Phase:              FThatPhase.Playing,
            Players:            gamePlayers,
            CurrentPlayerIndex: 0,
            Deck:               deck,
            FaceUpCard:         faceUpCard,
            ChipsOnCard:        0,
            Scores:             null,
            Winners:            null
        );
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    public string? Validate(FThatState state, FThatAction action, string playerId)
    {
        if (state.Phase == FThatPhase.GameOver)
            return "The game is over.";

        var currentPlayer = state.Players[state.CurrentPlayerIndex];
        if (currentPlayer.Id != playerId)
            return "It is not your turn.";

        if (action.Type == FThatActionType.Pass && currentPlayer.Chips <= 0)
            return "You have no chips — you must take the card.";

        return null;
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    public (FThatState State, GameEffect[] Effects) Apply(FThatState state, FThatAction action) =>
        action.Type switch
        {
            FThatActionType.Take => ApplyTake(state),
            FThatActionType.Pass => (ApplyPass(state), []),
            _                   => (state, [])
        };

    // ── Take ──────────────────────────────────────────────────────────────────

    private static (FThatState State, GameEffect[] Effects) ApplyTake(FThatState state)
    {
        var currentPlayer = state.Players[state.CurrentPlayerIndex];

        // Add faceUpCard to player's cards; add chipsOnCard to player's chips
        var updatedCards = new List<int>(currentPlayer.Cards) { state.FaceUpCard };
        var updatedPlayer = currentPlayer with
        {
            Cards = updatedCards,
            Chips = currentPlayer.Chips + state.ChipsOnCard
        };

        var players = ReplacePlayer(state.Players, updatedPlayer);

        // Check if deck is empty → GameOver
        if (state.Deck.Count == 0)
        {
            return HandleGameOver(state with { Players = players });
        }

        // Advance: deck[0] → faceUpCard; remove from deck; chipsOnCard = 0; turn stays
        var newFaceUpCard = state.Deck[0];
        var newDeck = state.Deck.Skip(1).ToList();

        return (state with
        {
            Players     = players,
            Deck        = newDeck,
            FaceUpCard  = newFaceUpCard,
            ChipsOnCard = 0
            // currentPlayerIndex stays — turn remains with same player after taking
        }, []);
    }

    private static (FThatState State, GameEffect[] Effects) HandleGameOver(FThatState state)
    {
        var scores = state.Players
            .Select(p => ComputeScore(p))
            .ToList();

        int lowestTotal = scores.Min(s => s.Total);
        var winners = scores
            .Where(s => s.Total == lowestTotal)
            .Select(s => s.PlayerId)
            .OrderBy(id => state.Players.First(p => p.Id == id).SeatIndex)
            .ToList();

        var lowestSeatWinnerId = winners.First();

        var newState = state with
        {
            Phase   = FThatPhase.GameOver,
            Scores  = scores,
            Winners = winners
        };

        return (newState, [new GameOverEffect(lowestSeatWinnerId)]);
    }

    // ── Pass ──────────────────────────────────────────────────────────────────

    private static FThatState ApplyPass(FThatState state)
    {
        var currentPlayer = state.Players[state.CurrentPlayerIndex];
        var updatedPlayer = currentPlayer with { Chips = currentPlayer.Chips - 1 };
        var players = ReplacePlayer(state.Players, updatedPlayer);

        int nextIndex = (state.CurrentPlayerIndex + 1) % players.Count;

        return state with
        {
            Players            = players,
            ChipsOnCard        = state.ChipsOnCard + 1,
            CurrentPlayerIndex = nextIndex
        };
    }

    // ── Scoring ───────────────────────────────────────────────────────────────

    private static FThatScore ComputeScore(FThatPlayer player)
    {
        if (player.Cards.Count == 0)
            return new FThatScore(player.Id, 0, player.Chips, -player.Chips);

        // Sort a copy — never mutate stored list
        var sorted = player.Cards.ToList();
        sorted.Sort();

        int cardScore = 0;
        // Sum the lowest card of each consecutive run
        for (int i = 0; i < sorted.Count; i++)
        {
            // A card starts a new run if it's the first, or not consecutive with previous
            if (i == 0 || sorted[i] != sorted[i - 1] + 1)
            {
                cardScore += sorted[i];
            }
        }

        int total = cardScore - player.Chips;
        return new FThatScore(player.Id, cardScore, player.Chips, total);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<FThatPlayer> ReplacePlayer(List<FThatPlayer> players, FThatPlayer updated)
    {
        var result = players.ToList();
        var idx = result.FindIndex(p => p.Id == updated.Id);
        if (idx >= 0) result[idx] = updated;
        return result;
    }

    private static void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        for (int i = n - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ── Serialization helpers ────────────────────────────────────────────────

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static T Deserialize<T>(JsonDocument doc) =>
        JsonSerializer.Deserialize<T>(doc.RootElement.GetRawText(), SerializerOptions)!;

    private static JsonDocument Serialize<T>(T obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj, SerializerOptions));
}
