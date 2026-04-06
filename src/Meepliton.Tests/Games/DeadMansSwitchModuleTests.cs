using System.Text.Json;
using FluentAssertions;
using Meepliton.Contracts;
using Meepliton.Games.DeadMansSwitch;
using Meepliton.Games.DeadMansSwitch.Models;
using Xunit;

namespace Meepliton.Tests.Games;

public class DeadMansSwitchModuleTests
{
    private readonly DeadMansSwitchModule _module = new();

    // ── Player-list helpers ───────────────────────────────────────────────────

    private static IReadOnlyList<PlayerInfo> Players(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new PlayerInfo($"p{i + 1}", $"Player{i + 1}", null, i))
            .ToList();

    private static IReadOnlyList<PlayerInfo> ThreePlayers() => Players(3);
    private static IReadOnlyList<PlayerInfo> SixPlayers()   => Players(6);

    // ── Action helpers ────────────────────────────────────────────────────────

    private static DeadMansSwitchAction PlaceDisc(DiscType t = DiscType.Rose) => new("PlaceDisc", DiscType: t);
    private static DeadMansSwitchAction StartBid(int target)             => new("StartBid",  TargetCount: target);
    private static DeadMansSwitchAction RaiseBid(int newBid)             => new("RaiseBid",  NewBid: newBid);
    private static DeadMansSwitchAction Pass()                           => new("Pass");
    private static DeadMansSwitchAction FlipDisc(string targetId)        => new("FlipDisc",  TargetPlayerId: targetId);
    private static DeadMansSwitchAction DiscardDisc(DiscType t)          => new("DiscardDisc", DiscType: t);
    private static DeadMansSwitchAction StartNextRound()                 => new("StartNextRound");

    // ── State builder helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Build a minimal DevicePlayer.  stack items are placed in insertion order;
    /// the last item is "the top" (matching ApplyFlipDisc's reverse iteration).
    /// </summary>
    private static DevicePlayer MakePlayer(
        string id,
        int seatIndex      = 0,
        int rosesOwned     = 3,
        bool skullOwned    = true,
        int stackCount     = 0,
        List<DiscSlot>? stack = null,
        int pointsWon      = 0,
        bool active        = true,
        bool passed        = false) =>
        new(id, $"Player {id}", null, seatIndex,
            Stack:      stack ?? [],
            StackCount: stackCount,
            RosesOwned: rosesOwned,
            SkullOwned: skullOwned,
            PointsWon:  pointsWon,
            Active:     active,
            Passed:     passed);

    private static DeadMansSwitchState MakeState(
        List<DevicePlayer>      players,
        DeadMansSwitchPhase     phase              = DeadMansSwitchPhase.Placing,
        int                     currentPlayerIndex = 0,
        int                     currentBid         = 0,
        int                     totalDiscsOnTable  = 0,
        string?                 challengerId       = null,
        int                     nextRoundFirstPlayerIndex = 0,
        FlipLog?                lastFlip           = null,
        string?                 winner             = null,
        int                     roundNumber        = 1) =>
        new(phase, players, currentPlayerIndex, currentBid, totalDiscsOnTable,
            challengerId, nextRoundFirstPlayerIndex, lastFlip, winner, roundNumber);

    // ── Module metadata ───────────────────────────────────────────────────────

    [Fact]
    public void Module_GameId_IsDeadMansSwitch() =>
        _module.GameId.Should().Be("deadmansswitch");

    [Fact]
    public void Module_PlayerLimits_Are3To6()
    {
        _module.MinPlayers.Should().Be(3);
        _module.MaxPlayers.Should().Be(6);
    }

    [Fact]
    public void Module_HasStateProjection_IsTrue() =>
        ((IGameModule)_module).HasStateProjection.Should().BeTrue();

    // ── CreateInitialState ───────────────────────────────────────────────────

    [Fact]
    public void CreateInitialState_ThreePlayers_ValidBaseState()
    {
        var state = _module.CreateInitialState(ThreePlayers());

        state.Phase.Should().Be(DeadMansSwitchPhase.Placing);
        state.CurrentPlayerIndex.Should().Be(0);
        state.TotalDiscsOnTable.Should().Be(0);
        state.CurrentBid.Should().Be(0);
        state.Winner.Should().BeNull();
        state.RoundNumber.Should().Be(1);
    }

    [Fact]
    public void CreateInitialState_EachPlayerGetsCorrectStartingDiscs()
    {
        var state = _module.CreateInitialState(ThreePlayers());

        foreach (var p in state.Players)
        {
            p.RosesOwned.Should().Be(3);
            p.SkullOwned.Should().BeTrue();
            p.Stack.Should().BeEmpty();
            p.StackCount.Should().Be(0);
            p.PointsWon.Should().Be(0);
            p.Active.Should().BeTrue();
        }
    }

    [Fact]
    public void CreateInitialState_SixPlayers_ValidBaseState()
    {
        var state = _module.CreateInitialState(SixPlayers());

        state.Players.Should().HaveCount(6);
        state.Players.Should().AllSatisfy(p => p.Active.Should().BeTrue());
    }

    [Fact]
    public void CreateInitialState_TwoPlayers_Throws()
    {
        var act = () => _module.CreateInitialState(Players(2));
        act.Should().Throw<InvalidOperationException>().WithMessage("*3*6*");
    }

    [Fact]
    public void CreateInitialState_SevenPlayers_Throws()
    {
        var act = () => _module.CreateInitialState(Players(7));
        act.Should().Throw<InvalidOperationException>().WithMessage("*3*6*");
    }

    // ── Validate — Placing phase ──────────────────────────────────────────────

    [Fact]
    public void Validate_PlaceDisc_OnYourTurn_IsValid()
    {
        var state = _module.CreateInitialState(ThreePlayers());
        var error = _module.Validate(state, PlaceDisc(), "p1");
        error.Should().BeNull();
    }

    [Fact]
    public void Validate_PlaceDisc_NotYourTurn_IsRejected()
    {
        var state = _module.CreateInitialState(ThreePlayers());
        var error = _module.Validate(state, PlaceDisc(), "p2");
        error.Should().NotBeNull();
        error.Should().Contain("turn");
    }

    [Fact]
    public void Validate_StartBid_WithZeroStackCount_IsRejected()
    {
        var state = _module.CreateInitialState(ThreePlayers());
        // p1 has stackCount=0 at start
        var error = _module.Validate(state, StartBid(1), "p1");
        error.Should().NotBeNull();
        error.Should().Contain("arm");
    }

    [Fact]
    public void Validate_StartBid_WithOneDisc_TargetZero_IsRejected()
    {
        // p1 has placed 1 disc; totalDiscsOnTable=1; target=0 is invalid
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, rosesOwned: 3, skullOwned: true, stackCount: 1,
                stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p2", 1),
            MakePlayer("p3", 2)
        };
        var state = MakeState(players, totalDiscsOnTable: 1);
        var error = _module.Validate(state, StartBid(0), "p1");
        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_StartBid_WithOneDisc_TargetExceedsTotal_IsRejected()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1,
                stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p2", 1),
            MakePlayer("p3", 2)
        };
        var state = MakeState(players, totalDiscsOnTable: 1);
        var error = _module.Validate(state, StartBid(2), "p1");
        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_StartBid_ValidTarget_IsAccepted()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1,
                stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p2", 1),
            MakePlayer("p3", 2)
        };
        var state = MakeState(players, totalDiscsOnTable: 1);
        var error = _module.Validate(state, StartBid(1), "p1");
        error.Should().BeNull();
    }

    // ── Validate — Bidding phase ──────────────────────────────────────────────

    [Fact]
    public void Validate_RaiseBid_AboveCurrentBid_IsValid()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1),
            MakePlayer("p2", 1, stackCount: 1),
            MakePlayer("p3", 2, stackCount: 1)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Bidding,
            currentPlayerIndex: 1,
            currentBid: 2,
            totalDiscsOnTable: 3,
            challengerId: "p1");
        var error = _module.Validate(state, RaiseBid(3), "p2");
        error.Should().BeNull();
    }

    [Fact]
    public void Validate_RaiseBid_SameAsCurrentBid_IsRejected()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Bidding,
            currentPlayerIndex: 1,
            currentBid: 2,
            totalDiscsOnTable: 3,
            challengerId: "p1");
        var error = _module.Validate(state, RaiseBid(2), "p2");
        error.Should().NotBeNull();
        error.Should().Contain("higher");
    }

    [Fact]
    public void Validate_RaiseBid_ExceedsTotalDiscs_IsRejected()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Bidding,
            currentPlayerIndex: 1,
            currentBid: 2,
            totalDiscsOnTable: 3,
            challengerId: "p1");
        var error = _module.Validate(state, RaiseBid(4), "p2");
        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_Pass_OnYourTurn_InBiddingPhase_IsValid()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Bidding,
            currentPlayerIndex: 1,
            currentBid: 1,
            totalDiscsOnTable: 3,
            challengerId: "p1");
        var error = _module.Validate(state, Pass(), "p2");
        error.Should().BeNull();
    }

    [Fact]
    public void Validate_Pass_NotYourTurn_IsRejected()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Bidding,
            currentPlayerIndex: 1,
            currentBid: 1,
            totalDiscsOnTable: 3,
            challengerId: "p1");
        var error = _module.Validate(state, Pass(), "p3");
        error.Should().NotBeNull();
    }

    // ── Validate — Revealing phase ────────────────────────────────────────────

    [Fact]
    public void Validate_FlipDisc_NonChallenger_IsRejected()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p2", 1, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p3", 2, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)])
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Revealing,
            currentPlayerIndex: 0,
            currentBid: 2,
            totalDiscsOnTable: 3,
            challengerId: "p1");
        var error = _module.Validate(state, FlipDisc("p1"), "p2");
        error.Should().NotBeNull();
        error.Should().Contain("Challenger");
    }

    [Fact]
    public void Validate_FlipDisc_OwnStack_WhenOwn_Unflipped_IsValid()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p2", 1, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p3", 2, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)])
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Revealing,
            currentPlayerIndex: 0,
            currentBid: 2,
            totalDiscsOnTable: 3,
            challengerId: "p1");
        var error = _module.Validate(state, FlipDisc("p1"), "p1");
        error.Should().BeNull();
    }

    [Fact]
    public void Validate_FlipDisc_OpponentTarget_WhenOwnStackHasUnflipped_IsRejected()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p2", 1, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p3", 2, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)])
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Revealing,
            currentPlayerIndex: 0,
            currentBid: 2,
            totalDiscsOnTable: 3,
            challengerId: "p1");
        var error = _module.Validate(state, FlipDisc("p2"), "p1");
        error.Should().NotBeNull();
        error.Should().Contain("own");
    }

    [Fact]
    public void Validate_FlipDisc_OpponentTarget_AfterOwnStackCleared_IsValid()
    {
        // p1's own stack is all flipped; targeting p2 is valid
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, true)]),
            MakePlayer("p2", 1, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p3", 2, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)])
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Revealing,
            currentPlayerIndex: 0,
            currentBid: 2,
            totalDiscsOnTable: 3,
            challengerId: "p1");
        var error = _module.Validate(state, FlipDisc("p2"), "p1");
        error.Should().BeNull();
    }

    // ── Validate — game-over guard ────────────────────────────────────────────

    [Fact]
    public void Validate_AnyAction_WhenFinished_IsRejected()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players, phase: DeadMansSwitchPhase.Finished, winner: "p1");
        _module.Validate(state, PlaceDisc(), "p1").Should().NotBeNull();
        _module.Validate(state, Pass(), "p2").Should().NotBeNull();
    }

    // ── Apply — PlaceDisc ─────────────────────────────────────────────────────

    [Fact]
    public void Apply_PlaceDisc_IncrementsStackCountAndTotalDiscs()
    {
        var state = _module.CreateInitialState(ThreePlayers());
        var next  = _module.Apply(state, PlaceDisc(), "p1");

        next.Players[0].StackCount.Should().Be(1);
        next.Players[0].Stack.Should().HaveCount(1);
        next.TotalDiscsOnTable.Should().Be(1);
    }

    [Fact]
    public void Apply_PlaceDisc_AdvancesToNextActivePlayer()
    {
        var state = _module.CreateInitialState(ThreePlayers());
        var next  = _module.Apply(state, PlaceDisc(), "p1");

        next.CurrentPlayerIndex.Should().Be(1, because: "turn must advance clockwise");
    }

    [Fact]
    public void Apply_PlaceDisc_MultipleRounds_WrapsAround()
    {
        var state = _module.CreateInitialState(ThreePlayers());
        // p1 places, p2 places, p3 places → back to p1
        var s1 = _module.Apply(state, PlaceDisc(), "p1");
        var s2 = _module.Apply(s1,    PlaceDisc(), "p2");
        var s3 = _module.Apply(s2,    PlaceDisc(), "p3");

        s3.CurrentPlayerIndex.Should().Be(0, because: "turn must wrap back to seat 0");
    }

    // ── Apply — StartBid ──────────────────────────────────────────────────────

    [Fact]
    public void Apply_StartBid_TransitionsToBiddingPhase()
    {
        var state = _module.CreateInitialState(ThreePlayers());
        var s1    = _module.Apply(state, PlaceDisc(), "p1");
        var next  = _module.Apply(s1, StartBid(1), "p2");

        next.Phase.Should().Be(DeadMansSwitchPhase.Bidding);
        next.CurrentBid.Should().Be(1);
    }

    [Fact]
    public void Apply_StartBid_SetsChallengerId()
    {
        var state = _module.CreateInitialState(ThreePlayers());
        var s1    = _module.Apply(state, PlaceDisc(), "p1");
        // after PlaceDisc by p1, currentPlayerIndex = 1 (p2)
        var next  = _module.Apply(s1, StartBid(1), "p2");

        next.ChallengerId.Should().Be("p2");
    }

    // ── Apply — RaiseBid ──────────────────────────────────────────────────────

    [Fact]
    public void Apply_RaiseBid_UpdatesCurrentBidAndChallengerId()
    {
        // Setup: bidding phase, p1 is challenger, p2's turn, total=3
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1), MakePlayer("p2", 1, stackCount: 1), MakePlayer("p3", 2, stackCount: 1)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Bidding,
            currentPlayerIndex: 1,
            currentBid: 1,
            totalDiscsOnTable: 3,
            challengerId: "p1");

        var next = _module.Apply(state, RaiseBid(2), "p2");

        next.CurrentBid.Should().Be(2);
        next.ChallengerId.Should().Be("p2");
    }

    [Fact]
    public void Apply_RaiseBid_EqualToTotalDiscs_TransitionsToRevealing()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1), MakePlayer("p2", 1, stackCount: 1), MakePlayer("p3", 2, stackCount: 1)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Bidding,
            currentPlayerIndex: 1,
            currentBid: 1,
            totalDiscsOnTable: 3,
            challengerId: "p1");

        var next = _module.Apply(state, RaiseBid(3), "p2");

        next.Phase.Should().Be(DeadMansSwitchPhase.Revealing);
        next.ChallengerId.Should().Be("p2");
    }

    // ── Apply — Pass ──────────────────────────────────────────────────────────

    [Fact]
    public void Apply_Pass_SetsPlayerPassedFlag()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Bidding,
            currentPlayerIndex: 1,
            currentBid: 1,
            totalDiscsOnTable: 3,
            challengerId: "p1");

        var next = _module.Apply(state, Pass(), "p2");

        next.Players[1].Passed.Should().BeTrue();
    }

    [Fact]
    public void Apply_Pass_AdvancesCurrentPlayerIndex()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Bidding,
            currentPlayerIndex: 1,
            currentBid: 1,
            totalDiscsOnTable: 3,
            challengerId: "p1");

        var next = _module.Apply(state, Pass(), "p2");

        next.CurrentPlayerIndex.Should().Be(2);
    }

    [Fact]
    public void Apply_Pass_LastNonPassedPlayer_AutoTransitionsToRevealing()
    {
        // p1 is challenger, p2 has already passed, p3 now passes → only p1 non-passed
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, passed: false),
            MakePlayer("p2", 1, passed: true),
            MakePlayer("p3", 2, passed: false)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Bidding,
            currentPlayerIndex: 2,
            currentBid: 1,
            totalDiscsOnTable: 3,
            challengerId: "p1");

        var next = _module.Apply(state, Pass(), "p3");

        next.Phase.Should().Be(DeadMansSwitchPhase.Revealing,
            because: "only one non-passed player remains — they become Challenger automatically");
        next.ChallengerId.Should().Be("p1");
    }

    // ── Apply — FlipDisc (Rose hit) ───────────────────────────────────────────

    [Fact]
    public void Apply_FlipDisc_Rose_IncrementsFlipCount()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 2,
                stack: [new DiscSlot(DiscType.Rose, false), new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p2", 1, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p3", 2, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)])
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Revealing,
            currentPlayerIndex: 0,
            currentBid: 3,
            totalDiscsOnTable: 4,
            challengerId: "p1");

        var next = _module.Apply(state, FlipDisc("p1"), "p1");

        next.LastFlip.Should().NotBeNull();
        next.LastFlip!.Result.Should().Be(DiscType.Rose);
        next.Phase.Should().Be(DeadMansSwitchPhase.Revealing,
            because: "bid=3, only 1 flipped so far — game continues");
    }

    [Fact]
    public void Apply_FlipDisc_ReachingBid_TransitionsToRoundOver()
    {
        // bid=1, one unflipped rose → flipping it completes the bid
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1,
                stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p2", 1),
            MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Revealing,
            currentPlayerIndex: 0,
            currentBid: 1,
            totalDiscsOnTable: 1,
            challengerId: "p1");

        var next = _module.Apply(state, FlipDisc("p1"), "p1");

        next.Phase.Should().Be(DeadMansSwitchPhase.RoundOver);
        next.Players[0].PointsWon.Should().Be(1);
    }

    [Fact]
    public void Apply_FlipDisc_TwoSuccessfulRounds_WinsGame()
    {
        // p1 already has 1 point; completing the bid grants the second
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, pointsWon: 1, stackCount: 1,
                stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p2", 1),
            MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Revealing,
            currentPlayerIndex: 0,
            currentBid: 1,
            totalDiscsOnTable: 1,
            challengerId: "p1");

        var next = _module.Apply(state, FlipDisc("p1"), "p1");

        next.Phase.Should().Be(DeadMansSwitchPhase.Finished);
        next.Winner.Should().Be("p1");
    }

    // ── Apply — FlipDisc (own skull) ──────────────────────────────────────────

    [Fact]
    public void Apply_FlipDisc_OwnSkull_TransitionsToDiscardChoice_WhenBothTypesOwned()
    {
        // p1 has roses + skull → DiscardChoice
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, rosesOwned: 2, skullOwned: true, stackCount: 1,
                stack: [new DiscSlot(DiscType.Skull, false)]),
            MakePlayer("p2", 1),
            MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Revealing,
            currentPlayerIndex: 0,
            currentBid: 1,
            totalDiscsOnTable: 1,
            challengerId: "p1");

        var next = _module.Apply(state, FlipDisc("p1"), "p1");

        next.Phase.Should().Be(DeadMansSwitchPhase.DiscardChoice);
    }

    [Fact]
    public void Apply_FlipDisc_OwnSkull_AutoDiscards_WhenOnlyOneTypeOwned()
    {
        // p1 has only skull left (no roses) → auto-discards skull → RoundOver
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, rosesOwned: 0, skullOwned: true, stackCount: 1,
                stack: [new DiscSlot(DiscType.Skull, false)]),
            MakePlayer("p2", 1),
            MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Revealing,
            currentPlayerIndex: 0,
            currentBid: 1,
            totalDiscsOnTable: 1,
            challengerId: "p1");

        var next = _module.Apply(state, FlipDisc("p1"), "p1");

        next.Phase.Should().Be(DeadMansSwitchPhase.RoundOver);
        next.Players[0].SkullOwned.Should().BeFalse();
    }

    // ── Apply — FlipDisc (opponent skull) ────────────────────────────────────

    [Fact]
    public void Apply_FlipDisc_OpponentSkull_TransitionsToRoundOverAndRemovesDiscFromChallenger()
    {
        // p2 stack has a skull; p1 (challenger) flips it → loses a random disc from p1
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, rosesOwned: 3, skullOwned: true,
                stackCount: 0, stack: []),
            MakePlayer("p2", 1, stackCount: 1,
                stack: [new DiscSlot(DiscType.Skull, false)]),
            MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Revealing,
            currentPlayerIndex: 0,
            currentBid: 1,
            totalDiscsOnTable: 1,
            challengerId: "p1");

        var next = _module.Apply(state, FlipDisc("p2"), "p1");

        next.Phase.Should().Be(DeadMansSwitchPhase.RoundOver);

        // p1 must have lost exactly one disc
        int before = 4; // 3 roses + 1 skull
        int after  = next.Players[0].RosesOwned + (next.Players[0].SkullOwned ? 1 : 0);
        after.Should().Be(before - 1);
    }

    [Fact]
    public void Apply_FlipDisc_OpponentSkull_SetsNextRoundFirstPlayerToSkullOwner()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, rosesOwned: 3, skullOwned: true, stack: []),
            MakePlayer("p2", 1, stackCount: 1, stack: [new DiscSlot(DiscType.Skull, false)]),
            MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.Revealing,
            currentPlayerIndex: 0,
            currentBid: 1,
            totalDiscsOnTable: 1,
            challengerId: "p1");

        var next = _module.Apply(state, FlipDisc("p2"), "p1");

        next.NextRoundFirstPlayerIndex.Should().Be(1, because: "skull owner (p2, index 1) leads next round");
    }

    // ── Apply — DiscardDisc ───────────────────────────────────────────────────

    [Fact]
    public void Apply_DiscardDisc_Rose_DecrementsRosesOwned()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, rosesOwned: 2, skullOwned: true),
            MakePlayer("p2", 1),
            MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.DiscardChoice,
            challengerId: "p1");

        var next = _module.Apply(state, DiscardDisc(DiscType.Rose), "p1");

        next.Players[0].RosesOwned.Should().Be(1);
        next.Phase.Should().Be(DeadMansSwitchPhase.RoundOver);
    }

    [Fact]
    public void Apply_DiscardDisc_Skull_RemovesSkull()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, rosesOwned: 2, skullOwned: true),
            MakePlayer("p2", 1),
            MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.DiscardChoice,
            challengerId: "p1");

        var next = _module.Apply(state, DiscardDisc(DiscType.Skull), "p1");

        next.Players[0].SkullOwned.Should().BeFalse();
        next.Phase.Should().Be(DeadMansSwitchPhase.RoundOver);
    }

    [Fact]
    public void Apply_DiscardDisc_LastDisc_EliminatesPlayer()
    {
        // p1 has only 1 rose and no skull — discarding it → active=false
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, rosesOwned: 1, skullOwned: false),
            MakePlayer("p2", 1),
            MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.DiscardChoice,
            challengerId: "p1");

        var next = _module.Apply(state, DiscardDisc(DiscType.Rose), "p1");

        next.Players[0].Active.Should().BeFalse();
    }

    // ── Apply — Elimination → last-player win ─────────────────────────────────

    [Fact]
    public void Apply_DiscardDisc_LastDisc_WithTwoRemainingPlayers_WinsGame()
    {
        // p1 loses last disc, p2 is already eliminated; p3 is the sole survivor
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, rosesOwned: 1, skullOwned: false, active: true),
            MakePlayer("p2", 1, rosesOwned: 0, skullOwned: false, active: false),
            MakePlayer("p3", 2, active: true)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.DiscardChoice,
            challengerId: "p1");

        var next = _module.Apply(state, DiscardDisc(DiscType.Rose), "p1");

        next.Phase.Should().Be(DeadMansSwitchPhase.Finished);
        next.Winner.Should().Be("p3");
    }

    // ── Apply — StartNextRound ────────────────────────────────────────────────

    [Fact]
    public void Apply_StartNextRound_ClearsStacksAndReturnsToPlacing()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p2", 1, stackCount: 1, stack: [new DiscSlot(DiscType.Skull, true)]),
            MakePlayer("p3", 2, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)])
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.RoundOver,
            nextRoundFirstPlayerIndex: 2);

        var next = _module.Apply(state, StartNextRound(), "p1");

        next.Phase.Should().Be(DeadMansSwitchPhase.Placing);
        next.Players.Should().AllSatisfy(p =>
        {
            if (p.Active)
            {
                p.Stack.Should().BeEmpty();
                p.StackCount.Should().Be(0);
                p.Passed.Should().BeFalse();
            }
        });
    }

    [Fact]
    public void Apply_StartNextRound_SetsCurrentPlayerIndexToNextRoundFirstPlayer()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.RoundOver,
            nextRoundFirstPlayerIndex: 2);

        var next = _module.Apply(state, StartNextRound(), "p1");

        next.CurrentPlayerIndex.Should().Be(2);
    }

    [Fact]
    public void Apply_StartNextRound_IncrementsRoundNumber()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players, phase: DeadMansSwitchPhase.RoundOver, roundNumber: 1);

        var next = _module.Apply(state, StartNextRound(), "p1");

        next.RoundNumber.Should().Be(2);
    }

    [Fact]
    public void Apply_StartNextRound_ResetsTotalDiscsAndBid()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players,
            phase: DeadMansSwitchPhase.RoundOver,
            currentBid: 3,
            totalDiscsOnTable: 5,
            challengerId: "p1");

        var next = _module.Apply(state, StartNextRound(), "p1");

        next.CurrentBid.Should().Be(0);
        next.TotalDiscsOnTable.Should().Be(0);
        next.ChallengerId.Should().BeNull();
    }

    // ── State projection ──────────────────────────────────────────────────────

    private DeadMansSwitchState ProjectViaInterface(DeadMansSwitchState state, string playerId)
    {
        var doc       = JsonDocument.Parse(JsonSerializer.Serialize(state,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var projected = ((IGameModule)_module).ProjectStateForPlayer(doc, playerId);
        projected.Should().NotBeNull();
        return JsonSerializer.Deserialize<DeadMansSwitchState>(
            projected!.RootElement.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    [Fact]
    public void ProjectForPlayer_Placing_HidesOpponentStacks()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p2", 1, stackCount: 1, stack: [new DiscSlot(DiscType.Skull, false)]),
            MakePlayer("p3", 2)
        };
        var state     = MakeState(players, phase: DeadMansSwitchPhase.Placing);
        var projected = ProjectViaInterface(state, "p1");

        projected.Players[0].Stack.Should().HaveCount(1, because: "own stack is visible");
        projected.Players[1].Stack.Should().BeEmpty(because: "opponent stack is hidden during Placing");
    }

    [Fact]
    public void ProjectForPlayer_Placing_StackCountReflectsAccurateCount()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0),
            MakePlayer("p2", 1, stackCount: 2, stack: [new DiscSlot(DiscType.Rose, false), new DiscSlot(DiscType.Skull, false)]),
            MakePlayer("p3", 2)
        };
        var state     = MakeState(players, phase: DeadMansSwitchPhase.Placing);
        var projected = ProjectViaInterface(state, "p1");

        projected.Players[1].StackCount.Should().Be(2);
    }

    [Fact]
    public void ProjectForPlayer_Revealing_ShowsFlippedDiscsForOpponents()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 0, stack: [new DiscSlot(DiscType.Rose, true)]),
            MakePlayer("p2", 1, stackCount: 2,
                stack: [new DiscSlot(DiscType.Rose, true), new DiscSlot(DiscType.Skull, false)]),
            MakePlayer("p3", 2)
        };
        var state     = MakeState(players,
            phase: DeadMansSwitchPhase.Revealing,
            challengerId: "p1");
        var projected = ProjectViaInterface(state, "p1");

        // opponent p2: only the flipped disc is visible
        projected.Players[1].Stack.Should().HaveCount(1);
        projected.Players[1].Stack[0].Flipped.Should().BeTrue();
        projected.Players[1].Stack[0].Type.Should().Be(DiscType.Rose);
    }

    [Fact]
    public void ProjectForPlayer_RoundOver_FullStateVisible()
    {
        var players = new List<DevicePlayer>
        {
            MakePlayer("p1", 0, stackCount: 1, stack: [new DiscSlot(DiscType.Rose, false)]),
            MakePlayer("p2", 1, stackCount: 1, stack: [new DiscSlot(DiscType.Skull, false)]),
            MakePlayer("p3", 2)
        };
        var state     = MakeState(players, phase: DeadMansSwitchPhase.RoundOver);
        var projected = ProjectViaInterface(state, "p1");

        projected.Players[1].Stack.Should().HaveCount(1, because: "full state is visible in RoundOver");
    }

    // ── Handle (round-trip via IGameHandler) ──────────────────────────────────

    [Fact]
    public void Handle_PlaceDisc_OnYourTurn_UpdatesState()
    {
        var state   = _module.CreateInitialState(ThreePlayers());
        var stateDoc = JsonDocument.Parse(JsonSerializer.Serialize(state,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var actionDoc = JsonDocument.Parse(JsonSerializer.Serialize(
            PlaceDisc(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var ctx    = new GameContext(stateDoc, actionDoc, "p1", "room-1", 1);
        var result = _module.Handle(ctx);

        result.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void Handle_PlaceDisc_NotYourTurn_ReturnsRejection()
    {
        var state   = _module.CreateInitialState(ThreePlayers());
        var stateDoc = JsonDocument.Parse(JsonSerializer.Serialize(state,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var actionDoc = JsonDocument.Parse(JsonSerializer.Serialize(
            PlaceDisc(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var ctx    = new GameContext(stateDoc, actionDoc, "p2", "room-1", 1);
        var result = _module.Handle(ctx);

        result.RejectionReason.Should().NotBeNull();
    }
}
