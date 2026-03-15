using System.Text.Json;
using Meepliton.Contracts;
using Meepliton.Games.LiarsDice.Models;

namespace Meepliton.Games.LiarsDice;

/// <summary>
/// Liar's Dice — dice-based bluffing game for 2–6 players.
/// Players bid on how many dice of a given face exist across all cups.
/// Any player may challenge the current bid; the loser drops one die.
/// Last player with dice wins.
/// </summary>
public class LiarsDiceModule : ReducerGameModule<LiarsDiceState, LiarsDiceAction, LiarsDiceOptions>
{
    public override string GameId      => "liarsdice";
    public override string Name        => "Liar's Dice";
    public override string Description => "Bid on hidden dice and call out bluffs. The last player with dice wins.";
    public override int    MinPlayers  => 2;
    public override int    MaxPlayers  => 6;
    public override bool   AllowLateJoin => false;
    public override bool   SupportsUndo  => false;

    // ── Explicit IGameHandler.Handle — adds GameOverEffect when game ends ─────

    GameResult IGameHandler.Handle(GameContext ctx)
    {
        var state  = Deserialize<LiarsDiceState>(ctx.CurrentState);
        var action = Deserialize<LiarsDiceAction>(ctx.Action);
        var error  = Validate(state, action, ctx.PlayerId);
        if (error is not null) return new GameResult(ctx.CurrentState, error);
        var newState    = Apply(state, action);
        var newStateDoc = Serialize(newState);
        if (newState.Phase == LiarsDicePhase.Finished && newState.Winner is not null)
            return new GameResult(newStateDoc, Effects: [new GameOverEffect(newState.Winner)]);
        return new GameResult(newStateDoc);
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    public override LiarsDiceState CreateInitialState(
        IReadOnlyList<PlayerInfo> players,
        LiarsDiceOptions? options)
    {
        var startingDice = options?.StartingDice ?? 5;
        var dicePlayers = players.Select(p => new DicePlayer(
            Id:              p.Id,
            DisplayName:     p.DisplayName,
            AvatarUrl:       p.AvatarUrl,
            SeatIndex:       p.SeatIndex,
            Dice:            RollDice(startingDice),
            DiceCount:       startingDice,
            Active:          true,
            HasUsedPalifico: false
        )).ToList();

        return new LiarsDiceState(
            Phase:               LiarsDicePhase.Bidding,
            Players:             dicePlayers,
            CurrentPlayerIndex:  0,
            CurrentBid:          null,
            RoundNumber:         1,
            PalificoActive:      false,
            LastChallengeResult: null,
            LastReveal:          null,
            Winner:              null
        );
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    public override string? Validate(LiarsDiceState state, LiarsDiceAction action, string playerId)
    {
        // Finished phase — reject everything
        if (state.Phase == LiarsDicePhase.Finished)
            return "The game is over.";

        switch (action.Type)
        {
            case "StartGame":
                // StartGame is handled before state is initialised; if state exists, reject
                return "The game has already started.";

            case "PlaceBid":
            {
                if (state.Phase != LiarsDicePhase.Bidding)
                    return "You can only place a bid during the Bidding phase.";
                if (!IsCurrentPlayer(state, playerId))
                    return "It is not your turn.";
                if (action.BidData is null)
                    return "Missing bid data.";
                if (action.BidData.Quantity < 1)
                    return "Quantity must be at least 1.";
                if (action.BidData.Face is < 1 or > 6)
                    return "Face must be between 1 and 6.";
                if (state.CurrentBid is not null && !IsBidHigher(action.BidData, state.CurrentBid))
                    return "Your bid must be strictly higher than the current bid.";
                return null;
            }

            case "CallLiar":
            {
                if (state.Phase != LiarsDicePhase.Bidding)
                    return "You can only call Liar during the Bidding phase.";
                if (!IsCurrentPlayer(state, playerId))
                    return "It is not your turn.";
                if (state.CurrentBid is null)
                    return "There is no bid to challenge. You must open with a bid.";
                return null;
            }

            case "StartNextRound":
            {
                if (state.Phase != LiarsDicePhase.Reveal)
                    return "StartNextRound is only valid during the Reveal phase.";
                // Any active player may trigger the next round
                var player = state.Players.FirstOrDefault(p => p.Id == playerId);
                if (player is null || !player.Active)
                    return "Only active players can start the next round.";
                return null;
            }

            case "DeclarePalifico":
            {
                if (state.Phase != LiarsDicePhase.Bidding)
                    return "Palifico can only be declared during the Bidding phase.";
                if (!IsCurrentPlayer(state, playerId))
                    return "It is not your turn.";
                if (state.CurrentBid is not null)
                    return "Palifico must be declared before any bid is placed this round.";
                var player = state.Players.First(p => p.Id == playerId);
                if (player.DiceCount != 1)
                    return "You can only declare Palifico when you have exactly one die.";
                if (player.HasUsedPalifico)
                    return "You have already used your Palifico this game.";
                return null;
            }

            default:
                return $"Unknown action type: {action.Type}";
        }
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    public override LiarsDiceState Apply(LiarsDiceState state, LiarsDiceAction action) =>
        action.Type switch
        {
            "PlaceBid"       => ApplyPlaceBid(state, action.BidData!),
            "CallLiar"       => ApplyCallLiar(state),
            "StartNextRound" => ApplyStartNextRound(state),
            "DeclarePalifico" => ApplyDeclarePalifico(state),
            _ => state
        };

    // ── PlaceBid ──────────────────────────────────────────────────────────────

    private static LiarsDiceState ApplyPlaceBid(LiarsDiceState state, BidPayload bid)
    {
        var newBid = new Bid(bid.Quantity, bid.Face);
        var nextIndex = NextActivePlayerIndex(state.Players, state.CurrentPlayerIndex);
        return state with
        {
            CurrentBid:         newBid,
            CurrentPlayerIndex: nextIndex
        };
    }

    // ── CallLiar ──────────────────────────────────────────────────────────────

    private static LiarsDiceState ApplyCallLiar(LiarsDiceState state)
    {
        var bid = state.CurrentBid!;
        var challenger = state.Players[state.CurrentPlayerIndex];

        // Find the previous bidder (who made the current bid) — it's the player before current
        var previousIndex = PreviousActivePlayerIndex(state.Players, state.CurrentPlayerIndex);
        var bidder = state.Players[previousIndex];

        // Count matching dice
        var actualCount = CountMatchingDice(state.Players, bid, state.PalificoActive);

        // Determine loser: if count >= quantity, challenger loses; else bidder loses
        string loserId;
        string resultMessage;
        if (actualCount >= bid.Quantity)
        {
            loserId = challenger.Id;
            resultMessage = $"The bid was met! {actualCount} {FaceName(bid.Face)}(s) found (needed {bid.Quantity}). {challenger.DisplayName} loses a die.";
        }
        else
        {
            loserId = bidder.Id;
            resultMessage = $"Liar! Only {actualCount} {FaceName(bid.Face)}(s) found (needed {bid.Quantity}). {bidder.DisplayName} loses a die.";
        }

        // Build reveal snapshot (before removing the die)
        var revealPlayers = state.Players.Select(p =>
            new PlayerReveal(p.Id, p.Dice.ToList())).ToList();
        var reveal = new RevealSnapshot(
            Players:      revealPlayers,
            ChallengedBid: bid,
            ActualCount:  actualCount,
            LoserId:      loserId
        );

        // Remove a die from the loser
        var players = state.Players.Select(p =>
        {
            if (p.Id != loserId) return p;
            var newDice = p.Dice.ToList();
            if (newDice.Count > 0) newDice.RemoveAt(newDice.Count - 1);
            var newCount = newDice.Count;
            return p with { Dice = newDice, DiceCount = newCount, Active = newCount > 0 };
        }).ToList();

        // Check for game over
        var activePlayers = players.Where(p => p.Active).ToList();
        if (activePlayers.Count == 1)
        {
            var winner = activePlayers[0];
            return state with
            {
                Phase:               LiarsDicePhase.Finished,
                Players:             players,
                CurrentBid:          null,
                LastChallengeResult: resultMessage,
                LastReveal:          reveal,
                Winner:              winner.Id
            };
        }

        // Transition to Reveal phase
        return state with
        {
            Phase:               LiarsDicePhase.Reveal,
            Players:             players,
            CurrentBid:          null,
            LastChallengeResult: resultMessage,
            LastReveal:          reveal
        };
    }

    // ── StartNextRound ────────────────────────────────────────────────────────

    private static LiarsDiceState ApplyStartNextRound(LiarsDiceState state)
    {
        var loserId = state.LastReveal?.LoserId;

        // Re-roll all active players' dice
        var players = state.Players.Select(p =>
        {
            if (!p.Active) return p;
            var newDice = RollDice(p.DiceCount);
            return p with { Dice = newDice };
        }).ToList();

        // Find starting player: loser's index, or first active if loser was eliminated
        int startIndex;
        if (loserId is not null)
        {
            var loserIdx = players.FindIndex(p => p.Id == loserId);
            if (loserIdx >= 0 && players[loserIdx].Active)
                startIndex = loserIdx;
            else
                startIndex = players.FindIndex(p => p.Active);
        }
        else
        {
            startIndex = players.FindIndex(p => p.Active);
        }

        if (startIndex < 0) startIndex = 0;

        return state with
        {
            Phase:               LiarsDicePhase.Bidding,
            Players:             players,
            CurrentPlayerIndex:  startIndex,
            CurrentBid:          null,
            RoundNumber:         state.RoundNumber + 1,
            PalificoActive:      false,
            LastChallengeResult: null,
            LastReveal:          null
        };
    }

    // ── DeclarePalifico ───────────────────────────────────────────────────────

    private static LiarsDiceState ApplyDeclarePalifico(LiarsDiceState state)
    {
        var players = state.Players.Select(p =>
        {
            if (p.Id != state.Players[state.CurrentPlayerIndex].Id) return p;
            return p with { HasUsedPalifico = true };
        }).ToList();

        return state with
        {
            PalificoActive: true,
            Players:        players
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsCurrentPlayer(LiarsDiceState state, string playerId) =>
        state.Players[state.CurrentPlayerIndex].Id == playerId;

    /// <summary>
    /// A bid (q2, f2) is strictly higher than (q1, f1) when q2 > q1, OR q2 == q1 and f2 > f1.
    /// </summary>
    private static bool IsBidHigher(BidPayload newBid, Bid current) =>
        newBid.Quantity > current.Quantity ||
        (newBid.Quantity == current.Quantity && newBid.Face > current.Face);

    /// <summary>
    /// Count dice matching the bid face across all active players.
    /// Wild logic: in a normal round, 1s count toward any non-1 face.
    /// No wilds in a Palifico round, or when the bid face is 1.
    /// </summary>
    private static int CountMatchingDice(List<DicePlayer> players, Bid bid, bool palificoActive)
    {
        bool wildsApply = !palificoActive && bid.Face != 1;
        int count = 0;
        foreach (var p in players)
        {
            foreach (var die in p.Dice)
            {
                if (die == bid.Face) count++;
                else if (wildsApply && die == 1) count++;
            }
        }
        return count;
    }

    private static int NextActivePlayerIndex(List<DicePlayer> players, int currentIndex)
    {
        int total = players.Count;
        for (int i = 1; i <= total; i++)
        {
            int idx = (currentIndex + i) % total;
            if (players[idx].Active) return idx;
        }
        return currentIndex; // fallback (should never happen with 2+ active players)
    }

    private static int PreviousActivePlayerIndex(List<DicePlayer> players, int currentIndex)
    {
        int total = players.Count;
        for (int i = 1; i <= total; i++)
        {
            int idx = (currentIndex - i + total) % total;
            if (players[idx].Active) return idx;
        }
        return currentIndex; // fallback
    }

    private static List<int> RollDice(int count) =>
        Enumerable.Range(0, count)
            .Select(_ => Random.Shared.Next(1, 7))
            .ToList();

    private static string FaceName(int face) => face switch
    {
        1 => "1",
        2 => "2",
        3 => "3",
        4 => "4",
        5 => "5",
        6 => "6",
        _ => face.ToString()
    };
}
