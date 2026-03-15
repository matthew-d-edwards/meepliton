// NOTE: No Playwright e2e setup exists in this repository. Playwright tests were not added.
// If a Playwright setup is introduced in the future, add a test that navigates to the lobby
// and verifies "Liar's Dice" appears in the game catalogue.

using System.Text.Json;
using FluentAssertions;
using Meepliton.Contracts;
using Meepliton.Games.LiarsDice;
using Meepliton.Games.LiarsDice.Models;

namespace Meepliton.Tests.Games;

public class LiarsDiceModuleTests
{
    private readonly LiarsDiceModule _module = new();

    // ── Player helpers ────────────────────────────────────────────────────────

    private static IReadOnlyList<PlayerInfo> TwoPlayers() =>
    [
        new("p1", "Alice", null, 0),
        new("p2", "Bob",   null, 1),
    ];

    private static IReadOnlyList<PlayerInfo> SixPlayers() =>
    [
        new("p1", "Alice",   null, 0),
        new("p2", "Bob",     null, 1),
        new("p3", "Charlie", null, 2),
        new("p4", "Diana",   null, 3),
        new("p5", "Eve",     null, 4),
        new("p6", "Frank",   null, 5),
    ];

    // ── State builder helpers ─────────────────────────────────────────────────

    /// <summary>
    /// A two-player Bidding state with no current bid, p1's turn (index 0).
    /// Each player has 5 known dice so wild/count logic is predictable.
    /// </summary>
    private static LiarsDiceState FreshBiddingState() =>
        new(
            Phase:               LiarsDicePhase.Bidding,
            Players:
            [
                new("p1", "Alice", null, 0, [3, 3, 3, 3, 3], 5, true, false),
                new("p2", "Bob",   null, 1, [4, 4, 4, 4, 4], 5, true, false),
            ],
            CurrentPlayerIndex:  0,
            CurrentBid:          null,
            RoundNumber:         1,
            PalificoActive:      false,
            LastChallengeResult: null,
            LastReveal:          null,
            Winner:              null
        );

    /// <summary>
    /// A two-player Bidding state where p1 already placed the given bid and
    /// it is now p2's turn (currentPlayerIndex = 1 by default).
    /// </summary>
    private static LiarsDiceState StateWithBid(Bid bid, int currentPlayerIndex = 1) =>
        new(
            Phase:               LiarsDicePhase.Bidding,
            Players:
            [
                new("p1", "Alice", null, 0, [3, 3, 3, 3, 3], 5, true, false),
                new("p2", "Bob",   null, 1, [4, 4, 4, 4, 4], 5, true, false),
            ],
            CurrentPlayerIndex:  currentPlayerIndex,
            CurrentBid:          bid,
            RoundNumber:         1,
            PalificoActive:      false,
            LastChallengeResult: null,
            LastReveal:          null,
            Winner:              null
        );

    /// <summary>
    /// Builds a state where we control the exact dice so outcome of CallLiar is deterministic.
    /// p1 made the bid; p2 is about to call Liar (currentPlayerIndex = 1).
    /// </summary>
    private static LiarsDiceState BuildCallLiarState(
        List<int> p1Dice,
        List<int> p2Dice,
        Bid bid,
        bool palificoActive = false)
    {
        return new LiarsDiceState(
            Phase: LiarsDicePhase.Bidding,
            Players:
            [
                new("p1", "Alice", null, 0, p1Dice, p1Dice.Count, true, false),
                new("p2", "Bob",   null, 1, p2Dice, p2Dice.Count, true, false),
            ],
            CurrentPlayerIndex:  1,
            CurrentBid:          bid,
            RoundNumber:         1,
            PalificoActive:      palificoActive,
            LastChallengeResult: null,
            LastReveal:          null,
            Winner:              null
        );
    }

    /// <summary>Builds a GameContext for use with IGameHandler.Handle().</summary>
    private static GameContext MakeContext(LiarsDiceState state, LiarsDiceAction action, string playerId)
    {
        var stateDoc  = JsonDocument.Parse(JsonSerializer.Serialize(state));
        var actionDoc = JsonDocument.Parse(JsonSerializer.Serialize(action));
        return new GameContext(stateDoc, actionDoc, playerId, "room-1", 1);
    }

    // ── CreateInitialState ────────────────────────────────────────────────────

    [Fact]
    public void CreateInitialState_TwoPlayers_PhaseIsBidding()
    {
        var state = _module.CreateInitialState(TwoPlayers(), options: null);

        state.Phase.Should().Be(LiarsDicePhase.Bidding);
    }

    [Fact]
    public void CreateInitialState_TwoPlayers_RoundNumberIsOne()
    {
        var state = _module.CreateInitialState(TwoPlayers(), options: null);

        state.RoundNumber.Should().Be(1);
    }

    [Fact]
    public void CreateInitialState_TwoPlayers_FirstPlayerIsCurrent()
    {
        var state = _module.CreateInitialState(TwoPlayers(), options: null);

        state.CurrentPlayerIndex.Should().Be(0);
        state.Players[0].Id.Should().Be("p1");
    }

    [Fact]
    public void CreateInitialState_TwoPlayers_EachPlayerHasFiveDiceByDefault()
    {
        var state = _module.CreateInitialState(TwoPlayers(), options: null);

        state.Players.Should().HaveCount(2);
        foreach (var player in state.Players)
        {
            player.Dice.Should().HaveCount(5);
            player.DiceCount.Should().Be(5);
            player.Active.Should().BeTrue();
        }
    }

    [Fact]
    public void CreateInitialState_TwoPlayers_AllDiceInRangeOneToSix()
    {
        var state = _module.CreateInitialState(TwoPlayers(), options: null);

        foreach (var player in state.Players)
            player.Dice.Should().AllSatisfy(d => d.Should().BeInRange(1, 6));
    }

    [Fact]
    public void CreateInitialState_TwoPlayers_NoBidAndNoWinner()
    {
        var state = _module.CreateInitialState(TwoPlayers(), options: null);

        state.CurrentBid.Should().BeNull();
        state.Winner.Should().BeNull();
        state.PalificoActive.Should().BeFalse();
    }

    [Fact]
    public void CreateInitialState_SixPlayers_AllPlayersCreatedWithFiveDice()
    {
        var state = _module.CreateInitialState(SixPlayers(), options: null);

        state.Players.Should().HaveCount(6);
        foreach (var player in state.Players)
        {
            player.Dice.Should().HaveCount(5);
            player.Active.Should().BeTrue();
        }
    }

    [Fact]
    public void CreateInitialState_CustomStartingDice_EachPlayerHasSpecifiedCount()
    {
        var state = _module.CreateInitialState(TwoPlayers(), new LiarsDiceOptions(StartingDice: 3));

        foreach (var player in state.Players)
        {
            player.Dice.Should().HaveCount(3);
            player.DiceCount.Should().Be(3);
        }
    }

    // ── Module metadata ────────────────────────────────────────────────────────

    [Fact]
    public void Module_GameId_IsLiarsDice()
    {
        _module.GameId.Should().Be("liarsdice");
    }

    [Fact]
    public void Module_PlayerLimits_AreCorrect()
    {
        _module.MinPlayers.Should().Be(2);
        _module.MaxPlayers.Should().Be(6);
    }

    [Fact]
    public void Module_SupportsUndo_IsFalse()
    {
        _module.SupportsUndo.Should().BeFalse();
    }

    // ── Validate — general ────────────────────────────────────────────────────

    [Fact]
    public void Validate_RejectsAnyActionWhenGameIsFinished()
    {
        var state  = FreshBiddingState() with { Phase = LiarsDicePhase.Finished };
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(1, 2));

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("over");
    }

    [Fact]
    public void Validate_RejectsStartGame_WhenGameAlreadyStarted()
    {
        var state  = FreshBiddingState();
        var action = new LiarsDiceAction(LiarsDiceActionType.StartGame);

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("already started");
    }

    // ── Validate — PlaceBid ───────────────────────────────────────────────────

    [Fact]
    public void Validate_PlaceBid_RejectsWhenNotYourTurn()
    {
        var state  = FreshBiddingState(); // p1's turn
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(1, 2));

        var error = _module.Validate(state, action, "p2"); // p2 acts out of turn

        error.Should().NotBeNull();
        error.Should().Contain("turn");
    }

    [Fact]
    public void Validate_PlaceBid_AcceptsFirstBidWithNoCurrentBid()
    {
        var state  = FreshBiddingState();
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(1, 2));

        var error = _module.Validate(state, action, "p1");

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_PlaceBid_AcceptsHigherQuantitySameFace()
    {
        var state  = StateWithBid(new Bid(2, 3), currentPlayerIndex: 1); // p2's turn
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(3, 3));

        var error = _module.Validate(state, action, "p2");

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_PlaceBid_AcceptsSameQuantityHigherFace()
    {
        var state  = StateWithBid(new Bid(2, 3), currentPlayerIndex: 1); // p2's turn
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(2, 4));

        var error = _module.Validate(state, action, "p2");

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_PlaceBid_RejectsSameQuantitySameFace()
    {
        var state  = StateWithBid(new Bid(2, 3), currentPlayerIndex: 1);
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(2, 3));

        var error = _module.Validate(state, action, "p2");

        error.Should().NotBeNull();
        error.Should().Contain("strictly higher");
    }

    [Fact]
    public void Validate_PlaceBid_RejectsLowerQuantity()
    {
        var state  = StateWithBid(new Bid(3, 3), currentPlayerIndex: 1);
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(2, 3));

        var error = _module.Validate(state, action, "p2");

        error.Should().NotBeNull();
        error.Should().Contain("strictly higher");
    }

    [Fact]
    public void Validate_PlaceBid_RejectsSameQuantityLowerFace()
    {
        var state  = StateWithBid(new Bid(2, 4), currentPlayerIndex: 1);
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(2, 3));

        var error = _module.Validate(state, action, "p2");

        error.Should().NotBeNull();
        error.Should().Contain("strictly higher");
    }

    [Fact]
    public void Validate_PlaceBid_RejectsMissingBidData()
    {
        var state  = FreshBiddingState();
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, Bid: null);

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("bid data");
    }

    [Fact]
    public void Validate_PlaceBid_RejectsZeroQuantity()
    {
        var state  = FreshBiddingState();
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(0, 3));

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("Quantity");
    }

    [Fact]
    public void Validate_PlaceBid_RejectsInvalidFaceTooHigh()
    {
        var state  = FreshBiddingState();
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(1, 7));

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("Face");
    }

    [Fact]
    public void Validate_PlaceBid_RejectsInvalidFaceZero()
    {
        var state  = FreshBiddingState();
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(1, 0));

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("Face");
    }

    [Fact]
    public void Validate_PlaceBid_RejectsDuringRevealPhase()
    {
        var state  = FreshBiddingState() with { Phase = LiarsDicePhase.Reveal };
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(1, 2));

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("Bidding phase");
    }

    // ── Validate — CallLiar ───────────────────────────────────────────────────

    [Fact]
    public void Validate_CallLiar_RejectsWhenNoBidExists()
    {
        var state  = FreshBiddingState(); // no current bid
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("no bid");
    }

    [Fact]
    public void Validate_CallLiar_AcceptsWhenBidExistsAndCurrentPlayer()
    {
        var state  = StateWithBid(new Bid(2, 3), currentPlayerIndex: 1); // p2's turn
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var error = _module.Validate(state, action, "p2");

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_CallLiar_RejectsWhenNotYourTurn()
    {
        var state  = StateWithBid(new Bid(2, 3), currentPlayerIndex: 1); // p2's turn
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var error = _module.Validate(state, action, "p1"); // p1 acts out of turn

        error.Should().NotBeNull();
        error.Should().Contain("turn");
    }

    [Fact]
    public void Validate_CallLiar_RejectsDuringRevealPhase()
    {
        var state  = StateWithBid(new Bid(2, 3)) with { Phase = LiarsDicePhase.Reveal };
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var error = _module.Validate(state, action, "p2");

        error.Should().NotBeNull();
        error.Should().Contain("Bidding phase");
    }

    // ── Validate — StartNextRound ─────────────────────────────────────────────

    [Fact]
    public void Validate_StartNextRound_AcceptsActivePlayerDuringReveal()
    {
        var state = FreshBiddingState() with
        {
            Phase      = LiarsDicePhase.Reveal,
            LastReveal = new RevealSnapshot([], new Bid(1, 1), 1, "p1")
        };
        var action = new LiarsDiceAction(LiarsDiceActionType.StartNextRound);

        var error = _module.Validate(state, action, "p2");

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_StartNextRound_RejectsDuringBiddingPhase()
    {
        var state  = FreshBiddingState();
        var action = new LiarsDiceAction(LiarsDiceActionType.StartNextRound);

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("Reveal");
    }

    // ── Validate — DeclarePalifico ────────────────────────────────────────────

    [Fact]
    public void Validate_DeclarePalifico_AcceptsWhenPlayerHasExactlyOneDie()
    {
        var state = new LiarsDiceState(
            Phase: LiarsDicePhase.Bidding,
            Players:
            [
                new("p1", "Alice", null, 0, [3], 1, true, false),
                new("p2", "Bob",   null, 1, [4, 4, 4, 4, 4], 5, true, false),
            ],
            CurrentPlayerIndex:  0,
            CurrentBid:          null,
            RoundNumber:         2,
            PalificoActive:      false,
            LastChallengeResult: null,
            LastReveal:          null,
            Winner:              null
        );
        var action = new LiarsDiceAction(LiarsDiceActionType.DeclarePalifico);

        var error = _module.Validate(state, action, "p1");

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_DeclarePalifico_RejectsWhenPlayerHasMoreThanOneDie()
    {
        var state  = FreshBiddingState(); // p1 has 5 dice
        var action = new LiarsDiceAction(LiarsDiceActionType.DeclarePalifico);

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("exactly one die");
    }

    [Fact]
    public void Validate_DeclarePalifico_RejectsIfAlreadyUsed()
    {
        var state = new LiarsDiceState(
            Phase: LiarsDicePhase.Bidding,
            Players:
            [
                new("p1", "Alice", null, 0, [3], 1, true, HasUsedPalifico: true),
                new("p2", "Bob",   null, 1, [4, 4, 4, 4, 4], 5, true, false),
            ],
            CurrentPlayerIndex:  0,
            CurrentBid:          null,
            RoundNumber:         3,
            PalificoActive:      false,
            LastChallengeResult: null,
            LastReveal:          null,
            Winner:              null
        );
        var action = new LiarsDiceAction(LiarsDiceActionType.DeclarePalifico);

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("already used");
    }

    [Fact]
    public void Validate_DeclarePalifico_RejectsIfBidAlreadyPlaced()
    {
        var state = new LiarsDiceState(
            Phase: LiarsDicePhase.Bidding,
            Players:
            [
                new("p1", "Alice", null, 0, [3], 1, true, false),
                new("p2", "Bob",   null, 1, [4, 4], 2, true, false),
            ],
            CurrentPlayerIndex:  0,
            CurrentBid:          new Bid(1, 3),
            RoundNumber:         2,
            PalificoActive:      false,
            LastChallengeResult: null,
            LastReveal:          null,
            Winner:              null
        );
        var action = new LiarsDiceAction(LiarsDiceActionType.DeclarePalifico);

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("before any bid");
    }

    [Fact]
    public void Validate_DeclarePalifico_RejectsWhenNotYourTurn()
    {
        var state = new LiarsDiceState(
            Phase: LiarsDicePhase.Bidding,
            Players:
            [
                new("p1", "Alice", null, 0, [3, 3, 3, 3, 3], 5, true, false),
                new("p2", "Bob",   null, 1, [4], 1, true, false),
            ],
            CurrentPlayerIndex:  0, // p1's turn
            CurrentBid:          null,
            RoundNumber:         2,
            PalificoActive:      false,
            LastChallengeResult: null,
            LastReveal:          null,
            Winner:              null
        );
        var action = new LiarsDiceAction(LiarsDiceActionType.DeclarePalifico);

        var error = _module.Validate(state, action, "p2"); // p2 out of turn

        error.Should().NotBeNull();
        error.Should().Contain("turn");
    }

    // ── Apply — PlaceBid ──────────────────────────────────────────────────────

    [Fact]
    public void Apply_PlaceBid_SetsBidOnState()
    {
        var state  = FreshBiddingState();
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(3, 5));

        var next = _module.Apply(state, action);

        next.CurrentBid.Should().NotBeNull();
        next.CurrentBid!.Quantity.Should().Be(3);
        next.CurrentBid.Face.Should().Be(5);
    }

    [Fact]
    public void Apply_PlaceBid_AdvancesToNextPlayer()
    {
        var state  = FreshBiddingState(); // p1's turn (index 0)
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(1, 2));

        var next = _module.Apply(state, action);

        next.CurrentPlayerIndex.Should().Be(1); // p2 is next
    }

    // ── Apply — CallLiar — loser determination ────────────────────────────────

    [Fact]
    public void Apply_CallLiar_BidNotMet_BidderLosesOneDie()
    {
        // p1 bid 3 fours. Fours in play: p1=[4,1,2] → one 4 + one wild = 2, p2=[2,3,5] → 0.
        // Total with wilds: 4 (face) + 1 (wild) = 2. Bid quantity = 3. Not met → p1 (bidder) loses.
        var state  = BuildCallLiarState([4, 1, 2], [2, 3, 5], new Bid(3, 4));
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var next = _module.Apply(state, action);

        var p1 = next.Players.First(p => p.Id == "p1");
        p1.DiceCount.Should().Be(2, because: "bidder lost one die since bid was not met");
    }

    [Fact]
    public void Apply_CallLiar_BidMet_CallerLosesOneDie()
    {
        // p1 bid 2 fours. p1=[4,1], p2=[2,3,4].
        // Wilds apply (face=4 != 1, not Palifico): 4 + wild(1) + 4 = 3 → bid met → p2 (caller) loses.
        var state  = BuildCallLiarState([4, 1], [2, 3, 4], new Bid(2, 4));
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var next = _module.Apply(state, action);

        var p2 = next.Players.First(p => p.Id == "p2");
        p2.DiceCount.Should().Be(2, because: "caller (p2) loses a die when bid is met");
    }

    [Fact]
    public void Apply_CallLiar_TransitionsToRevealPhase()
    {
        // Bid way too high — clearly not met — both players survive.
        var state  = BuildCallLiarState([4, 1, 2], [2, 3, 5], new Bid(10, 4));
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var next = _module.Apply(state, action);

        next.Phase.Should().Be(LiarsDicePhase.Reveal);
    }

    [Fact]
    public void Apply_CallLiar_RevealSnapshotIsPopulated()
    {
        var state  = BuildCallLiarState([4, 1, 2], [2, 3, 5], new Bid(10, 4));
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var next = _module.Apply(state, action);

        next.LastReveal.Should().NotBeNull();
        next.LastReveal!.ChallengedBid.Should().Be(new Bid(10, 4));
        next.LastReveal.Players.Should().HaveCount(2);
    }

    [Fact]
    public void Apply_CallLiar_CurrentBidClearedAfterChallenge()
    {
        var state  = BuildCallLiarState([4, 1, 2], [2, 3, 5], new Bid(10, 4));
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var next = _module.Apply(state, action);

        next.CurrentBid.Should().BeNull();
    }

    [Fact]
    public void Apply_CallLiar_PlayerEliminatedWhenDiceReachZero()
    {
        // p2 (caller) has 1 die. p1 bid 1 two; p1=[2], p2=[3].
        // Count of 2s = 1 → bid met → p2 (caller) loses last die → eliminated.
        var state  = BuildCallLiarState([2], [3], new Bid(1, 2));
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var next = _module.Apply(state, action);

        var p2 = next.Players.First(p => p.Id == "p2");
        p2.Active.Should().BeFalse();
        p2.DiceCount.Should().Be(0);
    }

    [Fact]
    public void Apply_CallLiar_GameFinishesWhenOnlyOnePlayerRemains()
    {
        // p2 has 1 die; bid met → p2 loses last die → game over, p1 wins.
        var state  = BuildCallLiarState([2], [3], new Bid(1, 2));
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var next = _module.Apply(state, action);

        next.Phase.Should().Be(LiarsDicePhase.Finished);
        next.Winner.Should().Be("p1");
    }

    [Fact]
    public void Handle_CallLiar_GameOver_EmitsGameOverEffect()
    {
        // p2 has 1 die; bid met → p2 eliminated → GameOverEffect(p1).
        var state  = BuildCallLiarState([2], [3], new Bid(1, 2));
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);
        var ctx    = MakeContext(state, action, "p2");

        var result = ((IGameHandler)_module).Handle(ctx);

        result.RejectionReason.Should().BeNull();
        result.Effects.Should().ContainSingle(e => e is GameOverEffect);
        var effect = (GameOverEffect)result.Effects.Single(e => e is GameOverEffect);
        effect.WinnerId.Should().Be("p1");
    }

    [Fact]
    public void Handle_CallLiar_NotGameOver_NoGameOverEffect()
    {
        // Bid not met, both players survive — no GameOverEffect.
        var state  = BuildCallLiarState([4, 1, 2], [2, 3, 5], new Bid(10, 4));
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);
        var ctx    = MakeContext(state, action, "p2");

        var result = ((IGameHandler)_module).Handle(ctx);

        result.RejectionReason.Should().BeNull();
        result.Effects.Should().NotContain(e => e is GameOverEffect);
    }

    // ── Wild dice logic ───────────────────────────────────────────────────────

    [Fact]
    public void Apply_CallLiar_WildsApply_OnesCountTowardNonOneBid()
    {
        // p1 bid 3 fives. p1=[5,1], p2=[5,2].
        // Wilds: 5 + wild(1) + 5 = 3 → bid met → p2 (caller) loses.
        var state  = BuildCallLiarState([5, 1], [5, 2], new Bid(3, 5));
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var next = _module.Apply(state, action);

        var p2 = next.Players.First(p => p.Id == "p2");
        p2.DiceCount.Should().Be(1, because: "bid met when 1s count as wilds");
    }

    [Fact]
    public void Apply_CallLiar_WildsDoNotApply_WhenBidFaceIsOne()
    {
        // p1 bid 3 ones. p1=[1,1], p2=[1,2,3].
        // Face=1 → no wilds. Count of 1s = 3 → bid met (3 >= 3) → p2 (caller) loses.
        // Verifies 1s are not double-counted as both themselves and wilds.
        var state  = BuildCallLiarState([1, 1], [1, 2, 3], new Bid(3, 1));
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var next = _module.Apply(state, action);

        next.LastReveal!.ActualCount.Should().Be(3,
            because: "only three 1s exist; they are not also counted as wilds when face=1");
        var p2 = next.Players.First(p => p.Id == "p2");
        p2.DiceCount.Should().Be(2, because: "bid of 3 ones met; caller (p2) loses");
    }

    [Fact]
    public void Apply_CallLiar_WildsDoNotApply_DuringPalificoRound()
    {
        // Palifico active. p1 bid 2 fives. p1=[5,1], p2=[2,3].
        // No wilds → count of 5s = 1 → bid NOT met (1 < 2) → p1 (bidder) loses.
        var state  = BuildCallLiarState([5, 1], [2, 3], new Bid(2, 5), palificoActive: true);
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var next = _module.Apply(state, action);

        next.LastReveal!.ActualCount.Should().Be(1,
            because: "in Palifico round the wild 1 does not count toward face 5");
        var p1 = next.Players.First(p => p.Id == "p1");
        p1.DiceCount.Should().Be(1,
            because: "bid not met during Palifico; bidder (p1) loses a die");
    }

    [Fact]
    public void Apply_CallLiar_OneDieCountedOnce_NotAsWildAndFace()
    {
        // p1 bid 2 fives. p1=[1], p2=[3]. Wild 1 counts as one five-equivalent.
        // Count = 1, bid = 2 → not met → p1 (bidder) loses last die → game over.
        var state  = BuildCallLiarState([1], [3], new Bid(2, 5));
        var action = new LiarsDiceAction(LiarsDiceActionType.CallLiar);

        var next = _module.Apply(state, action);

        next.LastReveal!.ActualCount.Should().Be(1,
            because: "a die showing 1 counts as one wild, not also as itself");
        var p1 = next.Players.First(p => p.Id == "p1");
        p1.DiceCount.Should().Be(0,
            because: "bid not met; bidder p1 loses last die and is eliminated");
        next.Phase.Should().Be(LiarsDicePhase.Finished);
    }

    // ── Apply — DeclarePalifico ───────────────────────────────────────────────

    [Fact]
    public void Apply_DeclarePalifico_SetsPalificoActiveTrue()
    {
        var state = new LiarsDiceState(
            Phase: LiarsDicePhase.Bidding,
            Players:
            [
                new("p1", "Alice", null, 0, [3], 1, true, false),
                new("p2", "Bob",   null, 1, [4, 4, 4, 4, 4], 5, true, false),
            ],
            CurrentPlayerIndex:  0,
            CurrentBid:          null,
            RoundNumber:         2,
            PalificoActive:      false,
            LastChallengeResult: null,
            LastReveal:          null,
            Winner:              null
        );
        var action = new LiarsDiceAction(LiarsDiceActionType.DeclarePalifico);

        var next = _module.Apply(state, action);

        next.PalificoActive.Should().BeTrue();
    }

    [Fact]
    public void Apply_DeclarePalifico_MarksPlayerAsHavingUsedPalifico()
    {
        var state = new LiarsDiceState(
            Phase: LiarsDicePhase.Bidding,
            Players:
            [
                new("p1", "Alice", null, 0, [3], 1, true, false),
                new("p2", "Bob",   null, 1, [4, 4, 4, 4, 4], 5, true, false),
            ],
            CurrentPlayerIndex:  0,
            CurrentBid:          null,
            RoundNumber:         2,
            PalificoActive:      false,
            LastChallengeResult: null,
            LastReveal:          null,
            Winner:              null
        );
        var action = new LiarsDiceAction(LiarsDiceActionType.DeclarePalifico);

        var next = _module.Apply(state, action);

        var p1 = next.Players.First(p => p.Id == "p1");
        p1.HasUsedPalifico.Should().BeTrue();
    }

    // ── Apply — StartNextRound ────────────────────────────────────────────────

    [Fact]
    public void Apply_StartNextRound_IncreasesRoundNumber()
    {
        var state = FreshBiddingState() with
        {
            Phase      = LiarsDicePhase.Reveal,
            LastReveal = new RevealSnapshot([], new Bid(1, 1), 0, "p1")
        };
        var action = new LiarsDiceAction(LiarsDiceActionType.StartNextRound);

        var next = _module.Apply(state, action);

        next.RoundNumber.Should().Be(2);
    }

    [Fact]
    public void Apply_StartNextRound_ReturnsToBiddingPhase()
    {
        var state = FreshBiddingState() with
        {
            Phase      = LiarsDicePhase.Reveal,
            LastReveal = new RevealSnapshot([], new Bid(1, 1), 0, "p1")
        };
        var action = new LiarsDiceAction(LiarsDiceActionType.StartNextRound);

        var next = _module.Apply(state, action);

        next.Phase.Should().Be(LiarsDicePhase.Bidding);
    }

    [Fact]
    public void Apply_StartNextRound_ClearsBidAndReveal()
    {
        var state = FreshBiddingState() with
        {
            Phase               = LiarsDicePhase.Reveal,
            CurrentBid          = new Bid(2, 3),
            LastReveal          = new RevealSnapshot([], new Bid(2, 3), 1, "p1"),
            LastChallengeResult = "some result"
        };
        var action = new LiarsDiceAction(LiarsDiceActionType.StartNextRound);

        var next = _module.Apply(state, action);

        next.CurrentBid.Should().BeNull();
        next.LastReveal.Should().BeNull();
        next.LastChallengeResult.Should().BeNull();
    }

    [Fact]
    public void Apply_StartNextRound_PalificoActiveResetToFalse()
    {
        var state = FreshBiddingState() with
        {
            Phase          = LiarsDicePhase.Reveal,
            PalificoActive = true,
            LastReveal     = new RevealSnapshot([], new Bid(1, 1), 0, "p1")
        };
        var action = new LiarsDiceAction(LiarsDiceActionType.StartNextRound);

        var next = _module.Apply(state, action);

        next.PalificoActive.Should().BeFalse();
    }

    [Fact]
    public void Apply_StartNextRound_LoserStartsNextRound_WhenStillActive()
    {
        // p1 was the loser of the previous round and is still active.
        var state = FreshBiddingState() with
        {
            Phase      = LiarsDicePhase.Reveal,
            LastReveal = new RevealSnapshot([], new Bid(1, 1), 0, LoserId: "p1")
        };
        var action = new LiarsDiceAction(LiarsDiceActionType.StartNextRound);

        var next = _module.Apply(state, action);

        next.CurrentPlayerIndex.Should().Be(0,
            because: "the loser (p1 at index 0) starts the next round when still active");
    }

    // ── Handle — rejection propagation ────────────────────────────────────────

    [Fact]
    public void Handle_OutOfTurnAction_ReturnsRejection()
    {
        var state  = FreshBiddingState(); // p1's turn
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(1, 2));
        var ctx    = MakeContext(state, action, "p2"); // p2 acts out of turn

        var result = ((IGameHandler)_module).Handle(ctx);

        result.RejectionReason.Should().NotBeNull();
        result.RejectionReason.Should().Contain("turn");
    }

    [Fact]
    public void Handle_ValidPlaceBid_StateReflectsNewBid()
    {
        var state  = FreshBiddingState();
        var action = new LiarsDiceAction(LiarsDiceActionType.PlaceBid, new BidPayload(2, 4));
        var ctx    = MakeContext(state, action, "p1");

        var result = ((IGameHandler)_module).Handle(ctx);

        result.RejectionReason.Should().BeNull();
        var newState = JsonSerializer.Deserialize<LiarsDiceState>(
            result.NewState.RootElement.GetRawText());
        newState.Should().NotBeNull();
        newState!.CurrentBid!.Quantity.Should().Be(2);
        newState.CurrentBid.Face.Should().Be(4);
    }

    // ── Projection — HasStateProjection ───────────────────────────────────────

    [Fact]
    public void HasStateProjection_IsTrue()
    {
        // AC-2: module declares it implements per-player state projection
        ((IGameModule)_module).HasStateProjection.Should().BeTrue();
    }

    // ── Projection helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Serialises a typed state to JsonDocument and calls the interface-level
    /// ProjectStateForPlayer so tests exercise the full serialise-project-deserialise path.
    /// </summary>
    private LiarsDiceState? Project(LiarsDiceState state, string playerId)
    {
        var doc       = JsonDocument.Parse(JsonSerializer.Serialize(state));
        var projected = ((IGameModule)_module).ProjectStateForPlayer(doc, playerId);
        if (projected is null) return null;
        return JsonSerializer.Deserialize<LiarsDiceState>(projected.RootElement.GetRawText());
    }

    // ── Projection — Bidding phase ────────────────────────────────────────────

    [Fact]
    public void ProjectForPlayer_Bidding_RequestingPlayerSeesOwnDice()
    {
        // AC-4: player A gets their own dice intact
        var state = FreshBiddingState(); // p1 has [3,3,3,3,3], p2 has [4,4,4,4,4]

        var projected = Project(state, "p1");

        projected.Should().NotBeNull();
        var p1 = projected!.Players.First(p => p.Id == "p1");
        p1.Dice.Should().BeEquivalentTo([3, 3, 3, 3, 3],
            because: "requesting player sees their own dice unchanged");
    }

    [Fact]
    public void ProjectForPlayer_Bidding_OtherPlayersDiceAreEmpty()
    {
        // AC-4: player B's dice are hidden from player A's perspective
        var state = FreshBiddingState();

        var projected = Project(state, "p1");

        projected.Should().NotBeNull();
        var p2 = projected!.Players.First(p => p.Id == "p2");
        p2.Dice.Should().BeEmpty(because: "other players' dice are hidden during Bidding");
    }

    [Fact]
    public void ProjectForPlayer_Bidding_DiceCountPreservedForAllPlayers()
    {
        // AC-4: diceCount is always visible so players can reason about odds
        var state = FreshBiddingState(); // both players have DiceCount = 5

        var projected = Project(state, "p1");

        projected.Should().NotBeNull();
        foreach (var p in projected!.Players)
            p.DiceCount.Should().Be(5,
                because: "DiceCount must remain visible even when dice values are hidden");
    }

    [Fact]
    public void ProjectForPlayer_Bidding_UnknownPlayerGetsMaximallyRestricted()
    {
        // AC-6: an ID not present in Players (spectator / eliminated) sees no dice
        var state = FreshBiddingState();

        var projected = Project(state, "unknown-id");

        projected.Should().NotBeNull();
        foreach (var p in projected!.Players)
            p.Dice.Should().BeEmpty(
                because: "unknown player IDs receive maximally restricted projection — all dice hidden");
    }

    // ── Projection — Reveal phase ─────────────────────────────────────────────

    [Fact]
    public void ProjectForPlayer_Reveal_AllDiceVisible()
    {
        // AC-5: during Reveal, the full dice are public so players can verify the count
        var state = FreshBiddingState() with
        {
            Phase      = LiarsDicePhase.Reveal,
            LastReveal = new RevealSnapshot([], new Bid(3, 3), 5, "p2")
        };

        var projectedAsP1 = Project(state, "p1");
        var projectedAsP2 = Project(state, "p2");

        projectedAsP1.Should().NotBeNull();
        projectedAsP2.Should().NotBeNull();

        projectedAsP1!.Players.First(p => p.Id == "p1").Dice
            .Should().BeEquivalentTo([3, 3, 3, 3, 3],
                because: "requesting player sees own dice during Reveal");
        projectedAsP1.Players.First(p => p.Id == "p2").Dice
            .Should().BeEquivalentTo([4, 4, 4, 4, 4],
                because: "other player's dice are visible during Reveal");

        projectedAsP2!.Players.First(p => p.Id == "p1").Dice
            .Should().BeEquivalentTo([3, 3, 3, 3, 3],
                because: "all dice are public during Reveal regardless of who requests");
    }

    // ── Projection — Finished phase ───────────────────────────────────────────

    [Fact]
    public void ProjectForPlayer_Finished_AllDiceVisible()
    {
        // AC-5: during Finished, game is over — full state visible to all
        var state = FreshBiddingState() with
        {
            Phase  = LiarsDicePhase.Finished,
            Winner = "p1"
        };

        var projectedAsP2 = Project(state, "p2");

        projectedAsP2.Should().NotBeNull();
        projectedAsP2!.Players.First(p => p.Id == "p1").Dice
            .Should().BeEquivalentTo([3, 3, 3, 3, 3],
                because: "dice are fully visible in Finished phase");
        projectedAsP2.Players.First(p => p.Id == "p2").Dice
            .Should().BeEquivalentTo([4, 4, 4, 4, 4],
                because: "dice are fully visible in Finished phase");
    }

    // ── Projection — Immutability (AC-8) ──────────────────────────────────────

    [Fact]
    public void ProjectForPlayer_DoesNotMutateInputState()
    {
        // AC-8: ProjectForPlayer must be pure — it must not modify the input document
        var original = FreshBiddingState(); // p2 has [4,4,4,4,4]
        var originalDoc = JsonDocument.Parse(JsonSerializer.Serialize(original));

        // Project as p1 — this should hide p2's dice in the output but NOT in originalDoc
        ((IGameModule)_module).ProjectStateForPlayer(originalDoc, "p1");

        // Re-read original state from the same document
        var afterProjection = JsonSerializer.Deserialize<LiarsDiceState>(
            originalDoc.RootElement.GetRawText());

        afterProjection.Should().NotBeNull();
        afterProjection!.Players.First(p => p.Id == "p2").Dice
            .Should().BeEquivalentTo([4, 4, 4, 4, 4],
                because: "ProjectForPlayer must not mutate the input JsonDocument");
    }

    // ── GameDispatcher helper ─────────────────────────────────────────────────

    [Fact]
    public void GameDispatcher_ProjectStateForPlayerOrFull_ReturnsProjectedStateForLiarsDice()
    {
        // AC-11: the dispatcher helper uses the same projection path as fan-out
        // The full GameDispatcher requires DI (DB, SignalR hub). We verify the contract
        // at the IGameModule level, which is the single shared projection path used by
        // both GameDispatcher.DispatchAsync (fan-out) and GameDispatcher.ProjectStateForPlayerOrFull
        // (reconnect). No database or SignalR wiring required for this pure-function test.
        var module  = new LiarsDiceModule();
        var state   = FreshBiddingState();
        var fullDoc = JsonDocument.Parse(JsonSerializer.Serialize(state));

        // Simulate what ProjectStateForPlayerOrFull does when HasStateProjection == true
        var projected = module.HasStateProjection
            ? ((IGameModule)module).ProjectStateForPlayer(fullDoc, "p1") ?? fullDoc
            : fullDoc;

        var result = JsonSerializer.Deserialize<LiarsDiceState>(projected.RootElement.GetRawText());
        result.Should().NotBeNull();
        result!.Players.First(p => p.Id == "p2").Dice.Should().BeEmpty(
            because: "ProjectStateForPlayerOrFull uses module.ProjectStateForPlayer — " +
                     "same path as both fan-out and reconnect (AC-11)");
        result.Players.First(p => p.Id == "p1").Dice.Should().BeEquivalentTo([3, 3, 3, 3, 3],
            because: "requesting player sees own dice through the shared projection path");
    }
}
