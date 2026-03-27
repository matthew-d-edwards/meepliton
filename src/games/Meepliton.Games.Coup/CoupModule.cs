using System.Text.Json;
using Meepliton.Contracts;
using Meepliton.Games.Coup.Models;

namespace Meepliton.Games.Coup;

/// <summary>
/// The Inner Circle — social deduction / bluffing game for 2–6 players.
/// Players claim character abilities and call each other's bluffs.
/// Last player with influence (face-down cards) wins.
/// </summary>
public class CoupModule : IGameModule, IGameHandler
{
    public string GameId      => "coup";
    public string Name        => "The Inner Circle";
    public string Description => "Everyone's lying. Only one is winning.";
    public int    MinPlayers  => CoupConstants.MinPlayers;
    public int    MaxPlayers  => CoupConstants.MaxPlayers;
    public bool   AllowLateJoin => false;
    public bool   SupportsAsync => false;
    public bool   SupportsUndo  => false;
    public string? ThumbnailUrl => null;
    public bool HasStateProjection => true;

    // ── IGameHandler ─────────────────────────────────────────────────────────

    public GameResult Handle(GameContext ctx)
    {
        var state  = Deserialize<CoupState>(ctx.CurrentState);
        var action = Deserialize<CoupAction>(ctx.Action);
        var error  = Validate(state, action, ctx.PlayerId);
        if (error is not null) return new GameResult(ctx.CurrentState, error);
        var newState    = Apply(state, action, ctx.PlayerId);
        var newStateDoc = Serialize(newState);
        if (newState.Phase == CoupPhase.Finished && newState.Winner is not null)
            return new GameResult(newStateDoc, Effects: [new GameOverEffect(newState.Winner)]);
        return new GameResult(newStateDoc);
    }

    // ── IGameModule.ProjectStateForPlayer ────────────────────────────────────

    JsonDocument? IGameModule.ProjectStateForPlayer(JsonDocument fullState, string playerId)
    {
        var state = Deserialize<CoupState>(fullState);
        if (state is null) return null;
        return Serialize(ProjectForPlayer(state, playerId));
    }

    private static CoupState ProjectForPlayer(CoupState state, string playerId)
    {
        // Hide other players' unrevealed influence cards
        var projectedPlayers = state.Players.Select(p =>
        {
            if (p.Id == playerId) return p;
            var hiddenInfluence = p.Influence.Select(card =>
                card.Revealed ? card : new InfluenceCard(Character: null!, Revealed: false)
            ).ToList();
            return p with { Influence = hiddenInfluence };
        }).ToList();

        // Hide ExchangeOptions from non-actor players
        var projectedPending = state.Pending;
        if (projectedPending is not null
            && projectedPending.ExchangeOptions is not null
            && projectedPending.ActorId != playerId)
        {
            projectedPending = projectedPending with { ExchangeOptions = null };
        }

        // Never send the deck
        return state with
        {
            Players = projectedPlayers,
            Deck    = [],
            Pending = projectedPending
        };
    }

    // ── IGameModule.CreateInitialState ───────────────────────────────────────

    JsonDocument IGameModule.CreateInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options)
        => Serialize(CreateInitialState(players));

    public CoupState CreateInitialState(IReadOnlyList<PlayerInfo> players)
    {
        var coupPlayers = players.Select(p => new CoupPlayer(
            Id:          p.Id,
            DisplayName: p.DisplayName,
            AvatarUrl:   p.AvatarUrl,
            SeatIndex:   p.SeatIndex,
            Influence:   [],
            Coins:       0,
            Active:      true
        )).ToList();

        return new CoupState(
            Phase:              CoupPhase.Waiting,
            Players:            coupPlayers,
            Deck:               [],
            ActivePlayerIndex:  0,
            Pending:            null,
            Winner:             null
        );
    }

    // ── Validate ─────────────────────────────────────────────────────────────

    public string? Validate(CoupState state, CoupAction action, string playerId)
    {
        if (state.Phase == CoupPhase.Finished)
            return "The game is over.";

        switch (action.Type)
        {
            case "StartGame":
                if (state.Phase != CoupPhase.Waiting)
                    return "The game has already started.";
                if (state.Players.Count < CoupConstants.MinPlayers)
                    return $"Need at least {CoupConstants.MinPlayers} players to start.";
                return null;

            case "TakeIncome":
            case "TakeForeignAid":
            case "TakeTax":
            case "Exchange":
                return ValidateActivePlayerAction(state, playerId);

            case "DoCoup":
            {
                var err = ValidateActivePlayerAction(state, playerId);
                if (err is not null) return err;
                var actor = GetPlayer(state, playerId)!;
                if (actor.Coins < 7) return "You need at least 7 coins to perform a coup.";
                if (action.TargetId is null) return "DoCoup requires a target.";
                var target = GetPlayer(state, action.TargetId);
                if (target is null || !target.Active) return "Invalid target.";
                return null;
            }

            case "Assassinate":
            {
                var err = ValidateActivePlayerAction(state, playerId);
                if (err is not null) return err;
                var actor = GetPlayer(state, playerId)!;
                if (actor.Coins < 3) return "You need at least 3 coins to assassinate.";
                if (action.TargetId is null) return "Assassinate requires a target.";
                var target = GetPlayer(state, action.TargetId);
                if (target is null || !target.Active) return "Invalid target.";
                return null;
            }

            case "Steal":
            {
                var err = ValidateActivePlayerAction(state, playerId);
                if (err is not null) return err;
                if (action.TargetId is null) return "Steal requires a target.";
                var target = GetPlayer(state, action.TargetId);
                if (target is null || !target.Active) return "Invalid target.";
                if (target.Id == playerId) return "Cannot steal from yourself.";
                return null;
            }

            case "Challenge":
            {
                if (state.Phase != CoupPhase.AwaitingResponses) return "No pending action to challenge.";
                if (state.Pending is null) return "No pending action to challenge.";
                var pending = state.Pending;
                // Actor cannot challenge their own action
                if (pending.ActorId == playerId) return "You cannot challenge your own action.";
                // In BlockResponses, only the original actor may challenge the block
                if (pending.Step == PendingStep.BlockResponses && pending.ActorId != playerId)
                    return "Only the original actor can challenge a block.";
                // Cannot challenge if already passed
                if (pending.PassedPlayers.Contains(playerId)) return "You have already passed.";
                return null;
            }

            case "Block":
            {
                if (state.Phase != CoupPhase.AwaitingResponses) return "Nothing to block.";
                if (state.Pending is null) return "Nothing to block.";
                var pending = state.Pending;
                if (pending.Step != PendingStep.ActionResponses) return "Cannot block at this stage.";
                if (pending.BlockerId is not null) return "Action is already being blocked.";
                // Actor cannot block their own action
                if (pending.ActorId == playerId) return "You cannot block your own action.";
                // Only targeted player can block assassinate/steal; anyone can block foreign aid
                if (pending.ActionType == "Assassinate" || pending.ActionType == "Steal")
                {
                    if (pending.TargetId != playerId) return "Only the target can block this action.";
                }
                if (action.Character is null) return "Block requires a character claim.";
                return null;
            }

            case "Pass":
            {
                if (state.Phase != CoupPhase.AwaitingResponses) return "Nothing to pass on.";
                if (state.Pending is null) return "Nothing to pass on.";
                var pending = state.Pending;
                // Actor cannot pass on their own action (they declared it already)
                if (pending.Step == PendingStep.ActionResponses && pending.ActorId == playerId)
                    return "You cannot pass on your own action.";
                // In BlockResponses, only the original actor passes
                if (pending.Step == PendingStep.BlockResponses && pending.ActorId != playerId)
                    return "Only the original actor can pass on a block.";
                if (pending.PassedPlayers.Contains(playerId)) return "You have already passed.";
                return null;
            }

            case "LoseInfluence":
            {
                if (state.Phase != CoupPhase.InfluenceLoss) return "Not in influence loss phase.";
                if (state.Pending?.InfluenceLossPlayerId != playerId)
                    return "It is not your turn to lose influence.";
                if (action.InfluenceToLose is null) return "Specify which card to lose.";
                var player = GetPlayer(state, playerId)!;
                var card = player.Influence.FirstOrDefault(
                    c => c.Character == action.InfluenceToLose && !c.Revealed);
                if (card is null) return "You do not have that card or it is already revealed.";
                return null;
            }

            case "ChooseExchange":
            {
                if (state.Phase != CoupPhase.Exchange) return "Not in exchange phase.";
                var pending = state.Pending;
                if (pending is null) return "No pending exchange.";
                if (pending.ActorId != playerId) return "It is not your turn to exchange.";
                if (action.KeepCards is null || action.KeepCards.Count != 2)
                    return "You must choose exactly 2 cards to keep.";
                // Validate kept cards are available in the exchange pool
                var exchangeOptions = pending.ExchangeOptions;
                if (exchangeOptions is null) return "No exchange options available.";
                var available = exchangeOptions.ToList();
                foreach (var card in action.KeepCards)
                {
                    var idx = available.IndexOf(card);
                    if (idx < 0) return $"Card '{card}' is not available in your exchange options.";
                    available.RemoveAt(idx);
                }
                return null;
            }

            default:
                return $"Unknown action type: {action.Type}";
        }
    }

    private static string? ValidateActivePlayerAction(CoupState state, string playerId)
    {
        if (state.Phase != CoupPhase.AwaitingResponses) return "Not in an action phase.";
        if (state.Pending is not null) return "Another action is already pending.";
        var activePlayer = state.Players[state.ActivePlayerIndex];
        if (activePlayer.Id != playerId) return "It is not your turn.";
        // Mandatory coup: must coup if 10+ coins
        if (activePlayer.Coins >= 10) return "You must perform a coup with 10 or more coins.";
        return null;
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    public CoupState Apply(CoupState state, CoupAction action, string playerId) =>
        action.Type switch
        {
            "StartGame"       => ApplyStartGame(state),
            "TakeIncome"      => ApplyTakeIncome(state),
            "TakeForeignAid"  => ApplyTakeForeignAid(state),
            "DoCoup"          => ApplyDoCoup(state, action),
            "TakeTax"         => ApplyTakeTax(state),
            "Assassinate"     => ApplyAssassinate(state, action),
            "Steal"           => ApplySteal(state, action),
            "Exchange"        => ApplyExchangeDeclare(state),
            "Challenge"       => ApplyChallenge(state, playerId),
            "Block"           => ApplyBlock(state, playerId, action),
            "Pass"            => ApplyPass(state, playerId),
            "LoseInfluence"   => ApplyLoseInfluence(state, playerId, action),
            "ChooseExchange"  => ApplyChooseExchange(state, action),
            _ => state
        };

    // ── StartGame ─────────────────────────────────────────────────────────────

    private static CoupState ApplyStartGame(CoupState state)
    {
        // Build and shuffle deck (3 of each character)
        var deck = BuildDeck();
        Shuffle(deck);

        // Deal 2 cards to each player and give 2 coins
        var players = state.Players.ToList();
        int deckIndex = 0;
        for (int i = 0; i < players.Count; i++)
        {
            var hand = new List<InfluenceCard>
            {
                new(deck[deckIndex++], Revealed: false),
                new(deck[deckIndex++], Revealed: false)
            };
            players[i] = players[i] with { Influence = hand, Coins = 2, Active = true };
        }

        var remainingDeck = deck.Skip(deckIndex).ToList();

        return state with
        {
            Phase             = CoupPhase.AwaitingResponses,
            Players           = players,
            Deck              = remainingDeck,
            ActivePlayerIndex = 0,
            Pending           = null
        };
    }

    // ── TakeIncome ────────────────────────────────────────────────────────────

    private static CoupState ApplyTakeIncome(CoupState state)
    {
        var players = ModifyPlayer(state.Players, state.Players[state.ActivePlayerIndex].Id,
            p => p with { Coins = p.Coins + 1 });
        return AdvanceTurn(state with { Players = players });
    }

    // ── TakeForeignAid ────────────────────────────────────────────────────────

    private static CoupState ApplyTakeForeignAid(CoupState state)
    {
        var actorId = state.Players[state.ActivePlayerIndex].Id;
        return state with
        {
            Pending = new PendingAction(
                ActionType:           "ForeignAid",
                ActorId:              actorId,
                TargetId:             null,
                Step:                 PendingStep.ActionResponses,
                PassedPlayers:        [],
                BlockerId:            null,
                ChallengerId:         null,
                ExchangeOptions:      null,
                InfluenceLossPlayerId: null
            )
        };
    }

    // ── DoCoup ────────────────────────────────────────────────────────────────

    private static CoupState ApplyDoCoup(CoupState state, CoupAction action)
    {
        var actorId = state.Players[state.ActivePlayerIndex].Id;
        // Deduct 7 coins
        var players = ModifyPlayer(state.Players, actorId, p => p with { Coins = p.Coins - 7 });
        // Target must lose influence — go to InfluenceLoss phase
        var newState = state with
        {
            Players = players,
            Pending = new PendingAction(
                ActionType:           "Coup",
                ActorId:              actorId,
                TargetId:             action.TargetId,
                Step:                 PendingStep.ActionResponses,
                PassedPlayers:        [],
                BlockerId:            null,
                ChallengerId:         null,
                ExchangeOptions:      null,
                InfluenceLossPlayerId: action.TargetId
            ),
            Phase = CoupPhase.InfluenceLoss
        };
        return newState;
    }

    // ── TakeTax ───────────────────────────────────────────────────────────────

    private static CoupState ApplyTakeTax(CoupState state)
    {
        var actorId = state.Players[state.ActivePlayerIndex].Id;
        return state with
        {
            Pending = new PendingAction(
                ActionType:           "Tax",
                ActorId:              actorId,
                TargetId:             null,
                Step:                 PendingStep.ActionResponses,
                PassedPlayers:        [],
                BlockerId:            null,
                ChallengerId:         null,
                ExchangeOptions:      null,
                InfluenceLossPlayerId: null
            )
        };
    }

    // ── Assassinate ───────────────────────────────────────────────────────────

    private static CoupState ApplyAssassinate(CoupState state, CoupAction action)
    {
        var actorId = state.Players[state.ActivePlayerIndex].Id;
        // Deduct 3 coins immediately
        var players = ModifyPlayer(state.Players, actorId, p => p with { Coins = p.Coins - 3 });
        return state with
        {
            Players = players,
            Pending = new PendingAction(
                ActionType:           "Assassinate",
                ActorId:              actorId,
                TargetId:             action.TargetId,
                Step:                 PendingStep.ActionResponses,
                PassedPlayers:        [],
                BlockerId:            null,
                ChallengerId:         null,
                ExchangeOptions:      null,
                InfluenceLossPlayerId: null
            )
        };
    }

    // ── Steal ─────────────────────────────────────────────────────────────────

    private static CoupState ApplySteal(CoupState state, CoupAction action)
    {
        var actorId = state.Players[state.ActivePlayerIndex].Id;
        return state with
        {
            Pending = new PendingAction(
                ActionType:           "Steal",
                ActorId:              actorId,
                TargetId:             action.TargetId,
                Step:                 PendingStep.ActionResponses,
                PassedPlayers:        [],
                BlockerId:            null,
                ChallengerId:         null,
                ExchangeOptions:      null,
                InfluenceLossPlayerId: null
            )
        };
    }

    // ── Exchange declare ──────────────────────────────────────────────────────

    private static CoupState ApplyExchangeDeclare(CoupState state)
    {
        var actorId = state.Players[state.ActivePlayerIndex].Id;
        return state with
        {
            Pending = new PendingAction(
                ActionType:           "Exchange",
                ActorId:              actorId,
                TargetId:             null,
                Step:                 PendingStep.ActionResponses,
                PassedPlayers:        [],
                BlockerId:            null,
                ChallengerId:         null,
                ExchangeOptions:      null,
                InfluenceLossPlayerId: null
            )
        };
    }

    // ── Challenge ─────────────────────────────────────────────────────────────

    private static CoupState ApplyChallenge(CoupState state, string challengerId)
    {
        var pending = state.Pending!;

        if (pending.Step == PendingStep.ActionResponses)
        {
            // Challenging the actor's claimed character
            var claimedCharacter = ActionCharacter(pending.ActionType);
            if (claimedCharacter is null)
            {
                // Action is not character-based — shouldn't happen if validation is correct
                return state;
            }
            var actor = GetPlayer(state, pending.ActorId)!;
            var cardIndex = actor.Influence.FindIndex(c => c.Character == claimedCharacter && !c.Revealed);

            if (cardIndex >= 0)
            {
                // Actor has the card — challenger loses influence; actor draws replacement
                var (newState, newDeck) = ActorWinsChallenge(state, pending.ActorId, claimedCharacter);
                // Challenger must lose influence
                return newState with
                {
                    Phase   = CoupPhase.InfluenceLoss,
                    Deck    = newDeck,
                    Pending = newState.Pending! with
                    {
                        ChallengerId:          challengerId,
                        InfluenceLossPlayerId: challengerId
                    }
                };
            }
            else
            {
                // Actor does NOT have the card — actor loses influence; action fails
                return state with
                {
                    Phase   = CoupPhase.InfluenceLoss,
                    Pending = pending with
                    {
                        ChallengerId:          challengerId,
                        InfluenceLossPlayerId: pending.ActorId
                    }
                };
            }
        }
        else
        {
            // Challenging the blocker's claimed character (Step == BlockResponses)
            // At this point pending.BlockerId is the blocker
            var blockCharacter = pending.ExchangeOptions?.FirstOrDefault();
            // ExchangeOptions[0] stores the blocker's claimed character during BlockResponses
            if (blockCharacter is null) return state;

            var blocker = GetPlayer(state, pending.BlockerId!)!;
            var cardIndex = blocker.Influence.FindIndex(c => c.Character == blockCharacter && !c.Revealed);

            if (cardIndex >= 0)
            {
                // Blocker has the card — challenger (actor) loses influence; block stands
                var (newState, newDeck) = ActorWinsChallenge(state, pending.BlockerId!, blockCharacter);
                // Actor (original) loses influence; refund coins if applicable
                var refunded = RefundActionCost(newState, pending);
                return refunded with
                {
                    Phase   = CoupPhase.InfluenceLoss,
                    Deck    = newDeck,
                    Pending = refunded.Pending! with
                    {
                        ChallengerId:          challengerId,
                        InfluenceLossPlayerId: pending.ActorId
                    }
                };
            }
            else
            {
                // Blocker does NOT have the card — blocker loses influence; block fails, action proceeds
                return state with
                {
                    Phase   = CoupPhase.InfluenceLoss,
                    Pending = pending with
                    {
                        ChallengerId:          challengerId,
                        InfluenceLossPlayerId: pending.BlockerId
                    }
                };
            }
        }
    }

    // ── Block ─────────────────────────────────────────────────────────────────

    private static CoupState ApplyBlock(CoupState state, string blockerId, CoupAction action)
    {
        var pending = state.Pending!;
        // Store the blocker's claimed character in ExchangeOptions[0] temporarily
        return state with
        {
            Pending = pending with
            {
                Step          = PendingStep.BlockResponses,
                BlockerId     = blockerId,
                PassedPlayers = [],
                // Reuse ExchangeOptions to store the blocker character claim
                ExchangeOptions = [action.Character!]
            }
        };
    }

    // ── Pass ──────────────────────────────────────────────────────────────────

    private static CoupState ApplyPass(CoupState state, string playerId)
    {
        var pending = state.Pending!;
        var passed  = pending.PassedPlayers.Append(playerId).ToList();

        if (pending.Step == PendingStep.ActionResponses)
        {
            // All eligible players (everyone except actor) must pass for action to proceed
            var eligibleCount = EligibleResponseCount(state, pending);
            if (passed.Count >= eligibleCount)
            {
                // All passed — resolve the action
                return ResolveAction(state with { Pending = pending with { PassedPlayers = passed } });
            }
            return state with { Pending = pending with { PassedPlayers = passed } };
        }
        else
        {
            // BlockResponses — only the actor passes; that means actor accepts the block
            // (eligibleCount = 1, just the actor)
            // Block succeeds — action is cancelled, refund costs, advance turn
            var refunded = RefundActionCost(state, pending);
            return AdvanceTurn(refunded with { Pending = null });
        }
    }

    // ── LoseInfluence ─────────────────────────────────────────────────────────

    private static CoupState ApplyLoseInfluence(CoupState state, string playerId, CoupAction action)
    {
        var pending = state.Pending!;

        // Reveal the chosen card
        var players = ModifyPlayer(state.Players, playerId, p =>
        {
            var influence = p.Influence.ToList();
            var idx = influence.FindIndex(c => c.Character == action.InfluenceToLose && !c.Revealed);
            if (idx >= 0) influence[idx] = influence[idx] with { Revealed = true };
            var active = influence.Any(c => !c.Revealed);
            return p with { Influence = influence, Active = active };
        });

        var newState = state with { Players = players };

        // Check for winner immediately
        var winner = CheckWinner(players);
        if (winner is not null)
        {
            return newState with
            {
                Phase   = CoupPhase.Finished,
                Winner  = winner,
                Pending = null
            };
        }

        // Determine what happens next based on context
        var pendingActionType = pending.ActionType;
        var challengerId      = pending.ChallengerId;
        var influenceLossId   = pending.InfluenceLossPlayerId;

        // Was this a Coup? If so, just advance turn
        if (pendingActionType == "Coup")
        {
            return AdvanceTurn(newState with { Pending = null });
        }

        // Was a challenge resolved?
        if (challengerId is not null)
        {
            if (influenceLossId == challengerId)
            {
                // Challenger lost — action/block succeeds; continue resolution
                return ContinueAfterChallengeActorWon(newState with
                {
                    Phase   = CoupPhase.AwaitingResponses,
                    Pending = pending with { InfluenceLossPlayerId = null, ChallengerId = null }
                });
            }
            else
            {
                // Actor/blocker lost the challenge — action/block fails
                return ActionFailedAfterChallenge(newState, pending);
            }
        }

        // Fallback: this should not normally be reached
        return AdvanceTurn(newState with { Pending = null });
    }

    // ── ChooseExchange ────────────────────────────────────────────────────────

    private static CoupState ApplyChooseExchange(CoupState state, CoupAction action)
    {
        var pending  = state.Pending!;
        var actorId  = pending.ActorId;
        var keepCards = action.KeepCards!;

        // Pool is ExchangeOptions (actor's 2 hand + 2 drawn = 4 cards)
        var pool = pending.ExchangeOptions!.ToList();

        // Determine which cards to return to deck
        var toReturn = pool.ToList();
        foreach (var card in keepCards)
        {
            toReturn.Remove(card);
        }

        // Update actor's influence with kept cards (only unrevealed slots)
        var players = ModifyPlayer(state.Players, actorId, p =>
        {
            // Keep revealed cards as-is; replace unrevealed cards with kept cards
            var influence = p.Influence.ToList();
            int keepIdx = 0;
            for (int i = 0; i < influence.Count && keepIdx < keepCards.Count; i++)
            {
                if (!influence[i].Revealed)
                {
                    influence[i] = new InfluenceCard(keepCards[keepIdx++], Revealed: false);
                }
            }
            return p with { Influence = influence };
        });

        // Return unused cards to deck and shuffle
        var deck = state.Deck.Concat(toReturn).ToList();
        Shuffle(deck);

        return AdvanceTurn(state with
        {
            Players = players,
            Deck    = deck,
            Pending = null
        });
    }

    // ── Resolution helpers ────────────────────────────────────────────────────

    /// <summary>Resolves the pending action assuming it succeeded (all passed).</summary>
    private static CoupState ResolveAction(CoupState state)
    {
        var pending = state.Pending!;
        switch (pending.ActionType)
        {
            case "ForeignAid":
            {
                var players = ModifyPlayer(state.Players, pending.ActorId,
                    p => p with { Coins = p.Coins + 2 });
                return AdvanceTurn(state with { Players = players, Pending = null });
            }
            case "Tax":
            {
                var players = ModifyPlayer(state.Players, pending.ActorId,
                    p => p with { Coins = p.Coins + 3 });
                return AdvanceTurn(state with { Players = players, Pending = null });
            }
            case "Assassinate":
            {
                // Target must lose influence
                return state with
                {
                    Phase   = CoupPhase.InfluenceLoss,
                    Pending = pending with
                    {
                        InfluenceLossPlayerId = pending.TargetId
                    }
                };
            }
            case "Steal":
            {
                // Transfer up to 2 coins from target to actor
                var target = GetPlayer(state, pending.TargetId!)!;
                var stolen = Math.Min(2, target.Coins);
                var players = ModifyPlayer(state.Players, pending.TargetId!,
                    p => p with { Coins = p.Coins - stolen });
                players = ModifyPlayer(players, pending.ActorId,
                    p => p with { Coins = p.Coins + stolen });
                return AdvanceTurn(state with { Players = players, Pending = null });
            }
            case "Exchange":
            {
                // Draw 2 cards from deck, combine with actor's hand
                var actor    = GetPlayer(state, pending.ActorId)!;
                var deck     = state.Deck.ToList();
                var drawn    = new List<string>();
                for (int i = 0; i < 2 && deck.Count > 0; i++)
                {
                    drawn.Add(deck[0]);
                    deck.RemoveAt(0);
                }
                // Pool: actor's unrevealed cards + drawn cards
                var pool = actor.Influence
                    .Where(c => !c.Revealed)
                    .Select(c => c.Character)
                    .Concat(drawn)
                    .ToList();

                return state with
                {
                    Phase = CoupPhase.Exchange,
                    Deck  = deck,
                    Pending = pending with
                    {
                        ExchangeOptions = pool
                    }
                };
            }
            default:
                return AdvanceTurn(state with { Pending = null });
        }
    }

    /// <summary>
    /// After a challenge where the actor/blocker WON (held the card), the challenged card is
    /// revealed, shuffled back, and a new card drawn. Returns (newState, newDeck).
    /// </summary>
    private static (CoupState, List<string>) ActorWinsChallenge(
        CoupState state, string actorId, string character)
    {
        var deck    = state.Deck.ToList();
        var players = ModifyPlayer(state.Players, actorId, p =>
        {
            var influence = p.Influence.ToList();
            var idx = influence.FindIndex(c => c.Character == character && !c.Revealed);
            if (idx >= 0 && deck.Count > 0)
            {
                // Shuffle old card back, draw new one
                deck.Add(character);
                Shuffle(deck);
                influence[idx] = new InfluenceCard(deck[0], Revealed: false);
                deck.RemoveAt(0);
            }
            return p with { Influence = influence };
        });
        return (state with { Players = players }, deck);
    }

    /// <summary>
    /// After a challenge where the actor won, continue the action resolution.
    /// </summary>
    private static CoupState ContinueAfterChallengeActorWon(CoupState state)
    {
        var pending = state.Pending!;
        // Count remaining eligible responders (excluding now-challenged player who lost)
        var eligibleCount = EligibleResponseCount(state, pending);
        if (pending.PassedPlayers.Count >= eligibleCount)
        {
            return ResolveAction(state);
        }
        return state;
    }

    /// <summary>
    /// After a challenge where the actor/blocker LOST — action or block fails.
    /// </summary>
    private static CoupState ActionFailedAfterChallenge(CoupState state, PendingAction pending)
    {
        if (pending.Step == PendingStep.BlockResponses)
        {
            // Block challenge: blocker lost, block fails → action proceeds
            // Restart action with no block, cleared challenge info
            var newPending = pending with
            {
                Step           = PendingStep.ActionResponses,
                BlockerId      = null,
                ChallengerId   = null,
                PassedPlayers  = [],
                ExchangeOptions = null,
                InfluenceLossPlayerId = null
            };
            var newState = state with
            {
                Phase   = CoupPhase.AwaitingResponses,
                Pending = newPending
            };
            // Check if action now auto-resolves (no remaining opponents)
            var eligible = EligibleResponseCount(newState, newPending);
            if (eligible == 0)
                return ResolveAction(newState);
            return newState;
        }
        else
        {
            // Action challenge: actor lost — action fails; refund costs, advance turn
            var refunded = RefundActionCost(state, pending);
            return AdvanceTurn(refunded with { Pending = null, Phase = CoupPhase.AwaitingResponses });
        }
    }

    /// <summary>Refunds coins spent on an action that was blocked or lost on challenge.</summary>
    private static CoupState RefundActionCost(CoupState state, PendingAction pending)
    {
        int refund = pending.ActionType switch
        {
            "Assassinate" => 3,
            _ => 0
        };
        if (refund == 0) return state;
        var players = ModifyPlayer(state.Players, pending.ActorId,
            p => p with { Coins = p.Coins + refund });
        return state with { Players = players };
    }

    /// <summary>Returns the number of players who must pass before an action auto-resolves.</summary>
    private static int EligibleResponseCount(CoupState state, PendingAction pending)
    {
        if (pending.Step == PendingStep.BlockResponses)
        {
            // Only the original actor can challenge/pass a block
            return 1;
        }

        // ActionResponses: all active players except the actor
        var activeOthers = state.Players
            .Where(p => p.Active && p.Id != pending.ActorId)
            .Count();

        return activeOthers;
    }

    // ── Turn management ───────────────────────────────────────────────────────

    private static CoupState AdvanceTurn(CoupState state)
    {
        var next = NextActivePlayerIndex(state.Players, state.ActivePlayerIndex);
        return state with
        {
            Phase             = CoupPhase.AwaitingResponses,
            ActivePlayerIndex = next,
            Pending           = null
        };
    }

    private static int NextActivePlayerIndex(List<CoupPlayer> players, int currentIndex)
    {
        int total = players.Count;
        for (int i = 1; i <= total; i++)
        {
            int idx = (currentIndex + i) % total;
            if (players[idx].Active) return idx;
        }
        return currentIndex;
    }

    // ── Utility helpers ───────────────────────────────────────────────────────

    private static string? CheckWinner(List<CoupPlayer> players)
    {
        var active = players.Where(p => p.Active).ToList();
        return active.Count == 1 ? active[0].Id : null;
    }

    private static List<string> BuildDeck()
    {
        var deck = new List<string>(15);
        foreach (var ch in CoupConstants.Characters)
            for (int i = 0; i < 3; i++)
                deck.Add(ch);
        return deck;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static CoupPlayer? GetPlayer(CoupState state, string id) =>
        state.Players.FirstOrDefault(p => p.Id == id);

    private static List<CoupPlayer> ModifyPlayer(
        List<CoupPlayer> players, string id, Func<CoupPlayer, CoupPlayer> modify)
        => players.Select(p => p.Id == id ? modify(p) : p).ToList();

    /// <summary>Returns the character a given action claims (null if no character claim).</summary>
    private static string? ActionCharacter(string actionType) => actionType switch
    {
        "Tax"         => "Duke",
        "Assassinate" => "Assassin",
        "Steal"       => "Captain",
        "Exchange"    => "Ambassador",
        _             => null
    };

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
