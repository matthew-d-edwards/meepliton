using System.Text.Json;
using Meepliton.Contracts;
using Meepliton.Games.DeadMansSwitch.Models;

namespace Meepliton.Games.DeadMansSwitch;

/// <summary>
/// Dead Man's Switch — disc-based bluffing game for 3–6 players.
/// Players place face-down discs (roses or skulls), then bid on how many
/// roses the Challenger can reveal without hitting a skull.
/// First player to win 2 rounds wins the game.
/// </summary>
public class DeadMansSwitchModule : IGameModule, IGameHandler
{
    public string GameId      => "deadmansswitch";
    public string Name        => "Dead Man's Switch";
    public string Description => "Place your devices and call the job. Defuse them all without triggering the switch.";
    public int    MinPlayers  => 3;
    public int    MaxPlayers  => 6;
    public bool   AllowLateJoin => false;
    public bool   SupportsAsync => false;
    public bool   SupportsUndo  => false;
    public string? ThumbnailUrl => null;
    public bool HasStateProjection => true;

    // ── IGameHandler ──────────────────────────────────────────────────────────

    public GameResult Handle(GameContext ctx)
    {
        var state  = Deserialize<DeadMansSwitchState>(ctx.CurrentState);
        var action = Deserialize<DeadMansSwitchAction>(ctx.Action);
        var error  = Validate(state, action, ctx.PlayerId);
        if (error is not null) return new GameResult(ctx.CurrentState, error);
        var newState    = Apply(state, action, ctx.PlayerId);
        var newStateDoc = Serialize(newState);
        if (newState.Phase == DeadMansSwitchPhase.Finished && newState.Winner is not null)
            return new GameResult(newStateDoc, Effects: [new GameOverEffect(newState.Winner)]);
        return new GameResult(newStateDoc);
    }

    // ── Per-player state projection ───────────────────────────────────────────

    JsonDocument? IGameModule.ProjectStateForPlayer(JsonDocument fullState, string playerId)
    {
        var state = Deserialize<DeadMansSwitchState>(fullState);
        if (state is null) return null;
        var projected = ProjectForPlayer(state, playerId);
        if (projected is null) return null;
        return Serialize(projected);
    }

    private static DeadMansSwitchState? ProjectForPlayer(DeadMansSwitchState fullState, string playerId)
    {
        // RoundOver and Finished — full state visible to all
        if (fullState.Phase is DeadMansSwitchPhase.RoundOver or DeadMansSwitchPhase.Finished)
            return fullState;

        // Placing / Bidding / Revealing / DiscardChoice: hide opponent stacks
        // During Revealing / DiscardChoice: include flipped discs for all players
        bool isRevealPhase = fullState.Phase is DeadMansSwitchPhase.Revealing
                          or DeadMansSwitchPhase.DiscardChoice;

        var projectedPlayers = fullState.Players.Select(p =>
        {
            if (p.Id == playerId)
                return p; // own stack fully visible

            if (isRevealPhase)
            {
                // Include only flipped discs; hidden discs removed
                var visibleStack = p.Stack.Where(d => d.Flipped).ToList();
                return p with { Stack = visibleStack };
            }

            // Placing / Bidding: opponent stack is empty
            return p with { Stack = [] };
        }).ToList();

        return fullState with { Players = projectedPlayers };
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    JsonDocument IGameModule.CreateInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options)
        => Serialize(CreateInitialState(players));

    public DeadMansSwitchState CreateInitialState(IReadOnlyList<PlayerInfo> players)
    {
        if (players.Count < MinPlayers || players.Count > MaxPlayers)
            throw new InvalidOperationException(
                $"Dead Man's Switch requires {MinPlayers}–{MaxPlayers} players, got {players.Count}.");

        var devicePlayers = players.Select(p => new DevicePlayer(
            Id:          p.Id,
            DisplayName: p.DisplayName,
            AvatarUrl:   p.AvatarUrl,
            SeatIndex:   p.SeatIndex,
            Stack:       [],
            StackCount:  0,
            RosesOwned:  3,
            SkullOwned:  true,
            PointsWon:   0,
            Active:      true,
            Passed:      false
        )).ToList();

        return new DeadMansSwitchState(
            Phase:                     DeadMansSwitchPhase.Placing,
            Players:                   devicePlayers,
            CurrentPlayerIndex:        0,
            CurrentBid:                0,
            TotalDiscsOnTable:         0,
            ChallengerId:              null,
            NextRoundFirstPlayerIndex: 0,
            LastFlip:                  null,
            Winner:                    null,
            RoundNumber:               1
        );
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    public string? Validate(DeadMansSwitchState state, DeadMansSwitchAction action, string playerId)
    {
        if (state.Phase == DeadMansSwitchPhase.Finished)
            return "The game is over.";

        switch (action.Type)
        {
            case "StartGame":
                return "The game has already started.";

            case "PlaceDisc":
            {
                if (state.Phase != DeadMansSwitchPhase.Placing)
                    return "You can only arm a device during the Placing phase.";
                if (!IsCurrentPlayer(state, playerId))
                    return "It is not your turn.";
                if (action.DiscType is null)
                    return "You must choose whether to arm a dud (Rose) or a trigger (Skull).";
                var player = state.Players[state.CurrentPlayerIndex];
                int totalDiscsInHand = player.RosesOwned + (player.SkullOwned ? 1 : 0);
                if (player.StackCount >= totalDiscsInHand)
                    return "You have no devices left to arm.";
                var alreadyPlacedRoses = player.Stack.Count(d => d.Type == DiscType.Rose);
                var alreadyPlacedSkull = player.Stack.Any(d => d.Type == DiscType.Skull);
                if (action.DiscType == DiscType.Rose && alreadyPlacedRoses >= player.RosesOwned)
                    return "You have no roses left to arm.";
                if (action.DiscType == DiscType.Skull && (!player.SkullOwned || alreadyPlacedSkull))
                    return "You have no trigger left to arm.";
                return null;
            }

            case "StartBid":
            {
                if (state.Phase != DeadMansSwitchPhase.Placing)
                    return "You can only commit to the job during the Placing phase.";
                if (!IsCurrentPlayer(state, playerId))
                    return "It is not your turn.";
                var player = state.Players[state.CurrentPlayerIndex];
                if (player.StackCount < 1)
                    return "You must arm at least one device before committing to the job.";
                if (action.TargetCount is null)
                    return "Missing target count.";
                if (action.TargetCount < 1 || action.TargetCount > state.TotalDiscsOnTable)
                    return $"Target count must be between 1 and {state.TotalDiscsOnTable}.";
                return null;
            }

            case "RaiseBid":
            {
                if (state.Phase != DeadMansSwitchPhase.Bidding)
                    return "You can only raise the bid during the Bidding phase.";
                if (!IsCurrentPlayer(state, playerId))
                    return "It is not your turn.";
                var player = state.Players[state.CurrentPlayerIndex];
                if (player.Passed)
                    return "You have already passed.";
                if (action.NewBid is null)
                    return "Missing new bid.";
                if (action.NewBid <= state.CurrentBid)
                    return $"Your bid must be higher than the current bid of {state.CurrentBid}.";
                if (action.NewBid > state.TotalDiscsOnTable)
                    return $"Bid cannot exceed total discs on the table ({state.TotalDiscsOnTable}).";
                return null;
            }

            case "Pass":
            {
                if (state.Phase != DeadMansSwitchPhase.Bidding)
                    return "You can only pass during the Bidding phase.";
                if (!IsCurrentPlayer(state, playerId))
                    return "It is not your turn.";
                var player = state.Players[state.CurrentPlayerIndex];
                if (player.Passed)
                    return "You have already passed.";
                return null;
            }

            case "FlipDisc":
            {
                if (state.Phase != DeadMansSwitchPhase.Revealing)
                    return "You can only flip discs during the Revealing phase.";
                if (state.ChallengerId != playerId)
                    return "Only the Challenger may flip discs.";
                if (action.TargetPlayerId is null)
                    return "Missing target player ID.";
                var target = state.Players.FirstOrDefault(p => p.Id == action.TargetPlayerId);
                if (target is null)
                    return "Target player not found.";

                // Own-stack-first constraint
                var challenger = state.Players.First(p => p.Id == playerId);
                bool challengerHasUnflipped = challenger.Stack.Any(d => !d.Flipped);
                if (challengerHasUnflipped && action.TargetPlayerId != playerId)
                    return "You must flip your own devices first.";

                bool targetHasUnflipped = target.Stack.Any(d => !d.Flipped);
                if (!targetHasUnflipped)
                    return "That player's stack has no more unflipped devices.";
                return null;
            }

            case "DiscardDisc":
            {
                if (state.Phase != DeadMansSwitchPhase.DiscardChoice)
                    return "You can only discard during the DiscardChoice phase.";
                if (state.ChallengerId != playerId)
                    return "Only the Challenger may discard.";
                if (action.DiscType is null)
                    return "Missing disc type.";
                var challenger = state.Players.First(p => p.Id == playerId);
                if (action.DiscType == DiscType.Rose && challenger.RosesOwned == 0)
                    return "You have no roses to discard.";
                if (action.DiscType == DiscType.Skull && !challenger.SkullOwned)
                    return "You do not own a skull to discard.";
                return null;
            }

            case "StartNextRound":
            {
                if (state.Phase != DeadMansSwitchPhase.RoundOver)
                    return "StartNextRound is only valid during the RoundOver phase.";
                var player = state.Players.FirstOrDefault(p => p.Id == playerId);
                if (player is null || !player.Active)
                    return "Only active players can start the next round.";
                return null;
            }

            default:
                return $"Unknown action type: {action.Type}";
        }
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    public DeadMansSwitchState Apply(DeadMansSwitchState state, DeadMansSwitchAction action, string playerId) =>
        action.Type switch
        {
            "PlaceDisc"      => ApplyPlaceDisc(state, playerId, action.DiscType!.Value),
            "StartBid"       => ApplyStartBid(state, playerId, action.TargetCount!.Value),
            "RaiseBid"       => ApplyRaiseBid(state, action.NewBid!.Value),
            "Pass"           => ApplyPass(state),
            "FlipDisc"       => ApplyFlipDisc(state, playerId, action.TargetPlayerId!),
            "DiscardDisc"    => ApplyDiscardDisc(state, playerId, action.DiscType!.Value),
            "StartNextRound" => ApplyStartNextRound(state),
            _                => state
        };

    // ── PlaceDisc ─────────────────────────────────────────────────────────────

    private static DeadMansSwitchState ApplyPlaceDisc(DeadMansSwitchState state, string playerId, DiscType chosenType)
    {
        var player = state.Players[state.CurrentPlayerIndex];

        var newSlot = new DiscSlot(chosenType, Flipped: false);

        // Add to top of stack (index 0 = top in placement order)
        var newStack = new List<DiscSlot>(player.Stack) { newSlot };
        var updatedPlayer = player with { Stack = newStack, StackCount = newStack.Count };

        var players = ReplacePlayer(state.Players, updatedPlayer);
        var nextIndex = NextActivePlayerIndex(players, state.CurrentPlayerIndex);

        return state with
        {
            Players           = players,
            CurrentPlayerIndex = nextIndex,
            TotalDiscsOnTable  = state.TotalDiscsOnTable + 1
        };
    }

    // ── StartBid ──────────────────────────────────────────────────────────────

    private static DeadMansSwitchState ApplyStartBid(DeadMansSwitchState state, string playerId, int targetCount)
    {
        // Reset all passed flags; bidding begins with the player who started it
        var players = state.Players.Select(p => p with { Passed = false }).ToList();
        var challengerIdx = players.FindIndex(p => p.Id == playerId);

        return state with
        {
            Phase                     = DeadMansSwitchPhase.Bidding,
            Players                   = players,
            CurrentBid                = targetCount,
            ChallengerId              = playerId,
            CurrentPlayerIndex        = NextActivePlayerIndex(players, challengerIdx),
            NextRoundFirstPlayerIndex = challengerIdx  // bid-opener leads on success
        };
    }

    // ── RaiseBid ──────────────────────────────────────────────────────────────

    private static DeadMansSwitchState ApplyRaiseBid(DeadMansSwitchState state, int newBid)
    {
        // Update challenger to the player who raised
        var currentPlayer = state.Players[state.CurrentPlayerIndex];
        var newChallengerId = currentPlayer.Id;

        // If bid == totalDiscsOnTable → auto-transition to Revealing
        if (newBid == state.TotalDiscsOnTable)
        {
            return state with
            {
                Phase        = DeadMansSwitchPhase.Revealing,
                CurrentBid   = newBid,
                ChallengerId = newChallengerId,
                CurrentPlayerIndex = state.Players.FindIndex(p => p.Id == newChallengerId)
            };
        }

        // Otherwise advance to next non-passed active player
        var nextIndex = NextNonPassedActivePlayerIndex(state.Players, state.CurrentPlayerIndex);

        return state with
        {
            CurrentBid         = newBid,
            ChallengerId       = newChallengerId,
            CurrentPlayerIndex = nextIndex
        };
    }

    // ── Pass ──────────────────────────────────────────────────────────────────

    private static DeadMansSwitchState ApplyPass(DeadMansSwitchState state)
    {
        var currentPlayer = state.Players[state.CurrentPlayerIndex];
        var updatedPlayer = currentPlayer with { Passed = true };
        var players = ReplacePlayer(state.Players, updatedPlayer);

        // Count non-passed active players
        var nonPassed = players.Where(p => p.Active && !p.Passed).ToList();

        if (nonPassed.Count == 1)
        {
            // That one player becomes Challenger; transition to Revealing
            var challenger = nonPassed[0];
            var challengerIdx = players.FindIndex(p => p.Id == challenger.Id);
            return state with
            {
                Phase              = DeadMansSwitchPhase.Revealing,
                Players            = players,
                ChallengerId       = challenger.Id,
                CurrentPlayerIndex = challengerIdx
            };
        }

        // Advance to next non-passed active player
        var nextIndex = NextNonPassedActivePlayerIndex(players, state.CurrentPlayerIndex);
        return state with
        {
            Players            = players,
            CurrentPlayerIndex = nextIndex
        };
    }

    // ── FlipDisc ──────────────────────────────────────────────────────────────

    private static DeadMansSwitchState ApplyFlipDisc(DeadMansSwitchState state, string challengerId, string targetPlayerId)
    {
        var targetPlayer = state.Players.First(p => p.Id == targetPlayerId);

        // Find the top unflipped disc (last in list = most recently placed = top of stack)
        var topUnflippedIndex = -1;
        for (int i = targetPlayer.Stack.Count - 1; i >= 0; i--)
        {
            if (!targetPlayer.Stack[i].Flipped)
            {
                topUnflippedIndex = i;
                break;
            }
        }

        // Flip the disc
        var newStack = targetPlayer.Stack.ToList();
        var flippedDisc = newStack[topUnflippedIndex] with { Flipped = true };
        newStack[topUnflippedIndex] = flippedDisc;
        var updatedTarget = targetPlayer with { Stack = newStack };
        var players = ReplacePlayer(state.Players, updatedTarget);

        // Count total flipped across all players
        int totalFlipped = players.Sum(p => p.Stack.Count(d => d.Flipped));

        var flipLog = new FlipLog(
            FlippedByPlayerId: challengerId,
            StackOwnerId:      targetPlayerId,
            Result:            flippedDisc.Type,
            FlipNumber:        totalFlipped
        );

        // Skull hit?
        if (flippedDisc.Type == DiscType.Skull)
        {
            if (targetPlayerId == challengerId)
            {
                // Own skull — transition to DiscardChoice (or auto-discard)
                var newState = state with
                {
                    Players  = players,
                    LastFlip = flipLog
                };
                return HandleOwnSkullHit(newState, challengerId);
            }
            else
            {
                // Opponent skull — randomly discard one of challenger's discs
                return HandleOpponentSkullHit(state with { Players = players, LastFlip = flipLog },
                    challengerId, targetPlayerId);
            }
        }

        // Rose flipped — check for success
        if (totalFlipped >= state.CurrentBid)
        {
            return HandleRevealSuccess(state with { Players = players, LastFlip = flipLog }, challengerId);
        }

        return state with
        {
            Players  = players,
            LastFlip = flipLog
        };
    }

    private static DeadMansSwitchState HandleOwnSkullHit(DeadMansSwitchState state, string challengerId)
    {
        var challengerIdx = state.Players.FindIndex(p => p.Id == challengerId);
        var challenger = state.Players[challengerIdx];

        // Check auto-discard condition
        bool hasRoses = challenger.RosesOwned > 0;
        bool hasSkull = challenger.SkullOwned;
        bool onlyOneType = !(hasRoses && hasSkull);

        if (onlyOneType)
        {
            // Auto-discard
            DiscType autoDiscard = hasRoses ? DiscType.Rose : DiscType.Skull;
            return ApplyDiscardAndAdvance(state, challengerId, autoDiscard, challengerIdx);
        }

        return state with
        {
            Phase = DeadMansSwitchPhase.DiscardChoice,
            NextRoundFirstPlayerIndex = challengerIdx
        };
    }

    private static DeadMansSwitchState HandleOpponentSkullHit(
        DeadMansSwitchState state,
        string challengerId,
        string skullOwnerId)
    {
        var challengerIdx = state.Players.FindIndex(p => p.Id == challengerId);
        var skullOwnerIdx = state.Players.FindIndex(p => p.Id == skullOwnerId);
        var challenger = state.Players[challengerIdx];

        // Randomly pick one disc from challenger's hand to remove
        bool hasRoses = challenger.RosesOwned > 0;
        bool hasSkull = challenger.SkullOwned;
        int totalDiscs = challenger.RosesOwned + (hasSkull ? 1 : 0);

        DiscType randomDiscard;
        if (totalDiscs == 0)
        {
            // Should not happen; pick skull as fallback
            randomDiscard = DiscType.Skull;
        }
        else
        {
            var pick = Random.Shared.Next(totalDiscs);
            randomDiscard = pick < challenger.RosesOwned ? DiscType.Rose : DiscType.Skull;
        }

        var updatedChallenger = randomDiscard == DiscType.Rose
            ? challenger with { RosesOwned = challenger.RosesOwned - 1 }
            : challenger with { SkullOwned = false };

        // Check elimination
        int remaining = updatedChallenger.RosesOwned + (updatedChallenger.SkullOwned ? 1 : 0);
        if (remaining == 0)
            updatedChallenger = updatedChallenger with { Active = false };

        var players = ReplacePlayer(state.Players, updatedChallenger);

        var newState = state with
        {
            Players                   = players,
            NextRoundFirstPlayerIndex = skullOwnerIdx,
            Phase                     = DeadMansSwitchPhase.RoundOver
        };

        // Check if only one active player remains
        var activePlayers = players.Where(p => p.Active).ToList();
        if (activePlayers.Count == 1)
        {
            return newState with
            {
                Phase  = DeadMansSwitchPhase.Finished,
                Winner = activePlayers[0].Id
            };
        }

        return newState;
    }

    private static DeadMansSwitchState HandleRevealSuccess(DeadMansSwitchState state, string challengerId)
    {
        var challengerIdx = state.Players.FindIndex(p => p.Id == challengerId);
        var challenger = state.Players[challengerIdx];
        var updatedChallenger = challenger with { PointsWon = challenger.PointsWon + 1 };
        var players = ReplacePlayer(state.Players, updatedChallenger);

        if (updatedChallenger.PointsWon >= 2)
        {
            return state with
            {
                Phase   = DeadMansSwitchPhase.Finished,
                Players = players,
                Winner  = challengerId
            };
        }

        return state with
        {
            Phase                     = DeadMansSwitchPhase.RoundOver,
            Players                   = players,
            // NextRoundFirstPlayerIndex remains as set when bidding started (bid-opener leads)
        };
    }

    // ── DiscardDisc ───────────────────────────────────────────────────────────

    private static DeadMansSwitchState ApplyDiscardDisc(DeadMansSwitchState state, string challengerId, DiscType discType)
    {
        var challengerIdx = state.Players.FindIndex(p => p.Id == challengerId);
        return ApplyDiscardAndAdvance(state, challengerId, discType, challengerIdx);
    }

    private static DeadMansSwitchState ApplyDiscardAndAdvance(
        DeadMansSwitchState state,
        string challengerId,
        DiscType discType,
        int challengerIdx)
    {
        var challenger = state.Players[challengerIdx];
        var updatedChallenger = discType == DiscType.Rose
            ? challenger with { RosesOwned = challenger.RosesOwned - 1 }
            : challenger with { SkullOwned = false };

        // Check elimination
        int remaining = updatedChallenger.RosesOwned + (updatedChallenger.SkullOwned ? 1 : 0);
        if (remaining == 0)
            updatedChallenger = updatedChallenger with { Active = false };

        var players = ReplacePlayer(state.Players, updatedChallenger);

        var newState = state with
        {
            Players                   = players,
            NextRoundFirstPlayerIndex = challengerIdx,
            Phase                     = DeadMansSwitchPhase.RoundOver
        };

        // Check if only one active player remains
        var activePlayers = players.Where(p => p.Active).ToList();
        if (activePlayers.Count == 1)
        {
            return newState with
            {
                Phase  = DeadMansSwitchPhase.Finished,
                Winner = activePlayers[0].Id
            };
        }

        return newState;
    }

    // ── StartNextRound ────────────────────────────────────────────────────────

    private static DeadMansSwitchState ApplyStartNextRound(DeadMansSwitchState state)
    {
        var players = state.Players.Select(p =>
        {
            if (!p.Active) return p;
            return p with
            {
                Stack      = [],
                StackCount = 0,
                Passed     = false
            };
        }).ToList();

        return state with
        {
            Phase              = DeadMansSwitchPhase.Placing,
            Players            = players,
            CurrentPlayerIndex = state.NextRoundFirstPlayerIndex,
            CurrentBid         = 0,
            TotalDiscsOnTable  = 0,
            ChallengerId       = null,
            LastFlip           = null,
            RoundNumber        = state.RoundNumber + 1
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsCurrentPlayer(DeadMansSwitchState state, string playerId) =>
        state.Players[state.CurrentPlayerIndex].Id == playerId;

    private static List<DevicePlayer> ReplacePlayer(List<DevicePlayer> players, DevicePlayer updated)
    {
        var result = players.ToList();
        var idx = result.FindIndex(p => p.Id == updated.Id);
        if (idx >= 0) result[idx] = updated;
        return result;
    }

    private static int NextActivePlayerIndex(List<DevicePlayer> players, int currentIndex)
    {
        int total = players.Count;
        for (int i = 1; i <= total; i++)
        {
            int idx = (currentIndex + i) % total;
            if (players[idx].Active) return idx;
        }
        return currentIndex;
    }

    private static int NextNonPassedActivePlayerIndex(List<DevicePlayer> players, int currentIndex)
    {
        int total = players.Count;
        for (int i = 1; i <= total; i++)
        {
            int idx = (currentIndex + i) % total;
            if (players[idx].Active && !players[idx].Passed) return idx;
        }
        return currentIndex;
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
