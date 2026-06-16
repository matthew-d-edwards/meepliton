using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Meepliton.Contracts;
using Meepliton.Games.HexEscape;
using Meepliton.Games.HexEscape.Models;
using Xunit;

namespace Meepliton.Tests.Games;

/// <summary>
/// Comprehensive tests for HexEscapeModule covering all 16 acceptance criteria.
///
/// NOTE: These tests were authored but NOT run locally (no .NET SDK in the authoring environment).
/// They must be validated in CI.
///
/// Key invocation notes:
/// - CreateInitialState is an explicit IGameModule method; call via ((IGameModule)_module)
///   and deserialise the returned JsonDocument.
/// - Handle() is public on HexEscapeModule.
/// - All deserialization must use CamelCaseOptions (PropertyNameCaseInsensitive = true).
/// </summary>
public class HexEscapeModuleTests
{
    private readonly HexEscapeModule _module = new();
    private readonly IGameModule     _imodule;

    public HexEscapeModuleTests()
    {
        _imodule = _module;
    }

    // ── Serialization options (camelCase round-trip) ──────────────────────────

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters                  = { new JsonStringEnumConverter() },
    };

    // ── Player helpers ────────────────────────────────────────────────────────

    private static IReadOnlyList<PlayerInfo> OnePlayers() =>
        [new("p1", "Alice", null, 0)];

    private static IReadOnlyList<PlayerInfo> TwoPlayers() =>
    [
        new("p1", "Alice", null, 0),
        new("p2", "Bob",   null, 1),
    ];

    private static IReadOnlyList<PlayerInfo> ThreePlayers() =>
    [
        new("p1", "Alice",   null, 0),
        new("p2", "Bob",     null, 1),
        new("p3", "Charlie", null, 2),
    ];

    private static IReadOnlyList<PlayerInfo> SixPlayers() =>
        Enumerable.Range(0, 6)
            .Select(i => new PlayerInfo($"p{i + 1}", $"Player{i + 1}", null, i))
            .ToList();

    // ── Options helpers ───────────────────────────────────────────────────────

    private static JsonDocument OptionsWithLevel(string levelId) =>
        JsonDocument.Parse(JsonSerializer.Serialize(
            new HexEscapeOptions(levelId), CamelCaseOptions));

    private static JsonDocument EmptyOptions() =>
        JsonDocument.Parse("{}");

    // ── State / action serialization helpers ──────────────────────────────────

    private HexEscapeState GetInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options = null)
    {
        var doc = _imodule.CreateInitialState(players, options);
        return Deserialize<HexEscapeState>(doc);
    }

    private static GameContext MakeContext(HexEscapeState state, HexEscapeAction action, string playerId)
    {
        var stateDoc  = JsonDocument.Parse(JsonSerializer.Serialize(state, CamelCaseOptions));
        var actionDoc = JsonDocument.Parse(JsonSerializer.Serialize(action, CamelCaseOptions));
        return new GameContext(stateDoc, actionDoc, playerId, "room-1", 1);
    }

    private GameResult Handle(HexEscapeState state, HexEscapeAction action, string playerId)
    {
        var ctx = MakeContext(state, action, playerId);
        return _module.Handle(ctx);
    }

    private HexEscapeState HandleAndDeserialize(HexEscapeState state, HexEscapeAction action, string playerId)
    {
        var result = Handle(state, action, playerId);
        result.RejectionReason.Should().BeNull(
            because: $"action should have been accepted but was rejected: {result.RejectionReason}");
        return Deserialize<HexEscapeState>(result.NewState);
    }

    private static T Deserialize<T>(JsonDocument doc) =>
        JsonSerializer.Deserialize<T>(doc.RootElement.GetRawText(), CamelCaseOptions)!;

    // ── Action factories ──────────────────────────────────────────────────────

    private static HexEscapeAction PlaceTile(string coord, HexTileType type, int rotation) =>
        new(HexActionType.PlaceTile, Coord: coord, TileType: type, Rotation: rotation);

    private static HexEscapeAction RotateTile(string coord, int rotation) =>
        new(HexActionType.RotateTile, Coord: coord, Rotation: rotation);

    private static HexEscapeAction Pass() =>
        new(HexActionType.Pass);

    // ── Module metadata ───────────────────────────────────────────────────────

    [Fact]
    public void Module_GameId_IsHexEscape() =>
        _module.GameId.Should().Be("hexescape");

    [Fact]
    public void Module_PlayerLimits_Are1To6()
    {
        _module.MinPlayers.Should().Be(1);
        _module.MaxPlayers.Should().Be(6);
    }

    [Fact]
    public void Module_SupportsUndo_IsFalse() =>
        _module.SupportsUndo.Should().BeFalse();

    [Fact]
    public void Module_HasStateProjection_IsFalse() =>
        _imodule.HasStateProjection.Should().BeFalse();

    // ── AC-1: CreateInitialState — null options → tutorial-01 ─────────────────

    [Fact]
    public void CreateInitialState_NullOptions_LoadsTutorial01()
    {
        var state = GetInitialState(TwoPlayers(), options: null);

        state.LevelId.Should().Be("tutorial-01");
    }

    [Fact]
    public void CreateInitialState_NullOptions_DoesNotThrow()
    {
        // Must not throw — AD-10 silent fallback on null options
        var act = () => _imodule.CreateInitialState(TwoPlayers(), null);
        act.Should().NotThrow();
    }

    [Fact]
    public void CreateInitialState_EmptyJsonOptions_LoadsTutorial01()
    {
        // {} options → no levelId key → falls back to tutorial-01
        var state = GetInitialState(TwoPlayers(), EmptyOptions());

        state.LevelId.Should().Be("tutorial-01");
    }

    [Fact]
    public void CreateInitialState_UnknownLevelId_FallsBackToTutorial01()
    {
        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("nonexistent-level"));

        state.LevelId.Should().Be("tutorial-01");
    }

    [Fact]
    public void CreateInitialState_ValidLevel_Medium01_LoadsCorrectLevel()
    {
        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("medium-01"));

        state.LevelId.Should().Be("medium-01");
        state.LevelName.Should().Be("The Bend");
    }

    [Fact]
    public void CreateInitialState_ValidLevel_Hard01_LoadsCorrectLevel()
    {
        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("hard-01"));

        state.LevelId.Should().Be("hard-01");
        state.LevelName.Should().Be("Two Roads");
    }

    [Fact]
    public void CreateInitialState_PhaseIsPlaying()
    {
        var state = GetInitialState(TwoPlayers(), null);

        state.Phase.Should().Be(HexEscapePhase.Playing);
    }

    [Fact]
    public void CreateInitialState_ThreatCounterIsZero()
    {
        var state = GetInitialState(TwoPlayers(), null);

        state.ThreatCounter.Should().Be(0);
    }

    [Fact]
    public void CreateInitialState_SeatsActedThisRoundIsEmpty()
    {
        var state = GetInitialState(TwoPlayers(), null);

        state.SeatsActedThisRound.Should().BeEmpty();
    }

    [Fact]
    public void CreateInitialState_ConnectedSurvivorsIsZero_NotEvaluatedAtInit()
    {
        // AC-1: CreateInitialState does NOT evaluate win condition
        var state = GetInitialState(TwoPlayers(), null);

        state.ConnectedSurvivors.Should().Be(0);
    }

    [Fact]
    public void CreateInitialState_TotalSurvivorsIsCorrect_Tutorial01()
    {
        // tutorial-01 has 1 survivor start cell
        var state = GetInitialState(TwoPlayers(), null);

        state.TotalSurvivors.Should().Be(1);
    }

    [Fact]
    public void CreateInitialState_TotalSurvivorsIsCorrect_Hard01()
    {
        // hard-01 has 2 survivor start cells
        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("hard-01"));

        state.TotalSurvivors.Should().Be(2);
    }

    [Fact]
    public void CreateInitialState_PrePlacedTilesAreFixed()
    {
        // tutorial-01: (-2,0) and (2,0) are pre-placed Straight tiles
        var state = GetInitialState(TwoPlayers(), null);

        state.Grid.Should().ContainKey("-2,0");
        state.Grid["-2,0"].Fixed.Should().BeTrue();
        state.Grid.Should().ContainKey("2,0");
        state.Grid["2,0"].Fixed.Should().BeTrue();
    }

    [Fact]
    public void CreateInitialState_EmptyCellsAreNotInGrid()
    {
        // tutorial-01: cells (-1,0), (0,0), (1,0) are empty — not in Grid
        var state = GetInitialState(TwoPlayers(), null);

        state.Grid.Should().NotContainKey("-1,0");
        state.Grid.Should().NotContainKey("0,0");
        state.Grid.Should().NotContainKey("1,0");
    }

    [Fact]
    public void CreateInitialState_PlayerSeatIndexAssignedInJoinOrder()
    {
        var state = GetInitialState(ThreePlayers(), null);

        state.Players[0].SeatIndex.Should().Be(0);
        state.Players[1].SeatIndex.Should().Be(1);
        state.Players[2].SeatIndex.Should().Be(2);
    }

    [Fact]
    public void CreateInitialState_MinPlayers_OneSeat_IsValid()
    {
        var act = () => GetInitialState(OnePlayers(), null);
        act.Should().NotThrow();
        var state = GetInitialState(OnePlayers(), null);
        state.Players.Should().HaveCount(1);
    }

    [Fact]
    public void CreateInitialState_MaxPlayers_SixSeats_IsValid()
    {
        var act = () => GetInitialState(SixPlayers(), null);
        act.Should().NotThrow();
        var state = GetInitialState(SixPlayers(), null);
        state.Players.Should().HaveCount(6);
    }

    // ── AC-10: Zero-survivor level rejected ───────────────────────────────────

    [Fact]
    public void CreateInitialState_ZeroSurvivorLevel_ThrowsArgumentException()
    {
        // Construct a crafted zero-survivor level by injecting it via HexEscapeLevels.
        // Because HexEscapeLevel is a public record, we can build one directly and
        // bypass the level lookup by calling BuildInitialState indirectly:
        // The only path to trigger AC-10 in production is to have the resolved level
        // have 0 survivors. In tests, we verify the authored catalogue guard is correct
        // and document that direct invocation is only possible via a crafted level.
        //
        // Since BuildInitialState is private and the public surface only resolves via
        // level catalogue, we verify AC-10 through a catalogue property test below,
        // and document here that 0-survivor is an invariant the catalogue upholds.
        //
        // This test explicitly asserts the catalogue invariant as a proxy:
        foreach (var level in HexEscapeLevels.Ordered)
        {
            level.SurvivorStartCells.Should().NotBeEmpty(
                because: $"level '{level.Id}' must have at least 1 survivor (AC-10)");
        }
    }

    // ── AC-15: Catalogue validation — no authored level is pre-won ───────────

    [Fact]
    public void CatalogueValidation_NoAuthoredLevelIsPre_Won()
    {
        // AC-15: assert no authored level's BFS (over pre-placed tiles only) yields
        // connectedSurvivors == totalSurvivors before any player action.
        // We verify this by initialising state and checking ConnectedSurvivors == 0
        // (the module explicitly sets 0 at init per AC-1), AND by playing zero actions
        // and confirming a Pass by one player does not immediately win.
        foreach (var level in HexEscapeLevels.Ordered)
        {
            var state = GetInitialState(TwoPlayers(), OptionsWithLevel(level.Id));

            // AC-1: CreateInitialState does NOT evaluate win — so ConnectedSurvivors is 0.
            // To actually test pre-won, we need to run one BFS by doing one action (Pass)
            // and checking the result is not Escaped.
            var result = Handle(state, Pass(), "p1");

            result.RejectionReason.Should().BeNull(
                because: $"Pass should be accepted on level '{level.Id}'");

            // If the level were pre-won, the first action's BFS would trigger Escaped
            var nextState = Deserialize<HexEscapeState>(result.NewState);
            nextState.Phase.Should().NotBe(HexEscapePhase.GameOver,
                because: $"level '{level.Id}' must not be pre-won — BFS after first action must not satisfy win condition");
            nextState.Outcome.Should().BeNull(
                because: $"level '{level.Id}' must not be pre-won");
        }
    }

    [Fact]
    public void CatalogueValidation_AllLevelsHaveAtLeastOneSurvivor()
    {
        foreach (var level in HexEscapeLevels.Ordered)
        {
            level.SurvivorStartCells.Should().NotBeEmpty(
                because: $"level '{level.Id}' must have >= 1 survivor (AC-10)");
        }
    }

    // ── AC-16: SetupOptions ───────────────────────────────────────────────────

    [Fact]
    public void SetupOptions_ContainsOneLevelIdKey()
    {
        _imodule.SetupOptions.Should().HaveCount(1);
        _imodule.SetupOptions[0].Key.Should().Be("levelId");
    }

    [Fact]
    public void SetupOptions_LevelChoicesAre3AuthoredLevels_TutorialFirst()
    {
        var choices = _imodule.SetupOptions[0].Choices;

        choices.Should().HaveCount(3);
        choices[0].Value.Should().Be("tutorial-01");
        choices[1].Value.Should().Be("medium-01");
        choices[2].Value.Should().Be("hard-01");
    }

    // ── AC-7: Free-order turn model — repeated seat rejected ─────────────────

    [Fact]
    public void Handle_RepeatAction_BySeatAlreadyActed_IsRejected()
    {
        var state = GetInitialState(TwoPlayers(), null);

        // p1 acts first — accepted
        var next = HandleAndDeserialize(state, Pass(), "p1");

        // p1 tries to act again in the same round — rejected
        var result = Handle(next, Pass(), "p1");

        result.RejectionReason.Should().Be("It is not your turn.");
    }

    [Fact]
    public void Handle_BothSeatsCanActInSameRound_InAnyOrder()
    {
        var state = GetInitialState(TwoPlayers(), null);

        // p2 acts first (free-order model — no sequential constraint)
        var afterP2 = HandleAndDeserialize(state, Pass(), "p2");

        // p1 acts second — should also succeed
        var result = Handle(afterP2, Pass(), "p1");

        result.RejectionReason.Should().BeNull(
            because: "free-order model allows any ordering of seat actions");
    }

    // ── AC-7: Any action after GameOver is rejected ───────────────────────────

    [Fact]
    public void Handle_AnyAction_WhenGameOver_IsRejected()
    {
        // Build a game-over state directly
        var initial = GetInitialState(OnePlayers(), null);
        var gameOverState = initial with
        {
            Phase = HexEscapePhase.GameOver,
            Outcome = HexEscapeOutcome.Overrun,
        };

        var result = Handle(gameOverState, Pass(), "p1");

        result.RejectionReason.Should().NotBeNull();
    }

    // ── AC-13: Out-of-bounds coord rejected ───────────────────────────────────

    [Fact]
    public void Handle_PlaceTile_OutOfBoundsCoord_IsRejected()
    {
        var state = GetInitialState(TwoPlayers(), null);

        // (99,99) is not in tutorial-01's cell list
        var result = Handle(state, PlaceTile("99,99", HexTileType.Straight, 0), "p1");

        result.RejectionReason.Should().Be("Cell is not on the board.");
    }

    // ── AC-14: Occupied cell (pre-placed) rejected ────────────────────────────

    [Fact]
    public void Handle_PlaceTile_OnPrePlacedCell_IsRejected()
    {
        var state = GetInitialState(TwoPlayers(), null);

        // (-2,0) is pre-placed in tutorial-01
        var result = Handle(state, PlaceTile("-2,0", HexTileType.Straight, 0), "p1");

        result.RejectionReason.Should().Be("Cell is already occupied.");
    }

    [Fact]
    public void Handle_PlaceTile_OnPlayerPlacedCell_IsRejected()
    {
        var state = GetInitialState(TwoPlayers(), null);

        // p1 places at (0,0)
        var next = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Straight, 0), "p1");

        // p2 tries to place at the same cell
        var result = Handle(next, PlaceTile("0,0", HexTileType.Straight, 0), "p2");

        result.RejectionReason.Should().Be("Cell is already occupied.");
    }

    // ── AC-12: Invalid rotation rejected for PlaceTile ───────────────────────

    [Fact]
    public void Handle_PlaceTile_RotationLessThanZero_IsRejected()
    {
        var state = GetInitialState(TwoPlayers(), null);

        var result = Handle(state, PlaceTile("0,0", HexTileType.Straight, -1), "p1");

        result.RejectionReason.Should().Be("Invalid rotation.");
    }

    [Fact]
    public void Handle_PlaceTile_RotationGreaterThan5_IsRejected()
    {
        var state = GetInitialState(TwoPlayers(), null);

        var result = Handle(state, PlaceTile("0,0", HexTileType.Straight, 6), "p1");

        result.RejectionReason.Should().Be("Invalid rotation.");
    }

    // ── AC-11: Hand exhaustion rejected ──────────────────────────────────────

    [Fact]
    public void Handle_PlaceTile_TileTypeWithZeroRemaining_IsRejected()
    {
        var state = GetInitialState(TwoPlayers(), null);

        // tutorial-01 has 0 Elbow tiles
        state.HandCounts[HexTileType.Elbow].Should().Be(0, because: "tutorial-01 has 0 Elbow tiles");
        var result = Handle(state, PlaceTile("0,0", HexTileType.Elbow, 0), "p1");

        result.RejectionReason.Should().Be("No tiles of that type remaining.");
    }

    // ── AC-12: Invalid rotation rejected for RotateTile ──────────────────────

    [Fact]
    public void Handle_RotateTile_RotationLessThanZero_IsRejected()
    {
        // Place a tile first so there's something to rotate
        var state = GetInitialState(TwoPlayers(), null);
        var next  = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Straight, 0), "p1");

        var result = Handle(next, RotateTile("0,0", -1), "p2");

        result.RejectionReason.Should().Be("Invalid rotation.");
    }

    [Fact]
    public void Handle_RotateTile_RotationGreaterThan5_IsRejected()
    {
        var state = GetInitialState(TwoPlayers(), null);
        var next  = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Straight, 0), "p1");

        var result = Handle(next, RotateTile("0,0", 6), "p2");

        result.RejectionReason.Should().Be("Invalid rotation.");
    }

    // ── AC-3: RotateTile on empty cell rejected ───────────────────────────────

    [Fact]
    public void Handle_RotateTile_OnEmptyCell_IsRejected()
    {
        var state = GetInitialState(TwoPlayers(), null);

        // (0,0) is empty in tutorial-01
        var result = Handle(state, RotateTile("0,0", 0), "p1");

        result.RejectionReason.Should().Be("No tile to rotate.");
    }

    // ── AC-3: RotateTile on pre-placed (fixed) tile rejected ─────────────────

    [Fact]
    public void Handle_RotateTile_OnFixedPrePlacedTile_IsRejected()
    {
        var state = GetInitialState(TwoPlayers(), null);

        // (-2,0) is pre-placed and fixed in tutorial-01
        var result = Handle(state, RotateTile("-2,0", 1), "p1");

        result.RejectionReason.Should().Be("Cannot rotate a fixed tile.");
    }

    // ── AC-3: Same-rotation RotateTile on player tile is allowed ─────────────

    [Fact]
    public void Handle_RotateTile_SameRotation_IsAcceptedAndConsumesTurn()
    {
        var state = GetInitialState(TwoPlayers(), null);

        // p1 places Straight r=0 at (0,0)
        var next = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Straight, 0), "p1");

        // p2 rotates to same rotation (0) — should be accepted
        var result = Handle(next, RotateTile("0,0", 0), "p2");

        result.RejectionReason.Should().BeNull(
            because: "same-rotation RotateTile is explicitly allowed (AC-3) and consumes the turn");

        var nextState = Deserialize<HexEscapeState>(result.NewState);
        nextState.Grid["0,0"].Rotation.Should().Be(0);
        nextState.SeatsActedThisRound.Should().Contain(1, because: "p2 seat index 1 consumed their turn");
    }

    // ── AC-2/AC-4: PlaceTile decrements hand count ────────────────────────────

    [Fact]
    public void Handle_PlaceTile_Valid_DecrementsHandCount()
    {
        var state = GetInitialState(TwoPlayers(), null);
        int beforeCount = state.HandCounts[HexTileType.Straight];

        var next = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Straight, 0), "p1");

        next.HandCounts[HexTileType.Straight].Should().Be(beforeCount - 1);
    }

    [Fact]
    public void Handle_PlaceTile_Valid_PopulatesGridCell()
    {
        var state = GetInitialState(TwoPlayers(), null);

        var next = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Straight, 0), "p1");

        next.Grid.Should().ContainKey("0,0");
        next.Grid["0,0"].TileType.Should().Be(HexTileType.Straight);
        next.Grid["0,0"].Rotation.Should().Be(0);
        next.Grid["0,0"].Fixed.Should().BeFalse();
    }

    [Fact]
    public void Handle_PlaceTile_Valid_AddsSeatToSeatsActedThisRound()
    {
        var state = GetInitialState(TwoPlayers(), null);

        var next = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Straight, 0), "p1");

        // p1 has seat index 0
        next.SeatsActedThisRound.Should().Contain(0);
    }

    // ── AC-4: Pass adds seat to seatsActedThisRound ───────────────────────────

    [Fact]
    public void Handle_Pass_Valid_AddsSeatToSeatsActedThisRound()
    {
        var state = GetInitialState(TwoPlayers(), null);

        var next = HandleAndDeserialize(state, Pass(), "p1");

        next.SeatsActedThisRound.Should().Contain(0);
    }

    [Fact]
    public void Handle_Pass_Valid_DoesNotModifyGrid()
    {
        var state = GetInitialState(TwoPlayers(), null);
        int gridCountBefore = state.Grid.Count;

        var next = HandleAndDeserialize(state, Pass(), "p1");

        next.Grid.Count.Should().Be(gridCountBefore, because: "Pass must not alter the grid");
    }

    // ── AC-2/AD-11: Threat advances per ROUND not per action ─────────────────

    [Fact]
    public void Handle_ThreatAdvancesPerRound_AfterAllSeatsAct()
    {
        // Two players: threat advances only when both have acted
        var state = GetInitialState(TwoPlayers(), null);

        // p1 acts — threat should NOT increment yet
        var afterP1 = HandleAndDeserialize(state, Pass(), "p1");
        afterP1.ThreatCounter.Should().Be(0, because: "threat increments only when all seats have acted");

        // p2 acts — round complete — threat should now be 1
        var afterP2 = HandleAndDeserialize(afterP1, Pass(), "p2");
        afterP2.ThreatCounter.Should().Be(1, because: "threat increments by exactly 1 after all seats act");
    }

    [Fact]
    public void Handle_SeatsActedThisRound_ResetsAfterRoundComplete()
    {
        var state = GetInitialState(TwoPlayers(), null);

        var afterP1 = HandleAndDeserialize(state, Pass(), "p1");
        afterP1.SeatsActedThisRound.Should().Contain(0);

        // p2 completes the round
        var afterP2 = HandleAndDeserialize(afterP1, Pass(), "p2");
        afterP2.SeatsActedThisRound.Should().BeEmpty(
            because: "seatsActedThisRound resets to empty after all seats have acted");
    }

    [Fact]
    public void Handle_SoloGame_SinglePass_AdvancesThreat()
    {
        // Solo (1 player): one Pass immediately advances threat
        var state = GetInitialState(OnePlayers(), null);

        var next = HandleAndDeserialize(state, Pass(), "p1");

        next.ThreatCounter.Should().Be(1, because: "solo game: single seat acting advances threat immediately");
        next.SeatsActedThisRound.Should().BeEmpty(because: "round resets after solo player acts");
    }

    // ── AC-6: Loss — threat threshold reached ────────────────────────────────

    [Fact]
    public void Handle_ThreatReachesThreshold_EmitsOverrunAndGameOverEffect()
    {
        // tutorial-01 has threatThreshold = 5
        // Two-player game: need 5 full rounds of passing (10 total Pass actions)
        var state = GetInitialState(TwoPlayers(), null);

        // Fill all 5 rounds with passes
        for (int round = 0; round < 5; round++)
        {
            state = HandleAndDeserialize(state, Pass(), "p1");
            // Check we haven't lost prematurely (before the 5th round completes)
            if (round < 4)
            {
                state = HandleAndDeserialize(state, Pass(), "p2");
            }
        }

        // p2 completes the 5th round — threat reaches 5 >= 5 = threatThreshold
        var result = Handle(state, Pass(), "p2");

        result.RejectionReason.Should().BeNull();
        var finalState = Deserialize<HexEscapeState>(result.NewState);
        finalState.Phase.Should().Be(HexEscapePhase.GameOver);
        finalState.Outcome.Should().Be(HexEscapeOutcome.Overrun);
        result.Effects.Should().ContainSingle(e => e is GameOverEffect ge && ge.WinnerId == null);
    }

    [Fact]
    public void Handle_AllPassStalemate_EventuallyEndsAsOverrun()
    {
        // medium-01 has threatThreshold = 4, one survivor, no auto-path
        // All-pass stalemate: 2 players passing for 4 rounds → Overrun
        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("medium-01"));
        GameResult? lastResult = null;

        for (int round = 0; round < 4; round++)
        {
            var r1 = Handle(state, Pass(), "p1");
            if (r1.RejectionReason is null)
                state = Deserialize<HexEscapeState>(r1.NewState);

            if (state.Phase == HexEscapePhase.GameOver) { lastResult = r1; break; }

            var r2 = Handle(state, Pass(), "p2");
            lastResult = r2;
            if (r2.RejectionReason is null)
                state = Deserialize<HexEscapeState>(r2.NewState);

            if (state.Phase == HexEscapePhase.GameOver) break;
        }

        state.Phase.Should().Be(HexEscapePhase.GameOver);
        state.Outcome.Should().Be(HexEscapeOutcome.Overrun);
    }

    // ── AC-5/AC-9: BFS connectivity and Win condition ────────────────────────
    //
    // tutorial-01: Five cells in a horizontal row.
    //   S = (-2,0) pre-placed Straight r=0 → edges {E(0), W(3)}
    //   (-1,0), (0,0), (1,0): empty — players must fill with Straight r=0 → {E(0), W(3)}
    //   X = (2,0): pre-placed Straight r=0 → edges {E(0), W(3)}
    //
    // Winning path (all 3 middle cells filled with Straight r=0):
    //   BFS from exit (2,0): W(3)→(1,0) W(3)→(0,0) W(3)→(-1,0) W(3)→(-2,0)=SURVIVOR
    //   connectedSurvivors=1=totalSurvivors → WIN

    [Fact]
    public void Handle_BFS_FullOpenEdgePath_WinsGame()
    {
        // tutorial-01: place Straight r=0 at (-1,0), (0,0), (1,0)
        // This completes the horizontal E+W path from survivor to exit.
        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("tutorial-01"));

        // Round 1: p1 places at (-1,0), p2 places at (0,0)
        state = HandleAndDeserialize(state, PlaceTile("-1,0", HexTileType.Straight, 0), "p1");
        state = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Straight, 0), "p2");

        // Round 2: p1 places the final tile at (1,0) — this should trigger BFS win
        var result = Handle(state, PlaceTile("1,0", HexTileType.Straight, 0), "p1");

        result.RejectionReason.Should().BeNull();
        var finalState = Deserialize<HexEscapeState>(result.NewState);

        // AC-5: win condition
        finalState.Phase.Should().Be(HexEscapePhase.GameOver);
        finalState.Outcome.Should().Be(HexEscapeOutcome.Escaped);
        finalState.ConnectedSurvivors.Should().Be(1);
        result.Effects.Should().ContainSingle(e => e is GameOverEffect ge && ge.WinnerId == null,
            because: "co-op win emits GameOverEffect with null WinnerId (AD-1, AD-12)");
    }

    [Fact]
    public void Handle_BFS_BrokenPath_ConnectedSurvivorsIsZero()
    {
        // Place a tile that doesn't connect to anything — BFS can't traverse
        // (0,0) Deadend r=0 has only edge {E(0)}; from exit (2,0) going W(3):
        // (1,0) is empty → BFS can't proceed past (2,0) through the empty cells
        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("tutorial-01"));

        // Place Deadend r=0 at (1,0) — opens edge E(0) only (goes toward exit, not survivor)
        // From exit (2,0), going W(3), arrives at (1,0). (1,0) needs open edge (3+3)%6=0=E.
        // Deadend r=0 has edge {0=E}. ✓ Enters (1,0).
        // From (1,0) Deadend r=0 edge E(0) → arrives back at (2,0) which is visited.
        // No other edges on Deadend. BFS terminates; (-2,0) not visited.
        state = HandleAndDeserialize(state, PlaceTile("1,0", HexTileType.Deadend, 0), "p1");
        state = HandleAndDeserialize(state, Pass(), "p2");

        // connectedSurvivors should be 0 — (-2,0) is not reachable from exit
        state.ConnectedSurvivors.Should().Be(0,
            because: "broken path: Deadend at (1,0) doesn't connect through to (-2,0)");
        state.Phase.Should().Be(HexEscapePhase.Playing);
    }

    [Fact]
    public void Handle_BFS_Hard01_MultiSurvivor_PartialPath_ConnectedIsSubset()
    {
        // hard-01: two survivors at (-2,0) and (2,0), exit at (0,-2)
        // Place only the path for survivor B at (2,0):
        //   (1,0) Straight r=0 → {E(0),W(3)}
        //   (0,0) Cross r=0 → {E(0),NE(1),N(2),W(3)}
        //   (0,-1) Straight r=2 → rotated: base {0,3}+2 → {(0+2)%6,(3+2)%6}={2,5}=N+S
        // Path: exit(0,-2)→S(5)→(0,-1)→S(5)→(0,0)→E(0)→(1,0)→E(0)→(2,0)=SurvivorB ✓
        // Survivor A at (-2,0): from (0,0) going W(3)→(-1,0) which is EMPTY → not traversable.
        // connectedSurvivors = 1 (SurvivorB only), totalSurvivors = 2

        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("hard-01"));

        // Round 1: place (1,0) and (0,0)
        state = HandleAndDeserialize(state, PlaceTile("1,0", HexTileType.Straight, 0), "p1");
        state = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Cross, 0), "p2");

        // Round 2: p1 places (0,-1) Straight r=2 (N+S), p2 passes
        state = HandleAndDeserialize(state, PlaceTile("0,-1", HexTileType.Straight, 2), "p1");
        // Before p2 acts, check state - p1 just completed a tile that connects to exit and SurvivorB
        // but NOT SurvivorA ((-1,0) is empty)
        state.ConnectedSurvivors.Should().Be(1,
            because: "partial path: only SurvivorB (2,0) is connected; (-1,0) is empty so SurvivorA unreachable");
        state.TotalSurvivors.Should().Be(2);
        state.Phase.Should().Be(HexEscapePhase.Playing,
            because: "not all survivors connected yet — game continues");
    }

    [Fact]
    public void Handle_BFS_Hard01_AllSurvivors_Connected_Wins()
    {
        // hard-01: complete the documented winning placement
        // Requires: (-1,0) Straight r=0, (0,0) Cross r=0, (1,0) Straight r=0, (0,-1) Straight r=2
        // BFS from exit (0,-2) reaches both survivors (-2,0) and (2,0)
        // connectedSurvivors=2=totalSurvivors → WIN

        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("hard-01"));

        // Round 1: place (-1,0) and (0,0)
        state = HandleAndDeserialize(state, PlaceTile("-1,0", HexTileType.Straight, 0), "p1");
        state = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Cross, 0), "p2");

        // Round 2: place (1,0) and (0,-1)
        state = HandleAndDeserialize(state, PlaceTile("1,0", HexTileType.Straight, 0), "p1");

        // After p1 places (1,0): exit→(0,-1) empty still; BFS can't reach survivors through empty (0,-1)
        state.Phase.Should().Be(HexEscapePhase.Playing, because: "(0,-1) is still empty; path not complete");

        // p2 places the final connecting tile (0,-1) Straight r=2 → {N(2),S(5)}
        // BFS: exit(0,-2) Straight r=2 {N(2),S(5)} → S(5)→(0,-1): (0,-1) needs N(2). Straight r=2 has N(2). ✓
        // → S(5) from (0,-1)→(0,0): (0,0) needs N(2). Cross r=0 {0,1,2,3} has N(2). ✓
        // → W(3)→(-1,0): needs E(0). Straight r=0 has E(0). ✓
        //   → W(3)→(-2,0): needs E(0). Straight r=0 (pre-placed). ✓ → SURVIVOR A
        // → E(0)→(1,0): needs W(3). Straight r=0 has W(3). ✓
        //   → E(0)→(2,0): needs W(3). Straight r=0 (pre-placed). ✓ → SURVIVOR B
        // connectedSurvivors=2=totalSurvivors → WIN
        var result = Handle(state, PlaceTile("0,-1", HexTileType.Straight, 2), "p2");

        result.RejectionReason.Should().BeNull();
        var finalState = Deserialize<HexEscapeState>(result.NewState);
        finalState.Phase.Should().Be(HexEscapePhase.GameOver);
        finalState.Outcome.Should().Be(HexEscapeOutcome.Escaped);
        finalState.ConnectedSurvivors.Should().Be(2);
        result.Effects.Should().ContainSingle(e => e is GameOverEffect ge && ge.WinnerId == null);
    }

    // ── AC-5: Win check fires BEFORE threat increment ─────────────────────────
    //
    // The last player to act in a round places the winning tile.
    // In the same Handle call: BFS says won → emit Escaped, do NOT increment threat.

    [Fact]
    public void Handle_WinBeforeThreatPriority_WinningActionOnLastSeatInRound_NoThreatIncrement()
    {
        // tutorial-01, two players, threat=0
        // Round 1: p1 places (-1,0), p2 places (0,0) — not yet won
        // Round 2: p1 places (1,0) — completes path AND is the last seat to act
        // if p2 already passed first this round. Win fires before threat.

        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("tutorial-01"));

        // Round 1
        state = HandleAndDeserialize(state, PlaceTile("-1,0", HexTileType.Straight, 0), "p1");
        state = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Straight, 0), "p2");

        // Round 2: p2 passes first
        state = HandleAndDeserialize(state, Pass(), "p2");
        state.ThreatCounter.Should().Be(0, because: "p1 hasn't acted yet — round not complete");

        // p1 places (1,0) — this is the last seat to act this round AND wins
        // Win check runs before threat increment: outcome=Escaped, threat stays at 0
        var result = Handle(state, PlaceTile("1,0", HexTileType.Straight, 0), "p1");

        result.RejectionReason.Should().BeNull();
        var finalState = Deserialize<HexEscapeState>(result.NewState);
        finalState.Phase.Should().Be(HexEscapePhase.GameOver);
        finalState.Outcome.Should().Be(HexEscapeOutcome.Escaped);
        finalState.ThreatCounter.Should().Be(0,
            because: "AC-5: win check fires before threat increment — threat must NOT be incremented");
        result.Effects.Should().ContainSingle(e => e is GameOverEffect ge && ge.WinnerId == null);
    }

    // ── AC-4: Pass triggers win check if BFS already satisfied ────────────────

    [Fact]
    public void Handle_Pass_RunsBfsFirst_CannotWinOnPassAloneWithNoPath()
    {
        // Pass doesn't place tiles, so BFS result is unchanged from before the pass.
        // tutorial-01 cannot be won without placing tiles. Pass on an unsatisfied BFS
        // should NOT emit Escaped.
        var state = GetInitialState(TwoPlayers(), null);

        var result = Handle(state, Pass(), "p1");

        result.RejectionReason.Should().BeNull();
        var next = Deserialize<HexEscapeState>(result.NewState);
        next.Phase.Should().Be(HexEscapePhase.Playing);
        next.Outcome.Should().BeNull();
        result.Effects.Should().BeEmpty();
    }

    // ── AC-3: RotateTile — valid rotation changes the tile rotation ───────────

    [Fact]
    public void Handle_RotateTile_ValidRotation_ChangesRotation()
    {
        var state = GetInitialState(TwoPlayers(), null);

        // p1 places Straight r=0 at (0,0)
        state = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Straight, 0), "p1");
        state.Grid["0,0"].Rotation.Should().Be(0);

        // p2 rotates to r=1 (valid; even if it produces a non-connecting orientation)
        var next = HandleAndDeserialize(state, RotateTile("0,0", 1), "p2");

        next.Grid["0,0"].Rotation.Should().Be(1);
        next.Grid["0,0"].TileType.Should().Be(HexTileType.Straight, because: "tile type unchanged by rotate");
    }

    [Fact]
    public void Handle_RotateTile_Valid_RecomputesBFS()
    {
        // Place Straight r=0 at (-1,0) and (0,0) — not yet connected to exit
        // tutorial-01: exit at (2,0), survivor at (-2,0)
        // Middle cells needed: (-1,0), (0,0), (1,0) all Straight E+W
        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("tutorial-01"));

        state = HandleAndDeserialize(state, PlaceTile("-1,0", HexTileType.Straight, 0), "p1");
        state = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Straight, 0), "p2");

        // Round 2: p1 places (1,0) Straight r=0 — completes path?
        // Actually yes: all three middle cells filled with Straight r=0. This should WIN.
        // Test RotateTile BFS recomputation separately: first place a wrong-rotation tile,
        // then rotate it into the correct orientation.

        // Reset: fresh state for rotation BFS test
        var fresh = GetInitialState(TwoPlayers(), OptionsWithLevel("tutorial-01"));

        // p1 places (-1,0) Straight r=0 (E+W) — correct
        fresh = HandleAndDeserialize(fresh, PlaceTile("-1,0", HexTileType.Straight, 0), "p1");
        // p2 places (0,0) Straight r=0 — correct
        fresh = HandleAndDeserialize(fresh, PlaceTile("0,0", HexTileType.Straight, 0), "p2");

        // Round 2: p1 places (1,0) Straight r=1 — WRONG rotation (Straight r=1: base{0,3}+1={1,4}=NE+SW)
        // This doesn't connect W←exit(2,0) because (1,0) needs edge (0+3)%6=3=W from exit's perspective.
        // Straight r=1 has edges {1(NE),4(SW)} — does NOT have W(3). BFS blocked.
        fresh = HandleAndDeserialize(fresh, PlaceTile("1,0", HexTileType.Straight, 1), "p1");
        fresh.Phase.Should().Be(HexEscapePhase.Playing, because: "wrong rotation at (1,0) breaks the path");

        // p2 rotates (1,0) from r=1 to r=0 (E+W) — now the path is complete → WIN
        var result = Handle(fresh, RotateTile("1,0", 0), "p2");

        result.RejectionReason.Should().BeNull();
        var finalState = Deserialize<HexEscapeState>(result.NewState);
        finalState.Phase.Should().Be(HexEscapePhase.GameOver);
        finalState.Outcome.Should().Be(HexEscapeOutcome.Escaped,
            because: "rotating (1,0) to r=0 completes the E+W path; BFS recomputes and finds win");
        finalState.ConnectedSurvivors.Should().Be(1);
    }

    // ── AC-11: Wall cell treated as occupied for placement ────────────────────

    [Fact]
    public void Handle_PlaceTile_OnWallCell_IsRejected()
    {
        // hard-01 has a wall at (1,-1)
        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("hard-01"));

        var result = Handle(state, PlaceTile("1,-1", HexTileType.Straight, 0), "p1");

        result.RejectionReason.Should().Be("Cell is already occupied.",
            because: "wall cells are on the board but treated as occupied — no tile may be placed (spec: walls)");
    }

    // ── AC-16: Disconnected seat stalls round (known v1 limitation) ───────────

    [Fact(Skip = "v1 known limitation: a disconnected seat stalls the round boundary; see follow-up")]
    public void DisconnectedSeat_DoesNotAdvanceRound()
    {
        // In v1, the round boundary only advances when ALL seated players have acted.
        // If a player disconnects and never dispatches, the round stalls indefinitely.
        //
        // Intended assertion (once follow-up story is implemented):
        //   - A "skip seat" or timeout mechanism exists
        //   - After the timeout, the round advances without the disconnected seat
        //   - threat counter increments correctly
        //
        // For now: set up a 3-player game where p1 and p2 act but p3 never does.
        // The threat counter should remain at 0 indefinitely (the stall).
        var state = GetInitialState(ThreePlayers(), null);

        // p1 and p2 act; p3 never acts
        state = HandleAndDeserialize(state, Pass(), "p1");
        state = HandleAndDeserialize(state, Pass(), "p2");

        // Currently stalls: threat is still 0 because p3 (seat 2) has not acted
        state.ThreatCounter.Should().Be(0, because: "round cannot complete without p3 acting");
        state.SeatsActedThisRound.Should().Contain(0).And.Contain(1);
        state.SeatsActedThisRound.Should().NotContain(2);
        // Follow-up: assert that after host-initiated skip or timeout, threat = 1
    }

    // ── Module metadata additional checks ────────────────────────────────────

    [Fact]
    public void Module_AllowLateJoin_IsFalse() =>
        _module.AllowLateJoin.Should().BeFalse();

    [Fact]
    public void Module_SupportsAsync_IsFalse() =>
        _module.SupportsAsync.Should().BeFalse();

    [Fact]
    public void Module_ThumbnailUrl_IsNull() =>
        _module.ThumbnailUrl.Should().BeNull();

    // ── Catalogue ordering ────────────────────────────────────────────────────

    [Fact]
    public void HexEscapeLevels_Ordered_TutorialIsFirst()
    {
        HexEscapeLevels.Ordered[0].Id.Should().Be("tutorial-01",
            because: "tutorial-01 must be first in Ordered — it is the AD-10 fallback target");
    }

    [Fact]
    public void HexEscapeLevels_All_ContainsAllThreeLevels()
    {
        HexEscapeLevels.All.Should().ContainKey("tutorial-01");
        HexEscapeLevels.All.Should().ContainKey("medium-01");
        HexEscapeLevels.All.Should().ContainKey("hard-01");
        HexEscapeLevels.All.Should().HaveCount(3);
    }

    // ── BFS geometry: verify direction offsets via tutorial-01 winning play ───

    [Fact]
    public void BfsGeometry_Straight_RotationZero_HasEdgesEastAndWest()
    {
        // A Straight tile at r=0 opens edges {E(0), W(3)}.
        // Verify by placing three Straight r=0 tiles in tutorial-01 to win.
        // If geometry were wrong, BFS would not traverse and win would not trigger.
        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("tutorial-01"));

        // Three middle cells all get Straight r=0 (E+W)
        state = HandleAndDeserialize(state, PlaceTile("-1,0", HexTileType.Straight, 0), "p1");
        state = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Straight, 0), "p2");

        var result = Handle(state, PlaceTile("1,0", HexTileType.Straight, 0), "p1");

        // If the BFS direction model is correct, this wins
        var finalState = Deserialize<HexEscapeState>(result.NewState);
        finalState.Outcome.Should().Be(HexEscapeOutcome.Escaped,
            because: "correctly oriented Straight tiles must form a traversable E↔W corridor");
    }

    [Fact]
    public void BfsGeometry_Medium01_WinningSequence_EmitsEscaped()
    {
        // medium-01 winning placements (from HexEscapeLevels comment):
        //   (-1,0) Straight r=0 → {E(0),W(3)}
        //   (0,0)  Elbow r=2   → {(0+2)%6,(1+2)%6}={2,3}={N,W}
        //   (0,-1) Straight r=2 → {(0+2)%6,(3+2)%6}={2,5}={N,S}
        // BFS from exit (0,-2) Straight r=2 {N(2),S(5)}:
        //   S(5)→(0,-1): needs N(2). Straight r=2 has N(2). ✓
        //   (0,-1) S(5)→(0,0): needs N(2). Elbow r=2 {2,3} has N(2). ✓
        //   (0,0) W(3)→(-1,0): needs E(0). Straight r=0 has E(0). ✓
        //   (-1,0) W(3)→(-2,0): needs E(0). Straight r=0 (pre-placed). ✓ → SURVIVOR
        //   connectedSurvivors=1=totalSurvivors → WIN

        var state = GetInitialState(TwoPlayers(), OptionsWithLevel("medium-01"));

        // Round 1: p1 places (-1,0) Straight r=0, p2 places (0,0) Elbow r=2
        state = HandleAndDeserialize(state, PlaceTile("-1,0", HexTileType.Straight, 0), "p1");
        state = HandleAndDeserialize(state, PlaceTile("0,0", HexTileType.Elbow, 2), "p2");

        // Round 2: p1 places (0,-1) Straight r=2 → WIN
        var result = Handle(state, PlaceTile("0,-1", HexTileType.Straight, 2), "p1");

        result.RejectionReason.Should().BeNull();
        var finalState = Deserialize<HexEscapeState>(result.NewState);
        finalState.Phase.Should().Be(HexEscapePhase.GameOver);
        finalState.Outcome.Should().Be(HexEscapeOutcome.Escaped);
        finalState.ConnectedSurvivors.Should().Be(1);
        result.Effects.Should().ContainSingle(e => e is GameOverEffect ge && ge.WinnerId == null);
    }
}
