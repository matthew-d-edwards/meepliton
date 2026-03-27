using System.Text.Json;
using Meepliton.Contracts;
using Meepliton.Games.LoveLetter.Models;

namespace Meepliton.Games.LoveLetter;

/// <summary>
/// Affairs of the Court — Love Letter for 2–4 players.
/// Players hold one card and try to eliminate rivals or hold the highest card
/// when the deck runs out. Collect tokens to win the heart of the Princess.
/// </summary>
public class LoveLetterModule : IGameModule, IGameHandler
{
    public string GameId      => "loveletter";
    public string Name        => "Affairs of the Court";
    public string Description => "One card. One chance. Win the heart of the Princess.";
    public int    MinPlayers  => LoveLetterConstants.MinPlayers;
    public int    MaxPlayers  => LoveLetterConstants.MaxPlayers;
    public bool   AllowLateJoin => false;
    public bool   SupportsAsync => true;
    public bool   SupportsUndo  => false;
    public string? ThumbnailUrl => null;
    public bool HasStateProjection => true;

    // ── IGameHandler ──────────────────────────────────────────────────────────

    public GameResult Handle(GameContext ctx)
    {
        var state  = Deserialize<LoveLetterState>(ctx.CurrentState);
        var action = Deserialize<LoveLetterAction>(ctx.Action);
        var error  = Validate(state, action, ctx.PlayerId);
        if (error is not null) return new GameResult(ctx.CurrentState, error);
        var newState    = Apply(state, action, ctx.PlayerId);
        var newStateDoc = Serialize(newState);
        if (newState.Phase == LoveLetterPhase.GameOver && newState.Winner is not null)
            return new GameResult(newStateDoc, Effects: [new GameOverEffect(newState.Winner)]);
        return new GameResult(newStateDoc);
    }

    // ── IGameModule.ProjectStateForPlayer ─────────────────────────────────────

    JsonDocument? IGameModule.ProjectStateForPlayer(JsonDocument fullState, string playerId)
    {
        var state = Deserialize<LoveLetterState>(fullState);
        if (state is null) return null;
        return Serialize(ProjectForPlayer(state, playerId));
    }

    private static LoveLetterState ProjectForPlayer(LoveLetterState state, string playerId)
    {
        // Hide other players' hand cards
        var projectedPlayers = state.Players.Select(p =>
            p.Id == playerId ? p : p with { HandCard = null }
        ).ToList();

        // PriestReveal only sent to the viewer
        var priestReveal = state.PendingPriestReveal?.ViewerId == playerId
            ? state.PendingPriestReveal
            : null;

        return state with
        {
            Players              = projectedPlayers,
            Deck                 = [],          // never send deck contents
            DeckSize             = state.Deck.Count,
            SetAsideCard         = null,        // never revealed
            PendingPriestReveal  = priestReveal
        };
    }

    // ── IGameModule.CreateInitialState ────────────────────────────────────────

    JsonDocument IGameModule.CreateInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options)
        => Serialize(CreateInitialState(players));

    public LoveLetterState CreateInitialState(IReadOnlyList<PlayerInfo> players)
    {
        var lobbyPlayers = players.Select(p => new LoveLetterPlayer(
            Id:          p.Id,
            DisplayName: p.DisplayName,
            AvatarUrl:   p.AvatarUrl,
            SeatIndex:   p.SeatIndex,
            HandCard:    null,
            DiscardPile: [],
            Tokens:      0,
            Active:      false,
            Handmaid:    false
        )).ToList();

        return new LoveLetterState(
            Phase:               LoveLetterPhase.Waiting,
            Players:             lobbyPlayers,
            Deck:                [],
            SetAsideCard:        null,
            FaceUpSetAside:      [],
            CurrentPlayerIndex:  0,
            Round:               0,
            LastRoundResult:     null,
            PendingPriestReveal: null,
            Winner:              null,
            DeckSize:            0
        );
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    public string? Validate(LoveLetterState state, LoveLetterAction action, string playerId)
    {
        switch (action.Type)
        {
            case "StartGame":
                if (state.Phase != LoveLetterPhase.Waiting)
                    return "The game has already started.";
                return null;

            case "PlayCard":
            {
                if (state.Phase != LoveLetterPhase.Playing)
                    return "You can only play a card during the Playing phase.";
                if (state.PendingPriestReveal is not null)
                    return "Waiting for the Priest reveal to be acknowledged.";
                var actor = state.Players.FirstOrDefault(p => p.Id == playerId);
                if (actor is null) return "Player not found.";
                if (!actor.Active) return "You have been eliminated.";
                if (state.Players[state.CurrentPlayerIndex].Id != playerId)
                    return "It is not your turn.";
                if (action.CardPlayed is null) return "Missing cardPlayed.";

                // After drawing, the actor holds 2 cards — validate they hold the card they claim to play.
                // We can't check this here without simulating the draw, so we trust the card name and validate effects.
                return ValidateCardPlay(state, actor, action);
            }

            case "AcknowledgePriest":
                if (state.Phase != LoveLetterPhase.Playing)
                    return "No Priest reveal pending.";
                if (state.PendingPriestReveal is null)
                    return "No Priest reveal pending.";
                if (state.PendingPriestReveal.ViewerId != playerId)
                    return "Only the player who played the Priest can acknowledge.";
                return null;

            case "StartNextRound":
                if (state.Phase != LoveLetterPhase.RoundEnd)
                    return "StartNextRound is only valid after a round ends.";
                // Host check: first player (seat 0) acts as host.
                var host = state.Players.MinBy(p => p.SeatIndex);
                if (host is null || host.Id != playerId)
                    return "Only the host can start the next round.";
                return null;

            default:
                return $"Unknown action type: {action.Type}";
        }
    }

    private static string? ValidateCardPlay(LoveLetterState state, LoveLetterPlayer actor, LoveLetterAction action)
    {
        // Countess rule: if the other card (the one NOT being played) is King or Prince,
        // the player MUST play Countess.
        // Since we know actor.HandCard is the card they had before drawing, and they'll draw one more,
        // we apply Countess enforcement at Apply time when we know both cards.
        // Here we just ensure the card name is valid.
        var card = action.CardPlayed;
        if (!IsValidCardName(card!)) return $"Unknown card: {card}";

        // For targeted effects, validate the target exists and is eligible
        switch (card)
        {
            case "Guard":
            {
                if (action.TargetId is not null)
                {
                    var target = state.Players.FirstOrDefault(p => p.Id == action.TargetId);
                    if (target is null) return "Target player not found.";
                    if (!target.Active) return "Target has been eliminated.";
                    if (target.Id == actor.Id) return "You cannot target yourself with the Guard.";
                    if (action.GuessedCard is null) return "Missing guessedCard for Guard.";
                    if (action.GuessedCard == "Guard") return "You cannot guess Guard.";
                    if (!IsValidCardName(action.GuessedCard)) return $"Unknown guessed card: {action.GuessedCard}";
                }
                // If all targets are Handmaid-protected, targeting anyone is fine — effect just fizzles.
                return null;
            }

            case "Priest":
            case "Baron":
            case "King":
            {
                if (action.TargetId is null) return $"Target is required for {card}.";
                var target = state.Players.FirstOrDefault(p => p.Id == action.TargetId);
                if (target is null) return "Target player not found.";
                if (!target.Active) return "Target has been eliminated.";
                if (target.Id == actor.Id) return $"You cannot target yourself with the {card}.";
                return null;
            }

            case "Prince":
            {
                if (action.TargetId is null) return "Target is required for Prince.";
                var target = state.Players.FirstOrDefault(p => p.Id == action.TargetId);
                if (target is null) return "Target player not found.";
                if (!target.Active) return "Target has been eliminated.";
                // Self-targeting allowed for Prince
                return null;
            }

            case "Handmaid":
            case "Countess":
            case "Princess":
                return null; // no target needed

            default:
                return null;
        }
    }

    private static bool IsValidCardName(string card) => card is
        "Guard" or "Priest" or "Baron" or "Handmaid" or
        "Prince" or "King" or "Countess" or "Princess";

    // ── Apply ─────────────────────────────────────────────────────────────────

    public LoveLetterState Apply(LoveLetterState state, LoveLetterAction action, string playerId) =>
        action.Type switch
        {
            "StartGame"         => ApplyStartGame(state),
            "PlayCard"          => ApplyPlayCard(state, action, playerId),
            "AcknowledgePriest" => ApplyAcknowledgePriest(state),
            "StartNextRound"    => ApplyStartNextRound(state),
            _                   => state
        };

    // ── StartGame ─────────────────────────────────────────────────────────────

    private static LoveLetterState ApplyStartGame(LoveLetterState state)
    {
        // Reset tokens and call SetupRound to deal first round
        var freshPlayers = state.Players.Select(p => p with { Tokens = 0 }).ToList();
        return SetupRound(state with { Players = freshPlayers, Round = 0 });
    }

    // ── SetupRound ────────────────────────────────────────────────────────────

    private static LoveLetterState SetupRound(LoveLetterState state)
    {
        var deck = Shuffle(LoveLetterConstants.BuildDeck());

        // Set aside 1 face-down card
        var setAside = deck[0];
        deck.RemoveAt(0);

        // 2 players: remove 3 more face-up
        var faceUp = new List<string>();
        if (state.Players.Count == 2)
        {
            for (int i = 0; i < 3; i++)
            {
                faceUp.Add(deck[0]);
                deck.RemoveAt(0);
            }
        }

        // Deal 1 card to each player
        var readyPlayers = state.Players.Select(p => p with
        {
            Active      = true,
            HandCard    = null,
            DiscardPile = [],
            Handmaid    = false
        }).ToList();

        for (int i = 0; i < readyPlayers.Count; i++)
        {
            readyPlayers[i] = readyPlayers[i] with { HandCard = deck[0] };
            deck.RemoveAt(0);
        }

        return state with
        {
            Phase               = LoveLetterPhase.Playing,
            Players             = readyPlayers,
            Deck                = deck,
            SetAsideCard        = setAside,
            FaceUpSetAside      = faceUp,
            CurrentPlayerIndex  = 0,
            Round               = state.Round + 1,
            LastRoundResult     = null,
            PendingPriestReveal = null,
            Winner              = null,
            DeckSize            = deck.Count
        };
    }

    // ── PlayCard ──────────────────────────────────────────────────────────────

    private static LoveLetterState ApplyPlayCard(LoveLetterState state, LoveLetterAction action, string playerId)
    {
        var actorIndex = state.CurrentPlayerIndex;
        var actor      = state.Players[actorIndex];

        // Clear Handmaid for current player at start of their turn
        actor = actor with { Handmaid = false };

        // Draw top card from deck — actor now holds 2 cards
        if (state.Deck.Count == 0)
            return state; // should not happen; validated implicitly

        var deck = state.Deck.ToList();
        var drawnCard = deck[0];
        deck.RemoveAt(0);

        // Determine which card actor holds after draw: their existing HandCard + drawnCard
        var cardA = actor.HandCard!;
        var cardB = drawnCard;
        var cardPlayed = action.CardPlayed!;

        // Countess enforcement: if they're NOT playing Countess but hold Countess with King or Prince
        string heldCard;
        if (cardPlayed == cardA)
            heldCard = cardB;
        else
            heldCard = cardA;

        // Validate Countess rule (holding King or Prince but not playing Countess)
        bool holdsCountess = cardA == "Countess" || cardB == "Countess";
        bool holdsRoyalty  = cardA is "King" or "Prince" || cardB is "King" or "Prince";
        if (holdsCountess && holdsRoyalty && cardPlayed != "Countess")
        {
            // Reject — but this should have been caught in Validate for a compliant client.
            // We apply it here as a server-side guard.
            return state;
        }

        // Update actor: hand card becomes the held card
        actor = actor with { HandCard = heldCard };

        // Move played card to actor's discard pile
        var actorDiscard = actor.DiscardPile.ToList();
        actorDiscard.Add(cardPlayed);
        actor = actor with { DiscardPile = actorDiscard };

        // Apply card effect
        var players = state.Players.ToList();
        players[actorIndex] = actor;

        switch (cardPlayed)
        {
            case "Guard":
            {
                if (action.TargetId is not null)
                {
                    var targetIdx = players.FindIndex(p => p.Id == action.TargetId);
                    if (targetIdx >= 0)
                    {
                        var target = players[targetIdx];
                        if (!target.Handmaid && target.Active && action.GuessedCard is not null)
                        {
                            if (target.HandCard == action.GuessedCard)
                                players[targetIdx] = Eliminate(target);
                        }
                    }
                }
                break;
            }

            case "Priest":
            {
                if (action.TargetId is not null)
                {
                    var target = players.FirstOrDefault(p => p.Id == action.TargetId);
                    if (target is not null && !target.Handmaid && target.Active)
                    {
                        var nextState = state with
                        {
                            Phase               = LoveLetterPhase.Playing,
                            Players             = players,
                            Deck                = deck,
                            DeckSize            = deck.Count,
                            PendingPriestReveal = new PriestReveal(playerId, target.Id, target.HandCard!)
                        };
                        return nextState;
                    }
                }
                break;
            }

            case "Baron":
            {
                if (action.TargetId is not null)
                {
                    var targetIdx = players.FindIndex(p => p.Id == action.TargetId);
                    if (targetIdx >= 0)
                    {
                        var target = players[targetIdx];
                        if (!target.Handmaid && target.Active)
                        {
                            int actorVal  = LoveLetterConstants.CardValue(actor.HandCard!);
                            int targetVal = LoveLetterConstants.CardValue(target.HandCard!);
                            if (actorVal > targetVal)
                                players[targetIdx] = Eliminate(target);
                            else if (targetVal > actorVal)
                                players[actorIndex] = Eliminate(players[actorIndex]);
                            // tie: no effect
                        }
                    }
                }
                break;
            }

            case "Handmaid":
                players[actorIndex] = players[actorIndex] with { Handmaid = true };
                break;

            case "Prince":
            {
                if (action.TargetId is not null)
                {
                    var targetIdx = players.FindIndex(p => p.Id == action.TargetId);
                    if (targetIdx >= 0)
                    {
                        var target = players[targetIdx];
                        bool isSelf = target.Id == playerId;
                        if (target.Active && (!target.Handmaid || isSelf))
                        {
                            var targetDiscard = target.DiscardPile.ToList();
                            targetDiscard.Add(target.HandCard!);

                            if (target.HandCard == "Princess")
                            {
                                // Eliminated for discarding Princess
                                players[targetIdx] = Eliminate(target) with { DiscardPile = targetDiscard };
                            }
                            else
                            {
                                // Draw new card (or SetAsideCard if deck empty)
                                string newCard;
                                if (deck.Count > 0)
                                {
                                    newCard = deck[0];
                                    deck.RemoveAt(0);
                                }
                                else
                                {
                                    newCard = state.SetAsideCard!;
                                }
                                players[targetIdx] = target with
                                {
                                    HandCard    = newCard,
                                    DiscardPile = targetDiscard
                                };
                            }
                        }
                    }
                }
                break;
            }

            case "King":
            {
                if (action.TargetId is not null)
                {
                    var targetIdx = players.FindIndex(p => p.Id == action.TargetId);
                    if (targetIdx >= 0)
                    {
                        var target = players[targetIdx];
                        if (!target.Handmaid && target.Active)
                        {
                            var actorHand  = players[actorIndex].HandCard;
                            var targetHand = target.HandCard;
                            players[actorIndex] = players[actorIndex] with { HandCard = targetHand };
                            players[targetIdx]  = target with { HandCard = actorHand };
                        }
                    }
                }
                break;
            }

            case "Countess":
                // No effect
                break;

            case "Princess":
                // Playing the Princess eliminates the actor
                players[actorIndex] = Eliminate(players[actorIndex]);
                break;
        }

        var updatedState = state with
        {
            Players             = players,
            Deck                = deck,
            DeckSize            = deck.Count,
            PendingPriestReveal = null
        };

        // Check immediate single-survivor round end
        var activePlayers = players.Where(p => p.Active).ToList();
        if (activePlayers.Count <= 1)
            return EndRound(updatedState);

        // Advance to next active player
        int nextIndex = NextActivePlayerIndex(players, actorIndex);
        updatedState = updatedState with { CurrentPlayerIndex = nextIndex };

        if (deck.Count == 0)
        {
            // Check if it's time to end the round at next turn opportunity
            // Round ends when deck is empty — do so now
            return EndRound(updatedState);
        }

        return updatedState;
    }

    // ── AcknowledgePriest ─────────────────────────────────────────────────────

    private static LoveLetterState ApplyAcknowledgePriest(LoveLetterState state)
    {
        var nextState = state with { PendingPriestReveal = null };

        // Advance turn
        var nextIndex = NextActivePlayerIndex(nextState.Players, nextState.CurrentPlayerIndex);
        nextState = nextState with { CurrentPlayerIndex = nextIndex };

        // Check round end (deck empty case — deck may have run out before Priest was acknowledged)
        if (nextState.Deck.Count == 0)
            return EndRound(nextState);

        var activePlayers = nextState.Players.Where(p => p.Active).ToList();
        if (activePlayers.Count <= 1)
            return EndRound(nextState);

        return nextState;
    }

    // ── StartNextRound ────────────────────────────────────────────────────────

    private static LoveLetterState ApplyStartNextRound(LoveLetterState state)
    {
        var resetPlayers = state.Players.Select(p => p with
        {
            Active      = true,
            HandCard    = null,
            DiscardPile = [],
            Handmaid    = false
        }).ToList();

        return SetupRound(state with { Players = resetPlayers });
    }

    // ── Round end logic ───────────────────────────────────────────────────────

    private static LoveLetterState EndRound(LoveLetterState state)
    {
        var activePlayers = state.Players.Where(p => p.Active).ToList();
        List<LoveLetterPlayer> winners;
        string reason;

        if (activePlayers.Count == 1)
        {
            winners = activePlayers;
            reason  = "Last player standing";
        }
        else
        {
            // Compare highest hand card value; tiebreak by highest discard pile total
            int maxHandVal    = activePlayers.Max(p => LoveLetterConstants.CardValue(p.HandCard!));
            var topHandPlayers = activePlayers.Where(p =>
                LoveLetterConstants.CardValue(p.HandCard!) == maxHandVal).ToList();

            if (topHandPlayers.Count == 1)
            {
                winners = topHandPlayers;
                reason  = "Highest card";
            }
            else
            {
                int maxDiscard = topHandPlayers.Max(p =>
                    p.DiscardPile.Sum(c => LoveLetterConstants.CardValue(c)));
                winners = topHandPlayers.Where(p =>
                    p.DiscardPile.Sum(c => LoveLetterConstants.CardValue(c)) == maxDiscard).ToList();
                reason = "Highest card (tiebreak: highest discard total)";
            }
        }

        // Award tokens
        var winnerIds = winners.Select(p => p.Id).ToList();
        var updatedPlayers = state.Players.Select(p =>
            winnerIds.Contains(p.Id) ? p with { Tokens = p.Tokens + 1 } : p
        ).ToList();

        // Build reveals
        var reveals = state.Players.Select(p => new PlayerHandReveal(p.Id, p.HandCard)).ToList();

        var roundResult = new RoundResult(winnerIds, reason, reveals);

        // Check game-over thresholds
        var tokensToWin = LoveLetterConstants.TokensToWin(state.Players.Count);
        string? gameWinner = null;
        LoveLetterPhase nextPhase;

        var gameWinners = updatedPlayers.Where(p => p.Tokens >= tokensToWin).ToList();
        if (gameWinners.Count > 0)
        {
            // If multiple hit threshold simultaneously, take the one with most tokens (or first by seat)
            gameWinner = gameWinners.OrderByDescending(p => p.Tokens).ThenBy(p => p.SeatIndex).First().Id;
            nextPhase  = LoveLetterPhase.GameOver;
        }
        else
        {
            nextPhase = LoveLetterPhase.RoundEnd;
        }

        return state with
        {
            Phase           = nextPhase,
            Players         = updatedPlayers,
            LastRoundResult = roundResult,
            Winner          = gameWinner,
            DeckSize        = 0
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LoveLetterPlayer Eliminate(LoveLetterPlayer player) =>
        player with { Active = false, HandCard = null };

    private static int NextActivePlayerIndex(List<LoveLetterPlayer> players, int currentIndex)
    {
        int total = players.Count;
        for (int i = 1; i <= total; i++)
        {
            int idx = (currentIndex + i) % total;
            if (players[idx].Active) return idx;
        }
        return currentIndex;
    }

    private static List<string> Shuffle(List<string> deck)
    {
        var result = deck.ToList();
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
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
