using System.Text.Json;
using Meepliton.Contracts;
using Meepliton.Games.SushiGo.Models;

namespace Meepliton.Games.SushiGo;

/// <summary>
/// The Sushi Train — a card-drafting game for 2–5 players.
/// Players simultaneously pick one card per turn, then pass their hands left.
/// After 3 rounds the player with the most points wins.
/// </summary>
public class SushiGoModule : IGameModule, IGameHandler
{
    public string  GameId        => "sushigo";
    public string  Name          => "The Sushi Train";
    public string  Description   => "Pick fast, pass left, score big.";
    public int     MinPlayers    => 2;
    public int     MaxPlayers    => 5;
    public bool    AllowLateJoin => false;
    public bool    SupportsAsync => false;
    public bool    SupportsUndo  => false;
    public string? ThumbnailUrl  => null;
    public bool    HasStateProjection => true;

    // ── IGameHandler ──────────────────────────────────────────────────────────

    public GameResult Handle(GameContext ctx)
    {
        var state  = Deserialize<SushiGoState>(ctx.CurrentState);
        var action = Deserialize<SushiGoAction>(ctx.Action);
        var error  = Validate(state, action, ctx.PlayerId);
        if (error is not null) return new GameResult(ctx.CurrentState, error);
        var newState    = Apply(state, action, ctx.PlayerId);
        var newStateDoc = Serialize(newState);
        if (newState.Phase == SushiGoPhase.Finished && newState.Winner is not null)
            return new GameResult(newStateDoc, Effects: [new GameOverEffect(newState.Winner)]);
        return new GameResult(newStateDoc);
    }

    // ── IGameModule.ProjectStateForPlayer ─────────────────────────────────────

    JsonDocument? IGameModule.ProjectStateForPlayer(JsonDocument fullState, string playerId)
    {
        var state = Deserialize<SushiGoState>(fullState);
        if (state is null) return null;
        var projected = ProjectForPlayer(state, playerId);
        return Serialize(projected);
    }

    private static SushiGoState ProjectForPlayer(SushiGoState state, string playerId)
    {
        // Compute hand sizes from actual hands before masking
        var handSizes = state.Hands.Select(h => h.Count).ToList();

        // Mask other players' hands; strip deck entirely
        var projectedHands = state.Hands.Select((hand, i) =>
            state.Players[i].Id == playerId ? hand : new List<string>()
        ).ToList();

        return state with
        {
            Deck      = [],
            Hands     = projectedHands,
            HandSizes = handSizes
        };
    }

    // ── IGameModule.CreateInitialState ────────────────────────────────────────

    JsonDocument IGameModule.CreateInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options)
        => Serialize(CreateInitialState(players));

    public SushiGoState CreateInitialState(IReadOnlyList<PlayerInfo> players)
    {
        var playerList = players.Select(p => new SushiGoPlayer(
            Id:              p.Id,
            DisplayName:     p.DisplayName,
            AvatarUrl:       p.AvatarUrl,
            SeatIndex:       p.SeatIndex,
            Tableau:         [],
            RoundScores:     [],
            PuddingCount:    0,
            HasPicked:       false,
            UsingChopsticks: false
        )).ToList();

        var pendingPicks = playerList.Select(_ => (string?)null).ToList();

        return new SushiGoState(
            Phase:        SushiGoPhase.Waiting,
            Players:      playerList,
            Round:        0,
            Turn:         0,
            Deck:         [],
            Hands:        playerList.Select(_ => new List<string>()).ToList(),
            PendingPicks: pendingPicks,
            Winner:       null,
            HandSizes:    null
        );
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    public string? Validate(SushiGoState state, SushiGoAction action, string playerId)
    {
        if (state.Phase == SushiGoPhase.Finished)
            return "The game is over.";

        switch (action.Type)
        {
            case "StartGame":
            {
                if (state.Phase != SushiGoPhase.Waiting)
                    return "Game has already started.";
                var host = state.Players.FirstOrDefault(p => p.SeatIndex == 0);
                if (host?.Id != playerId)
                    return "Only the host can start the game.";
                return null;
            }

            case "PickCard":
            {
                if (state.Phase != SushiGoPhase.Picking)
                    return "You can only pick a card during the Picking phase.";
                var (playerIdx, player) = FindPlayer(state, playerId);
                if (player is null) return "You are not in this game.";
                if (player.HasPicked) return "You have already picked a card this turn.";
                if (action.Pick is null) return "Missing card pick.";
                var hand = state.Hands[playerIdx];
                if (!hand.Contains(action.Pick))
                    return $"Card '{action.Pick}' is not in your hand.";
                return null;
            }

            case "UseChopsticks":
            {
                if (state.Phase != SushiGoPhase.Picking)
                    return "You can only use chopsticks during the Picking phase.";
                var (playerIdx, player) = FindPlayer(state, playerId);
                if (player is null) return "You are not in this game.";
                if (player.HasPicked) return "You have already picked a card this turn.";
                if (!player.Tableau.Contains(SushiGoCards.Chopsticks))
                    return "You do not have Chopsticks in your tableau.";
                var hand = state.Hands[playerIdx];
                if (hand.Count < 2)
                    return "You need at least 2 cards in your hand to use Chopsticks.";
                if (action.Pick is null || action.Pick2 is null)
                    return "UseChopsticks requires both pick and pick2.";
                if (action.Pick == action.Pick2)
                {
                    var sameCount = hand.Count(c => c == action.Pick);
                    if (sameCount < 2) return $"You only have one '{action.Pick}' in your hand.";
                }
                else
                {
                    if (!hand.Contains(action.Pick))
                        return $"Card '{action.Pick}' is not in your hand.";
                    if (!hand.Contains(action.Pick2))
                        return $"Card '{action.Pick2}' is not in your hand.";
                }
                return null;
            }

            case "AdvanceRound":
            {
                if (state.Phase != SushiGoPhase.Scoring)
                    return "AdvanceRound is only valid during the Scoring phase.";
                var host = state.Players.FirstOrDefault(p => p.SeatIndex == 0);
                if (host?.Id != playerId)
                    return "Only the host can advance the round.";
                return null;
            }

            default:
                return $"Unknown action type: '{action.Type}'.";
        }
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    public SushiGoState Apply(SushiGoState state, SushiGoAction action, string playerId) =>
        action.Type switch
        {
            "StartGame"     => ApplyStartGame(state),
            "PickCard"      => ApplyPickCard(state, action, playerId),
            "UseChopsticks" => ApplyUseChopsticks(state, action, playerId),
            "AdvanceRound"  => ApplyAdvanceRound(state),
            _               => state
        };

    // ── StartGame ─────────────────────────────────────────────────────────────

    private static SushiGoState ApplyStartGame(SushiGoState state)
    {
        var deck  = BuildShuffledDeck();
        var hands = DealHands(deck, state.Players.Count, out var remainingDeck);
        var pendingPicks = state.Players.Select(_ => (string?)null).ToList();

        return state with
        {
            Phase        = SushiGoPhase.Picking,
            Round        = 1,
            Turn         = 1,
            Deck         = remainingDeck,
            Hands        = hands,
            PendingPicks = pendingPicks,
            Players      = state.Players.Select(p => p with
            {
                Tableau         = [],
                RoundScores     = [],
                PuddingCount    = 0,
                HasPicked       = false,
                UsingChopsticks = false
            }).ToList()
        };
    }

    // ── PickCard ──────────────────────────────────────────────────────────────

    private static SushiGoState ApplyPickCard(SushiGoState state, SushiGoAction action, string playerId)
    {
        var (playerIdx, _) = FindPlayer(state, playerId);
        var pick = action.Pick!;

        var newPendingPicks = state.PendingPicks.ToList();
        newPendingPicks[playerIdx] = pick;

        var newPlayers = state.Players.Select((p, i) =>
            i == playerIdx ? p with { HasPicked = true } : p
        ).ToList();

        var newState = state with { PendingPicks = newPendingPicks, Players = newPlayers };

        if (newPlayers.All(p => p.HasPicked))
            return ResolveTurn(newState);

        return newState;
    }

    // ── UseChopsticks ─────────────────────────────────────────────────────────

    private static SushiGoState ApplyUseChopsticks(SushiGoState state, SushiGoAction action, string playerId)
    {
        var (playerIdx, _) = FindPlayer(state, playerId);
        var marker = $"{SushiGoCards.ChopsticsMarkerPrefix}{action.Pick}:{action.Pick2}";

        var newPendingPicks = state.PendingPicks.ToList();
        newPendingPicks[playerIdx] = marker;

        var newPlayers = state.Players.Select((p, i) =>
            i == playerIdx ? p with { HasPicked = true, UsingChopsticks = true } : p
        ).ToList();

        var newState = state with { PendingPicks = newPendingPicks, Players = newPlayers };

        if (newPlayers.All(p => p.HasPicked))
            return ResolveTurn(newState);

        return newState;
    }

    // ── ResolveTurn ───────────────────────────────────────────────────────────

    /// <summary>
    /// All players have picked. Move cards to tableaux, pass hands left,
    /// then either start the next turn or move to Scoring.
    /// </summary>
    private static SushiGoState ResolveTurn(SushiGoState state)
    {
        int playerCount = state.Players.Count;
        var newHands   = state.Hands.Select(h => h.ToList()).ToList();
        var newPlayers = state.Players.ToList();

        for (int i = 0; i < playerCount; i++)
        {
            var pick    = state.PendingPicks[i]!;
            var hand    = newHands[i];
            var tableau = newPlayers[i].Tableau.ToList();

            if (pick.StartsWith(SushiGoCards.ChopsticsMarkerPrefix))
            {
                // Parse "CHOP:pick1:pick2"
                var parts = pick[SushiGoCards.ChopsticsMarkerPrefix.Length..].Split(':', 2);
                var c1 = parts[0];
                var c2 = parts[1];

                hand.Remove(c1);
                hand.Remove(c2);
                tableau.Add(c1);
                tableau.Add(c2);

                // Return Chopsticks from tableau back to hand before passing
                var chopIdx = tableau.IndexOf(SushiGoCards.Chopsticks);
                if (chopIdx >= 0)
                {
                    tableau.RemoveAt(chopIdx);
                    hand.Add(SushiGoCards.Chopsticks);
                }
            }
            else
            {
                hand.Remove(pick);
                tableau.Add(pick);
            }

            newHands[i]   = hand;
            newPlayers[i] = newPlayers[i] with
            {
                Tableau         = tableau,
                HasPicked       = false,
                UsingChopsticks = false
            };
        }

        // Pass hands left: player i receives the hand from player (i+1) % playerCount
        var passedHands = new List<List<string>>(playerCount);
        for (int i = 0; i < playerCount; i++)
            passedHands.Add(newHands[(i + 1) % playerCount]);

        var pendingPicks = newPlayers.Select(_ => (string?)null).ToList();
        bool handsEmpty  = passedHands.All(h => h.Count == 0);

        if (handsEmpty)
        {
            var scoredPlayers = ScoreRound(newPlayers);
            bool gameOver     = state.Round >= 3;

            if (gameOver)
            {
                scoredPlayers = ApplyPuddingScoring(scoredPlayers);
                var winnerId  = DetermineWinner(scoredPlayers);
                return state with
                {
                    Phase        = SushiGoPhase.Finished,
                    Players      = scoredPlayers,
                    Hands        = passedHands,
                    PendingPicks = pendingPicks,
                    Turn         = state.Turn + 1,
                    Winner       = winnerId
                };
            }

            return state with
            {
                Phase        = SushiGoPhase.Scoring,
                Players      = scoredPlayers,
                Hands        = passedHands,
                PendingPicks = pendingPicks,
                Turn         = state.Turn + 1
            };
        }

        // Next turn in the same round
        return state with
        {
            Phase        = SushiGoPhase.Picking,
            Players      = newPlayers,
            Hands        = passedHands,
            PendingPicks = pendingPicks,
            Turn         = state.Turn + 1
        };
    }

    // ── AdvanceRound ──────────────────────────────────────────────────────────

    private static SushiGoState ApplyAdvanceRound(SushiGoState state)
    {
        int playerCount = state.Players.Count;
        var deck        = BuildShuffledDeck();
        var hands       = DealHands(deck, playerCount, out var remainingDeck);
        var pendingPicks = state.Players.Select(_ => (string?)null).ToList();

        // Clear tableaux; pudding totals are preserved in PuddingCount
        var newPlayers = state.Players.Select(p => p with
        {
            Tableau         = [],
            HasPicked       = false,
            UsingChopsticks = false
        }).ToList();

        return state with
        {
            Phase        = SushiGoPhase.Picking,
            Round        = state.Round + 1,
            Turn         = 1,
            Deck         = remainingDeck,
            Hands        = hands,
            PendingPicks = pendingPicks,
            Players      = newPlayers
        };
    }

    // ── ScoreRound ────────────────────────────────────────────────────────────

    private static List<SushiGoPlayer> ScoreRound(List<SushiGoPlayer> players)
    {
        int playerCount = players.Count;
        var roundScores = new int[playerCount];

        for (int i = 0; i < playerCount; i++)
        {
            var tableau = players[i].Tableau;
            roundScores[i] += ScoreTempura(tableau);
            roundScores[i] += ScoreSashimi(tableau);
            roundScores[i] += ScoreDumpling(tableau);
            roundScores[i] += ScoreNigiri(tableau);
            // Pudding not scored per-round; tracked in PuddingCount
        }

        var makiCounts = players.Select(p => CountMaki(p.Tableau)).ToArray();
        ApplyMakiScoring(makiCounts, roundScores, playerCount);

        return players.Select((p, i) =>
        {
            var puddingGain = p.Tableau.Count(c => c == SushiGoCards.Pudding);
            return p with
            {
                RoundScores  = [.. p.RoundScores, roundScores[i]],
                PuddingCount = p.PuddingCount + puddingGain
            };
        }).ToList();
    }

    private static int ScoreTempura(List<string> tableau)
    {
        int count = tableau.Count(c => c == SushiGoCards.Tempura);
        return (count / 2) * 5;
    }

    private static int ScoreSashimi(List<string> tableau)
    {
        int count = tableau.Count(c => c == SushiGoCards.Sashimi);
        return (count / 3) * 10;
    }

    private static int ScoreDumpling(List<string> tableau)
    {
        int count = tableau.Count(c => c == SushiGoCards.Dumpling);
        return count switch
        {
            0 => 0,
            1 => 1,
            2 => 3,
            3 => 6,
            4 => 10,
            _ => 15
        };
    }

    /// <summary>
    /// Score nigiri cards. Scan tableau left-to-right; a Wasabi that precedes
    /// a nigiri triples its value and is then consumed.
    /// </summary>
    private static int ScoreNigiri(List<string> tableau)
    {
        int  score      = 0;
        bool wasabiReady = false;

        foreach (var card in tableau)
        {
            switch (card)
            {
                case SushiGoCards.Wasabi:
                    wasabiReady = true;
                    break;

                case SushiGoCards.EggNigiri:
                {
                    int pts = 1;
                    if (wasabiReady) { pts *= 3; wasabiReady = false; }
                    score += pts;
                    break;
                }

                case SushiGoCards.SalmonNigiri:
                {
                    int pts = 2;
                    if (wasabiReady) { pts *= 3; wasabiReady = false; }
                    score += pts;
                    break;
                }

                case SushiGoCards.SquidNigiri:
                {
                    int pts = 3;
                    if (wasabiReady) { pts *= 3; wasabiReady = false; }
                    score += pts;
                    break;
                }
                // Chopsticks, Pudding, Tempura, etc. do not affect wasabi state
            }
        }

        return score;
    }

    private static int CountMaki(List<string> tableau)
    {
        int total = 0;
        foreach (var card in tableau)
        {
            total += card switch
            {
                SushiGoCards.Maki1 => 1,
                SushiGoCards.Maki2 => 2,
                SushiGoCards.Maki3 => 3,
                _                  => 0
            };
        }
        return total;
    }

    private static void ApplyMakiScoring(int[] makiCounts, int[] roundScores, int playerCount)
    {
        int firstMax = makiCounts.Max();
        if (firstMax == 0) return;

        var firstWinners = Enumerable.Range(0, playerCount)
            .Where(i => makiCounts[i] == firstMax).ToList();

        int firstPrize = 6 / firstWinners.Count;
        foreach (var i in firstWinners)
            roundScores[i] += firstPrize;

        // In 2-player, no second-place maki scoring
        if (playerCount == 2) return;

        int secondMax = makiCounts
            .Where((v, i) => !firstWinners.Contains(i))
            .DefaultIfEmpty(0)
            .Max();
        if (secondMax == 0) return;

        var secondWinners = Enumerable.Range(0, playerCount)
            .Where(i => !firstWinners.Contains(i) && makiCounts[i] == secondMax).ToList();

        int secondPrize = 3 / secondWinners.Count;
        foreach (var i in secondWinners)
            roundScores[i] += secondPrize;
    }

    // ── Pudding end-of-game scoring ───────────────────────────────────────────

    private static List<SushiGoPlayer> ApplyPuddingScoring(List<SushiGoPlayer> players)
    {
        int playerCount = players.Count;
        var puddings    = players.Select(p => p.PuddingCount).ToArray();
        var bonuses     = new int[playerCount];

        int maxPudding  = puddings.Max();
        var mostWinners = Enumerable.Range(0, playerCount)
            .Where(i => puddings[i] == maxPudding).ToList();
        int mostBonus = 6 / mostWinners.Count;
        foreach (var i in mostWinners)
            bonuses[i] += mostBonus;

        // -6 penalty for fewest puddings — NOT applied in 2-player
        if (playerCount > 2)
        {
            int minPudding   = puddings.Min();
            var fewestLosers = Enumerable.Range(0, playerCount)
                .Where(i => puddings[i] == minPudding).ToList();
            int fewestPenalty = 6 / fewestLosers.Count;
            foreach (var i in fewestLosers)
                bonuses[i] -= fewestPenalty;
        }

        // Append pudding score as a final entry in RoundScores
        return players.Select((p, i) =>
            bonuses[i] != 0
                ? p with { RoundScores = [.. p.RoundScores, bonuses[i]] }
                : p
        ).ToList();
    }

    // ── Determine winner ──────────────────────────────────────────────────────

    private static string DetermineWinner(List<SushiGoPlayer> players)
    {
        return players
            .OrderByDescending(p => p.RoundScores.Sum())
            .ThenByDescending(p => p.PuddingCount)
            .First()
            .Id;
    }

    // ── Deck / hand helpers ───────────────────────────────────────────────────

    private static readonly Dictionary<string, int> DeckComposition = new()
    {
        [SushiGoCards.Tempura]      = 14,
        [SushiGoCards.Sashimi]      = 14,
        [SushiGoCards.Dumpling]     = 14,
        [SushiGoCards.Maki3]        = 8,
        [SushiGoCards.Maki2]        = 12,
        [SushiGoCards.Maki1]        = 6,
        [SushiGoCards.SalmonNigiri] = 10,
        [SushiGoCards.SquidNigiri]  = 5,
        [SushiGoCards.EggNigiri]    = 5,
        [SushiGoCards.Pudding]      = 10,
        [SushiGoCards.Wasabi]       = 6,
        [SushiGoCards.Chopsticks]   = 4,
    };

    private static List<string> BuildShuffledDeck()
    {
        var deck = new List<string>(108);
        foreach (var (card, count) in DeckComposition)
            for (int i = 0; i < count; i++)
                deck.Add(card);

        // Fisher-Yates shuffle
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        return deck;
    }

    private static List<List<string>> DealHands(
        List<string> deck,
        int playerCount,
        out List<string> remainingDeck)
    {
        int handSize = playerCount switch
        {
            2 => 10,
            3 => 9,
            4 => 8,
            _ => 7   // 5 players
        };

        var hands = new List<List<string>>(playerCount);
        int pos   = 0;
        for (int i = 0; i < playerCount; i++)
        {
            hands.Add(deck.Skip(pos).Take(handSize).ToList());
            pos += handSize;
        }

        remainingDeck = deck.Skip(pos).ToList();
        return hands;
    }

    // ── Lookup helpers ────────────────────────────────────────────────────────

    private static (int index, SushiGoPlayer? player) FindPlayer(SushiGoState state, string playerId)
    {
        for (int i = 0; i < state.Players.Count; i++)
            if (state.Players[i].Id == playerId)
                return (i, state.Players[i]);
        return (-1, null);
    }

    // ── Serialization helpers ─────────────────────────────────────────────────

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters                  = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static T Deserialize<T>(JsonDocument doc) =>
        JsonSerializer.Deserialize<T>(doc.RootElement.GetRawText(), SerializerOptions)!;

    private static JsonDocument Serialize<T>(T obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj, SerializerOptions));
}
