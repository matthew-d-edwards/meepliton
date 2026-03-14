using System.Text.Json;
using FluentAssertions;
using Meepliton.Contracts;
using Meepliton.Games.Skyline;
using Meepliton.Games.Skyline.Models;

namespace Meepliton.Tests.Games;

public class SkylineGameTests
{
    private readonly SkylineModule _module = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<PlayerInfo> TwoPlayers() =>
    [
        new("p1", "Alice", null, 0),
        new("p2", "Bob",   null, 1),
    ];

    private static IReadOnlyList<PlayerInfo> FourPlayers() =>
    [
        new("p1", "Alice",   null, 0),
        new("p2", "Bob",     null, 1),
        new("p3", "Charlie", null, 2),
        new("p4", "Diana",   null, 3),
    ];

    /// <summary>
    /// Builds an action JSON document and a GameContext for use with Handle().
    /// </summary>
    private static GameContext MakeContext(SkylineState state, SkylineAction action, string playerId)
    {
        var stateDoc  = JsonDocument.Parse(JsonSerializer.Serialize(state));
        var actionDoc = JsonDocument.Parse(JsonSerializer.Serialize(action));
        return new GameContext(stateDoc, actionDoc, playerId, "room-1", 1);
    }

    /// <summary>
    /// Returns a near-full board state: 24 cells filled, one cell at (4,4) empty.
    /// Player p1 holds tileValue=5 in hand so the last placement is valid.
    /// </summary>
    private static SkylineState NearFullBoardState(string currentPlayerId, int tileValue)
    {
        // 5x5 board, all cells filled with 1 except (4,4) which stays null
        var board = Enumerable.Range(0, 5)
            .Select(r => Enumerable.Range(0, 5)
                .Select(c => (r == 4 && c == 4) ? (int?)null : (int?)1)
                .ToList())
            .ToList();

        var players = new List<PlayerState>
        {
            new("p1", "Alice", null, 0, Score: 0, Hand: [tileValue, 2, 3]),
            new("p2", "Bob",   null, 1, Score: 0, Hand: [4, 5, 6]),
        };

        return new SkylineState(
            Players:         players,
            Board:           board,
            CurrentPlayerId: currentPlayerId,
            Phase:           SkylinePhase.PlacingTile,
            Turn:            25,
            WinnerId:        null
        );
    }

    // ── CreateInitialState ────────────────────────────────────────────────────

    [Fact]
    public void CreateInitialState_TwoPlayers_ReturnsValidState()
    {
        var state = _module.CreateInitialState(TwoPlayers(), options: null);

        state.Should().NotBeNull();
        state.CurrentPlayerId.Should().Be("p1");
        state.Phase.Should().Be(SkylinePhase.PlacingTile);
        state.Turn.Should().Be(1);
        state.WinnerId.Should().BeNull();
    }

    [Fact]
    public void CreateInitialState_TwoPlayers_BoardIs5x5AllEmpty()
    {
        var state = _module.CreateInitialState(TwoPlayers(), options: null);

        state.Board.Should().HaveCount(5);
        foreach (var row in state.Board)
        {
            row.Should().HaveCount(5);
            row.Should().AllSatisfy(cell => cell.Should().BeNull());
        }
    }

    [Fact]
    public void CreateInitialState_TwoPlayers_EachPlayerHas3TilesInHand()
    {
        var state = _module.CreateInitialState(TwoPlayers(), options: null);

        state.Players.Should().HaveCount(2);
        foreach (var player in state.Players)
        {
            player.Hand.Should().HaveCount(3);
            player.Score.Should().Be(0);
        }
    }

    [Fact]
    public void CreateInitialState_MaxFourPlayers_CreatesAllPlayerStates()
    {
        var state = _module.CreateInitialState(FourPlayers(), options: null);

        state.Players.Should().HaveCount(4);
        state.CurrentPlayerId.Should().Be("p1");
        foreach (var player in state.Players)
            player.Hand.Should().HaveCount(3);
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_RejectsActionWhenNotYourTurn()
    {
        var state  = _module.CreateInitialState(TwoPlayers(), options: null);
        var tileValue = state.Players.First(p => p.Id == "p2").Hand[0];
        var action = new SkylineAction("PlaceTile", new PlaceTilePayload(0, 0, tileValue));

        var error = _module.Validate(state, action, "p2");

        error.Should().NotBeNull();
        error.Should().Contain("turn", Exactly.Once(),
            because: "the rejection message should mention turn");
    }

    [Fact]
    public void Validate_AcceptsValidPlacementForCurrentPlayer()
    {
        var state     = _module.CreateInitialState(TwoPlayers(), options: null);
        var tileValue = state.Players.First(p => p.Id == "p1").Hand[0];
        var action    = new SkylineAction("PlaceTile", new PlaceTilePayload(0, 0, tileValue));

        var error = _module.Validate(state, action, "p1");

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_RejectsPlacementOnOccupiedCell()
    {
        var state     = _module.CreateInitialState(TwoPlayers(), options: null);
        var tileValue = state.Players.First(p => p.Id == "p1").Hand[0];
        var action    = new SkylineAction("PlaceTile", new PlaceTilePayload(0, 0, tileValue));

        // Apply a valid first move to occupy (0,0)
        var stateAfterFirst = _module.Apply(state, action);

        // Now p2's turn — p2 tries to place on the same cell
        var p2Tile   = stateAfterFirst.Players.First(p => p.Id == "p2").Hand[0];
        var blocked  = new SkylineAction("PlaceTile", new PlaceTilePayload(0, 0, p2Tile));
        var error    = _module.Validate(stateAfterFirst, blocked, "p2");

        error.Should().NotBeNull();
        error.Should().Contain("occupied", Exactly.Once(),
            because: "the rejection message should say the cell is already occupied");
    }

    [Fact]
    public void Validate_RejectsPlacementOutOfBounds()
    {
        var state     = _module.CreateInitialState(TwoPlayers(), options: null);
        var tileValue = state.Players.First(p => p.Id == "p1").Hand[0];
        var action    = new SkylineAction("PlaceTile", new PlaceTilePayload(5, 5, tileValue));

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("bounds", Exactly.Once(),
            because: "row/col 5 is out of a 5x5 grid");
    }

    [Fact]
    public void Validate_RejectsPlacementOfTileNotInHand()
    {
        var state = _module.CreateInitialState(TwoPlayers(), options: null);
        // Use tile value 0, which is never drawn (DrawTiles uses 1..9)
        var action = new SkylineAction("PlaceTile", new PlaceTilePayload(0, 0, 0));

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("hand", Exactly.Once(),
            because: "tile 0 is not in the player's hand");
    }

    [Fact]
    public void Validate_RejectsAnyActionWhenGameIsOver()
    {
        var finishedState = NearFullBoardState("p1", tileValue: 5);
        var lastMove      = new SkylineAction("PlaceTile", new PlaceTilePayload(4, 4, 5));
        var gameOverState = _module.Apply(finishedState, lastMove);

        // Attempt a further action on the game-over state
        var tileValue = gameOverState.Players.First(p => p.Id == "p1").Hand[0];
        var extraAction = new SkylineAction("PlaceTile", new PlaceTilePayload(0, 0, tileValue));
        var error = _module.Validate(gameOverState, extraAction, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("over", Exactly.Once(),
            because: "no actions are allowed after game over");
    }

    [Fact]
    public void Validate_RejectsUnknownActionType()
    {
        var state  = _module.CreateInitialState(TwoPlayers(), options: null);
        var action = new SkylineAction("Teleport");

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("Unknown action type", Exactly.Once());
    }

    [Fact]
    public void Validate_AcceptsUndoAction()
    {
        var state  = _module.CreateInitialState(TwoPlayers(), options: null);
        var action = new SkylineAction("Undo");

        var error = _module.Validate(state, action, "p1");

        // Undo is accepted by Validate (Apply handles actual undo logic)
        error.Should().BeNull();
    }

    [Fact]
    public void Validate_RejectsMissingPlaceTilePayload()
    {
        var state  = _module.CreateInitialState(TwoPlayers(), options: null);
        var action = new SkylineAction("PlaceTile", PlaceTile: null);

        var error = _module.Validate(state, action, "p1");

        error.Should().NotBeNull();
        error.Should().Contain("payload", Exactly.Once(),
            because: "PlaceTile action requires a non-null PlaceTile payload");
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_ValidPlacement_TilePlacedOnBoard()
    {
        var state     = _module.CreateInitialState(TwoPlayers(), options: null);
        var tileValue = state.Players.First(p => p.Id == "p1").Hand[0];
        var action    = new SkylineAction("PlaceTile", new PlaceTilePayload(2, 3, tileValue));

        var next = _module.Apply(state, action);

        next.Board[2][3].Should().Be(tileValue);
    }

    [Fact]
    public void Apply_ValidPlacement_AdvancesToNextPlayer()
    {
        var state     = _module.CreateInitialState(TwoPlayers(), options: null);
        var tileValue = state.Players.First(p => p.Id == "p1").Hand[0];
        var action    = new SkylineAction("PlaceTile", new PlaceTilePayload(0, 0, tileValue));

        var next = _module.Apply(state, action);

        next.CurrentPlayerId.Should().Be("p2");
    }

    [Fact]
    public void Apply_ValidPlacement_TurnIncrements()
    {
        var state     = _module.CreateInitialState(TwoPlayers(), options: null);
        var tileValue = state.Players.First(p => p.Id == "p1").Hand[0];
        var action    = new SkylineAction("PlaceTile", new PlaceTilePayload(0, 0, tileValue));

        var next = _module.Apply(state, action);

        next.Turn.Should().Be(2);
    }

    [Fact]
    public void Apply_ValidPlacement_PlacedTileRemovedFromHandAndReplaced()
    {
        var state     = _module.CreateInitialState(TwoPlayers(), options: null);
        var tileValue = state.Players.First(p => p.Id == "p1").Hand[0];
        var action    = new SkylineAction("PlaceTile", new PlaceTilePayload(0, 0, tileValue));

        var next = _module.Apply(state, action);

        var p1After = next.Players.First(p => p.Id == "p1");
        // Hand should still have exactly 3 tiles (1 removed, 1 drawn)
        p1After.Hand.Should().HaveCount(3);
    }

    [Fact]
    public void Apply_CompletedRow_ScoresSum()
    {
        // Build a state where row 0 has 4 cells filled with value 1, and p1's hand contains 1.
        // Place at (0,4) to complete the row. Expected row score = 5.
        var board = Enumerable.Range(0, 5)
            .Select(r => Enumerable.Range(0, 5)
                .Select(c => (r == 0 && c < 4) ? (int?)1 : (int?)null)
                .ToList())
            .ToList();

        var players = new List<PlayerState>
        {
            new("p1", "Alice", null, 0, Score: 0, Hand: [1, 2, 3]),
            new("p2", "Bob",   null, 1, Score: 0, Hand: [4, 5, 6]),
        };

        var state = new SkylineState(players, board, "p1", SkylinePhase.PlacingTile, 5, null);
        var action = new SkylineAction("PlaceTile", new PlaceTilePayload(0, 4, 1));

        var next = _module.Apply(state, action);

        var p1After = next.Players.First(p => p.Id == "p1");
        p1After.Score.Should().Be(5, because: "row 0 = 1+1+1+1+1 = 5");
    }

    [Fact]
    public void Apply_CompletedColumn_ScoresSum()
    {
        // Build a state where col 0 has 4 cells filled with value 2, and p1's hand contains 2.
        // Place at (4,0) to complete the column. Expected col score = 10.
        var board = Enumerable.Range(0, 5)
            .Select(r => Enumerable.Range(0, 5)
                .Select(c => (c == 0 && r < 4) ? (int?)2 : (int?)null)
                .ToList())
            .ToList();

        var players = new List<PlayerState>
        {
            new("p1", "Alice", null, 0, Score: 0, Hand: [2, 3, 4]),
            new("p2", "Bob",   null, 1, Score: 0, Hand: [5, 6, 7]),
        };

        var state  = new SkylineState(players, board, "p1", SkylinePhase.PlacingTile, 5, null);
        var action = new SkylineAction("PlaceTile", new PlaceTilePayload(4, 0, 2));

        var next = _module.Apply(state, action);

        var p1After = next.Players.First(p => p.Id == "p1");
        p1After.Score.Should().Be(10, because: "col 0 = 2+2+2+2+2 = 10");
    }

    [Fact]
    public void Apply_TurnWrapsAroundAfterLastPlayer()
    {
        // With 2 players, after p2 places a tile the turn should return to p1.
        var state = _module.CreateInitialState(TwoPlayers(), options: null);

        var tile1 = state.Players.First(p => p.Id == "p1").Hand[0];
        var after1 = _module.Apply(state, new SkylineAction("PlaceTile", new PlaceTilePayload(0, 0, tile1)));

        var tile2 = after1.Players.First(p => p.Id == "p2").Hand[0];
        var after2 = _module.Apply(after1, new SkylineAction("PlaceTile", new PlaceTilePayload(0, 1, tile2)));

        after2.CurrentPlayerId.Should().Be("p1");
    }

    // ── Game-over via Apply and Handle ───────────────────────────────────────

    [Fact]
    public void Apply_FullBoard_SetsPhaseToGameOver()
    {
        const int lastTile = 7;
        var state  = NearFullBoardState("p1", tileValue: lastTile);
        var action = new SkylineAction("PlaceTile", new PlaceTilePayload(4, 4, lastTile));

        var next = _module.Apply(state, action);

        next.Phase.Should().Be(SkylinePhase.GameOver);
    }

    [Fact]
    public void Apply_FullBoard_WinnerIdIsSet()
    {
        const int lastTile = 7;
        var state  = NearFullBoardState("p1", tileValue: lastTile);
        var action = new SkylineAction("PlaceTile", new PlaceTilePayload(4, 4, lastTile));

        var next = _module.Apply(state, action);

        next.WinnerId.Should().NotBeNull();
    }

    // NOTE: ReducerGameModule.Handle does not emit GameOverEffect — it returns
    // new GameResult(Serialize(newState)) with no effects. The platform therefore
    // cannot act on game-over automatically via the Handle pathway. This is a
    // known gap; the test below documents the current (incomplete) behaviour.
    [Fact]
    public void Handle_FullBoard_DoesNotYetEmitGameOverEffect_KnownGap()
    {
        const int lastTile = 7;
        var state  = NearFullBoardState("p1", tileValue: lastTile);
        var action = new SkylineAction("PlaceTile", new PlaceTilePayload(4, 4, lastTile));
        var ctx    = MakeContext(state, action, "p1");

        var result = _module.Handle(ctx);

        result.RejectionReason.Should().BeNull();
        // ReducerGameModule.Handle never sets effects — game over is only visible
        // in the serialised state blob (Phase == GameOver), not via a GameOverEffect.
        result.Effects.Should().BeEmpty(
            because: "ReducerGameModule.Handle does not currently inspect state " +
                     "and emit GameOverEffect; that requires a platform-level fix");
    }

    [Fact]
    public void Handle_FullBoard_NewStateReflectsGameOver()
    {
        const int lastTile = 7;
        var state  = NearFullBoardState("p1", tileValue: lastTile);
        var action = new SkylineAction("PlaceTile", new PlaceTilePayload(4, 4, lastTile));
        var ctx    = MakeContext(state, action, "p1");

        var result = _module.Handle(ctx);

        result.RejectionReason.Should().BeNull();
        var newState = JsonSerializer.Deserialize<SkylineState>(
            result.NewState.RootElement.GetRawText());
        newState!.Phase.Should().Be(SkylinePhase.GameOver);
        newState.WinnerId.Should().NotBeNull();
    }

    [Fact]
    public void Handle_OutOfTurnAction_ReturnsRejection()
    {
        var state  = _module.CreateInitialState(TwoPlayers(), options: null);
        // p2 tries to act on p1's turn
        var p2Tile = state.Players.First(p => p.Id == "p2").Hand[0];
        var action = new SkylineAction("PlaceTile", new PlaceTilePayload(0, 0, p2Tile));
        var ctx    = MakeContext(state, action, "p2");

        var result = _module.Handle(ctx);

        result.RejectionReason.Should().NotBeNull();
        result.RejectionReason.Should().Contain("turn");
    }

    [Fact]
    public void Handle_ValidAction_StateIsUpdatedInReturnedJson()
    {
        var state     = _module.CreateInitialState(TwoPlayers(), options: null);
        var tileValue = state.Players.First(p => p.Id == "p1").Hand[0];
        var action    = new SkylineAction("PlaceTile", new PlaceTilePayload(1, 1, tileValue));
        var ctx       = MakeContext(state, action, "p1");

        var result = _module.Handle(ctx);

        result.RejectionReason.Should().BeNull();

        // Deserialize new state from the returned JsonDocument
        var newState = JsonSerializer.Deserialize<SkylineState>(
            result.NewState.RootElement.GetRawText());
        newState.Should().NotBeNull();
        newState!.CurrentPlayerId.Should().Be("p2");
        newState.Board[1][1].Should().Be(tileValue);
    }

    // ── Game metadata ─────────────────────────────────────────────────────────

    [Fact]
    public void Module_GameId_IsSkyline()
    {
        _module.GameId.Should().Be("skyline");
    }

    [Fact]
    public void Module_PlayerLimits_AreCorrect()
    {
        _module.MinPlayers.Should().Be(2);
        _module.MaxPlayers.Should().Be(4);
    }

    [Fact]
    public void Module_SupportsUndo_IsTrue()
    {
        _module.SupportsUndo.Should().BeTrue();
    }
}
