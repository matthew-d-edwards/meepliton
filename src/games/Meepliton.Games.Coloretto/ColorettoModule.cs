using System.Text.Json;
using Meepliton.Contracts;
using Meepliton.Games.Coloretto.Models;

namespace Meepliton.Games.Coloretto;

/// <summary>
/// Chameleon Market (Coloretto) — card-drafting game for 2–5 players.
/// Players take turns drawing cards into rows or taking a row to add to their collection.
/// Top 3 colour groups score positively; all others score negatively.
/// The game ends after the round in which the EndGame card is drawn.
/// </summary>
public class ColorettoModule : IGameModule, IGameHandler
{
    // ── Metadata ──────────────────────────────────────────────────────────────

    public string  GameId        => "coloretto";
    public string  Name          => "Chameleon Market";
    public string  Description   => "Collect wisely. Everything else costs you.";
    public int     MinPlayers    => 2;
    public int     MaxPlayers    => 5;
    public bool    AllowLateJoin => false;
    public bool    SupportsAsync => true;
    public bool    SupportsUndo  => false;
    public string? ThumbnailUrl  => null;
    public bool    HasStateProjection => true;

    // ── Colour definitions ────────────────────────────────────────────────────

    // All possible colours in order of activation
    private static readonly string[] AllColors = ["Brown", "Blue", "Green", "Orange", "Purple", "Red", "Yellow"];

    private static IReadOnlyList<string> ActiveColors(int playerCount) => playerCount switch
    {
        2 => AllColors[..3],
        3 => AllColors[..5],
        4 => AllColors[..6],
        _ => AllColors[..7]  // 5 players
    };

    private static int RowCount(int playerCount) => playerCount + 1;

    // ── IGameHandler ──────────────────────────────────────────────────────────

    public GameResult Handle(GameContext ctx)
    {
        var state  = Deserialize<ColorettoState>(ctx.CurrentState);
        var action = Deserialize<ColorettoAction>(ctx.Action);
        var error  = Validate(state, action, ctx.PlayerId);
        if (error is not null) return new GameResult(ctx.CurrentState, error);
        var newState    = Apply(state, action, ctx.PlayerId);
        var newStateDoc = Serialize(newState);
        if (newState.Phase == ColorettoPhase.Finished && newState.Winner is not null)
            return new GameResult(newStateDoc, Effects: [new GameOverEffect(newState.Winner)]);
        // If it's a draw (Winner==null, Phase==Finished) still signal game over using first player
        if (newState.Phase == ColorettoPhase.Finished)
            return new GameResult(newStateDoc, Effects: [new GameOverEffect(newState.Players[0].Id)]);
        return new GameResult(newStateDoc);
    }

    // ── Per-player state projection ───────────────────────────────────────────

    JsonDocument? IGameModule.ProjectStateForPlayer(JsonDocument fullState, string playerId)
    {
        var state = Deserialize<ColorettoState>(fullState);
        if (state is null) return null;
        var projected = new ColorettoProjectedState(
            Phase:               state.Phase,
            Players:             state.Players,
            Deck:                [],
            DeckSize:            state.Deck.Count,
            Rows:                state.Rows,
            CurrentPlayerIndex:  state.CurrentPlayerIndex,
            EndGameTriggered:    state.EndGameTriggered,
            FinalScores:         state.FinalScores,
            Winner:              state.Winner
        );
        return Serialize(projected);
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    JsonDocument IGameModule.CreateInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options)
        => Serialize(CreateInitialState(players));

    public ColorettoState CreateInitialState(IReadOnlyList<PlayerInfo> players)
    {
        var colorettoPlayers = players
            .OrderBy(p => p.SeatIndex)
            .Select(p => new ColorettoPlayer(
                Id:                p.Id,
                DisplayName:       p.DisplayName,
                AvatarUrl:         p.AvatarUrl,
                SeatIndex:         p.SeatIndex,
                Collection:        new Dictionary<string, int>(),
                HasTakenThisRound: false
            )).ToList();

        return new ColorettoState(
            Phase:               ColorettoPhase.Waiting,
            Players:             colorettoPlayers,
            Deck:                [],
            Rows:                [],
            CurrentPlayerIndex:  0,
            EndGameTriggered:    false,
            FinalScores:         null,
            Winner:              null
        );
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    public string? Validate(ColorettoState state, ColorettoAction action, string playerId)
    {
        if (state.Phase == ColorettoPhase.Finished)
            return "The game is over.";

        switch (action.Type)
        {
            case "StartGame":
            {
                if (state.Phase != ColorettoPhase.Waiting)
                    return "The game has already started.";
                return null;
            }

            case "DrawCard":
            {
                if (state.Phase != ColorettoPhase.Playing)
                    return "The game has not started yet.";
                if (!IsCurrentPlayer(state, playerId))
                    return "It is not your turn.";
                var player = state.Players.First(p => p.Id == playerId);
                if (player.HasTakenThisRound)
                    return "You have already taken a row this round.";
                if (action.RowIndex is null)
                    return "Missing rowIndex.";
                if (action.RowIndex < 0 || action.RowIndex >= state.Rows.Count)
                    return "Invalid rowIndex.";
                var targetRow = state.Rows[action.RowIndex.Value];
                if (targetRow.Cards.Count >= 3)
                    return "That row already has 3 cards.";
                if (state.Deck.Count == 0)
                    return "The deck is empty.";
                // Check all rows already have 3 cards (no DrawCard possible anywhere)
                if (state.Rows.All(r => r.Cards.Count >= 3))
                    return "All rows are full. You must take a row.";
                return null;
            }

            case "TakeRow":
            {
                if (state.Phase != ColorettoPhase.Playing)
                    return "The game has not started yet.";
                if (!IsCurrentPlayer(state, playerId))
                    return "It is not your turn.";
                var player = state.Players.First(p => p.Id == playerId);
                if (player.HasTakenThisRound)
                    return "You have already taken a row this round.";
                if (action.RowIndex is null)
                    return "Missing rowIndex.";
                if (action.RowIndex < 0 || action.RowIndex >= state.Rows.Count)
                    return "Invalid rowIndex.";
                return null;
            }

            default:
                return $"Unknown action type: {action.Type}";
        }
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    public ColorettoState Apply(ColorettoState state, ColorettoAction action, string playerId) =>
        action.Type switch
        {
            "StartGame" => ApplyStartGame(state),
            "DrawCard"  => ApplyDrawCard(state, action.RowIndex!.Value),
            "TakeRow"   => ApplyTakeRow(state, action.RowIndex!.Value),
            _           => state
        };

    // ── StartGame ─────────────────────────────────────────────────────────────

    private static ColorettoState ApplyStartGame(ColorettoState state)
    {
        var playerCount = state.Players.Count;
        var colors      = ActiveColors(playerCount);
        var rowCount    = RowCount(playerCount);

        // Build deck: 9 of each active colour + 3 Jokers + 1 EndGame
        var deck = new List<string>();
        foreach (var color in colors)
            for (int i = 0; i < 9; i++)
                deck.Add(color);
        for (int i = 0; i < 3; i++)
            deck.Add("Joker");
        deck.Add("EndGame");

        // Shuffle
        Shuffle(deck);

        // Build empty rows
        var rows = Enumerable.Range(0, rowCount)
            .Select(i => new ColorettoRow(i, []))
            .ToList();

        return state with
        {
            Phase              = ColorettoPhase.Playing,
            Deck               = deck,
            Rows               = rows,
            CurrentPlayerIndex = 0
        };
    }

    // ── DrawCard ──────────────────────────────────────────────────────────────

    private static ColorettoState ApplyDrawCard(ColorettoState state, int rowIndex)
    {
        // Pop top card
        var deck = state.Deck.ToList();
        var card = deck[^1];
        deck.RemoveAt(deck.Count - 1);

        bool endGameTriggered = state.EndGameTriggered;
        if (card == "EndGame")
            endGameTriggered = true;

        // Add card to row (EndGame card included as slot per spec)
        var rows = state.Rows.Select((r, i) =>
            i == rowIndex ? r with { Cards = [..r.Cards, card] } : r
        ).ToList();

        // Advance to next player who hasn't taken this round
        var nextIndex = NextAvailablePlayerIndex(state.Players, state.CurrentPlayerIndex);

        return state with
        {
            Deck               = deck,
            Rows               = rows,
            CurrentPlayerIndex = nextIndex,
            EndGameTriggered   = endGameTriggered
        };
    }

    // ── TakeRow ───────────────────────────────────────────────────────────────

    private static ColorettoState ApplyTakeRow(ColorettoState state, int rowIndex)
    {
        var row = state.Rows[rowIndex];

        // Add colour cards to player's collection (skip EndGame card)
        var players = state.Players.Select(p =>
        {
            if (p.Id != state.Players[state.CurrentPlayerIndex].Id) return p;

            var collection = new Dictionary<string, int>(p.Collection);
            foreach (var card in row.Cards)
            {
                if (card == "EndGame") continue; // EndGame card has no collection value
                collection[card] = collection.GetValueOrDefault(card, 0) + 1;
            }
            return p with
            {
                Collection        = collection,
                HasTakenThisRound = true
            };
        }).ToList();

        // Clear the taken row
        var rows = state.Rows.Select((r, i) =>
            i == rowIndex ? r with { Cards = [] } : r
        ).ToList();

        // Advance to next player who hasn't taken this round
        var nextIndex = NextAvailablePlayerIndex(players, state.CurrentPlayerIndex);

        var updatedState = state with
        {
            Players            = players,
            Rows               = rows,
            CurrentPlayerIndex = nextIndex
        };

        // Check if all players have taken
        bool allTaken = players.All(p => p.HasTakenThisRound);
        if (!allTaken)
            return updatedState;

        // End of round — reset HasTakenThisRound
        var resetPlayers = players.Select(p => p with { HasTakenThisRound = false }).ToList();

        if (state.EndGameTriggered)
        {
            // Score and finish
            var finalScores = ComputeScores(resetPlayers, state.Players.Count);
            var winner      = DetermineWinner(finalScores);

            return updatedState with
            {
                Players            = resetPlayers,
                Phase              = ColorettoPhase.Finished,
                FinalScores        = finalScores,
                Winner             = winner,
                CurrentPlayerIndex = 0
            };
        }

        // New round — rows already cleared above, just reset players
        return updatedState with
        {
            Players            = resetPlayers,
            CurrentPlayerIndex = 0
        };
    }

    // ── Scoring ───────────────────────────────────────────────────────────────

    private static readonly int[] ScoreTable = [0, 1, 3, 6, 10, 15, 21, 28, 36, 45];

    private static int ScoreForCount(int count)
        => count >= 0 && count < ScoreTable.Length ? ScoreTable[count] : ScoreTable[^1];

    private static RoundScoreResult ComputeScores(List<ColorettoPlayer> players, int playerCount)
    {
        var activeColors = ActiveColors(playerCount);
        var scores = players.Select(p => ScorePlayer(p, activeColors)).ToList();
        return new RoundScoreResult(scores);
    }

    private static PlayerScore ScorePlayer(ColorettoPlayer player, IReadOnlyList<string> activeColors)
    {
        // Start with a mutable copy of the collection (only active colours)
        var collection = new Dictionary<string, int>(
            activeColors.ToDictionary(c => c, c => player.Collection.GetValueOrDefault(c, 0)));

        int jokerCount = player.Collection.GetValueOrDefault("Joker", 0);

        // Assign jokers greedily: each joker goes to the colour that gives maximum marginal gain
        for (int j = 0; j < jokerCount; j++)
        {
            string? bestColor = null;
            int     bestGain  = int.MinValue;

            foreach (var color in activeColors)
            {
                int current = collection[color];
                int gain    = ScoreForCount(current + 1) - ScoreForCount(current);

                if (gain > bestGain || (gain == bestGain && bestColor is not null &&
                    string.Compare(color, bestColor, StringComparison.Ordinal) < 0))
                {
                    bestGain  = gain;
                    bestColor = color;
                }
            }

            if (bestColor is not null)
                collection[bestColor]++;
        }

        // Identify top 3 colours by count (descending), ties broken alphabetically
        var sortedColors = activeColors
            .OrderByDescending(c => collection[c])
            .ThenBy(c => c)
            .ToList();

        var topColors  = sortedColors.Take(3).ToList();
        var restColors = sortedColors.Skip(3).ToList();

        // Score each colour
        var colorScores = new Dictionary<string, int>();
        int total = 0;

        foreach (var color in topColors)
        {
            int score = ScoreForCount(collection[color]);
            colorScores[color] = score;
            total += score;
        }

        foreach (var color in restColors)
        {
            int score = -ScoreForCount(collection[color]);
            colorScores[color] = score;
            total += score;
        }

        return new PlayerScore(
            PlayerId:    player.Id,
            Collection:  new Dictionary<string, int>(collection),
            TopColors:   topColors,
            ColorScores: colorScores,
            Total:       total
        );
    }

    private static string? DetermineWinner(RoundScoreResult scores)
    {
        if (scores.Scores.Count == 0) return null;
        int maxScore = scores.Scores.Max(s => s.Total);
        var winners  = scores.Scores.Where(s => s.Total == maxScore).ToList();
        return winners.Count == 1 ? winners[0].PlayerId : null; // null = draw
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsCurrentPlayer(ColorettoState state, string playerId) =>
        state.Players[state.CurrentPlayerIndex].Id == playerId;

    /// <summary>
    /// Returns the index of the next player who has NOT taken this round.
    /// Starts searching from currentIndex + 1 (wrapping). If none found, returns 0.
    /// </summary>
    private static int NextAvailablePlayerIndex(List<ColorettoPlayer> players, int currentIndex)
    {
        int total = players.Count;
        for (int i = 1; i <= total; i++)
        {
            int idx = (currentIndex + i) % total;
            if (!players[idx].HasTakenThisRound) return idx;
        }
        return 0; // all have taken — round-end case
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ── Serialization helpers ─────────────────────────────────────────────────

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

// ── Projected state (adds DeckSize) ─────────────────────────────────────────

/// <summary>
/// Wrapper sent to clients — Deck is hidden, DeckSize is exposed instead.
/// </summary>
file record ColorettoProjectedState(
    ColorettoPhase        Phase,
    List<ColorettoPlayer> Players,
    List<string>          Deck,
    int                   DeckSize,
    List<ColorettoRow>    Rows,
    int                   CurrentPlayerIndex,
    bool                  EndGameTriggered,
    RoundScoreResult?     FinalScores,
    string?               Winner
);
