using System.Text.Json;
using FluentAssertions;
using Meepliton.Contracts;
using Meepliton.Games.Skyline;
using Meepliton.Games.Skyline.Models;
using Xunit;

namespace Meepliton.Tests.Games;

public class SkylineGameTests
{
    private readonly SkylineModule _module = new();

    private static readonly string[] AllHotels =
        ["luxor", "tower", "american", "festival", "worldwide", "continental", "imperial"];

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

    private static PlayerState P(string id, string name = "Player", int cash = 6000,
        Dictionary<string, int>? stocks = null, List<string>? hand = null)
    {
        return new PlayerState(id, name, "#fff", cash,
            Stocks: stocks ?? AllHotels.ToDictionary(h => h, _ => 0),
            Hand: hand ?? []);
    }

    private static SkylineState MakeState(
        List<PlayerState> players,
        Dictionary<string, string>? board = null,
        Dictionary<string, ChainState>? chains = null,
        Dictionary<string, int>? stockBank = null,
        List<string>? bag = null,
        string phase = "place",
        int currentPlayer = 0,
        PendingState? pending = null,
        bool gameOver = false)
    {
        return new SkylineState(
            Players:       players,
            CurrentPlayer: currentPlayer,
            Board:         board       ?? [],
            Chains:        chains      ?? AllHotels.ToDictionary(h => h, _ => new ChainState(false, 0, [])),
            StockBank:     stockBank   ?? AllHotels.ToDictionary(h => h, _ => 25),
            Bag:           bag         ?? [],
            Log:           [],
            GameOver:      gameOver,
            Winner:        null,
            RankedOrder:   null,
            Phase:         phase,
            Pending:       pending);
    }

    private static SkylineAction Action(string type,
        string? tileId = null, string? hotel = null,
        int sell = 0, int trade = 0,
        Dictionary<string, int>? purchases = null)
        => new(type, tileId, hotel, sell, trade, purchases);

    private static GameContext MakeContext(SkylineState state, SkylineAction action, string playerId)
    {
        var stateDoc  = JsonDocument.Parse(JsonSerializer.Serialize(state));
        var actionDoc = JsonDocument.Parse(JsonSerializer.Serialize(action));
        return new GameContext(stateDoc, actionDoc, playerId, "room-1", 1);
    }

    private SkylineState ProjectViaInterface(SkylineState state, string playerId)
    {
        var doc = JsonDocument.Parse(JsonSerializer.Serialize(state));
        var projected = ((IGameModule)_module).ProjectStateForPlayer(doc, playerId);
        projected.Should().NotBeNull("ProjectStateForPlayer must return non-null when HasStateProjection is true");
        return JsonSerializer.Deserialize<SkylineState>(projected!.RootElement.GetRawText())!;
    }

    private static PendingState FoundPending(string triggerTile, List<string> connectedNeutrals) =>
        new(Type: "found", Tiles: connectedNeutrals, Chosen: null,
            Tid: triggerTile, Hotels: null, Survivors: null, Survivor: null,
            Defunct: null, SurvivorChosen: null, DefunctSizes: null,
            DisposeQueue: null, DisposeIdx: null, DisposeDecisions: null);

    // ── Module metadata ────────────────────────────────────────────────────────

    [Fact]
    public void Module_GameId_IsSkyline() =>
        _module.GameId.Should().Be("skyline");

    [Fact]
    public void Module_PlayerLimits_Are2To6()
    {
        _module.MinPlayers.Should().Be(2);
        _module.MaxPlayers.Should().Be(6);
    }

    [Fact]
    public void Module_SupportsUndo_IsFalse() =>
        _module.SupportsUndo.Should().BeFalse();

    [Fact]
    public void Module_HasStateProjection_IsTrue() =>
        ((IGameModule)_module).HasStateProjection.Should().BeTrue();

    // ── CreateInitialState ────────────────────────────────────────────────────

    [Fact]
    public void CreateInitialState_TwoPlayers_ValidBaseState()
    {
        var state = _module.CreateInitialState(TwoPlayers(), null);

        state.Should().NotBeNull();
        state.CurrentPlayer.Should().Be(0);
        state.Phase.Should().Be("place");
        state.GameOver.Should().BeFalse();
        state.Winner.Should().BeNull();
    }

    [Fact]
    public void CreateInitialState_EachPlayerGets6TilesAnd6000Cash()
    {
        var state = _module.CreateInitialState(TwoPlayers(), null);

        foreach (var p in state.Players)
        {
            p.Hand.Should().HaveCount(6);
            p.Cash.Should().Be(6000);
        }
    }

    [Fact]
    public void CreateInitialState_AllChainsInactiveAndSizeZero()
    {
        var state = _module.CreateInitialState(TwoPlayers(), null);

        state.Chains.Should().HaveCount(7);
        state.Chains.Values.Should().AllSatisfy(c =>
        {
            c.Active.Should().BeFalse();
            c.Size.Should().Be(0);
        });
    }

    [Fact]
    public void CreateInitialState_StockBankHas25PerHotel()
    {
        var state = _module.CreateInitialState(TwoPlayers(), null);

        state.StockBank.Should().HaveCount(7);
        state.StockBank.Values.Should().AllSatisfy(v => v.Should().Be(25));
    }

    [Fact]
    public void CreateInitialState_HandsAreDistinct()
    {
        var state = _module.CreateInitialState(TwoPlayers(), null);

        var p1 = state.Players[0].Hand;
        var p2 = state.Players[1].Hand;
        p1.Intersect(p2).Should().BeEmpty(because: "no tile can be in two hands at once");
    }

    [Fact]
    public void CreateInitialState_FourPlayers_AllGetHands()
    {
        var state = _module.CreateInitialState(FourPlayers(), null);

        state.Players.Should().HaveCount(4);
        state.Players.Should().AllSatisfy(p => p.Hand.Should().HaveCount(6));
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_RejectsWhenNotYourTurn()
    {
        var state  = _module.CreateInitialState(TwoPlayers(), null);
        var p2Tile = state.Players[1].Hand[0];
        var error  = _module.Validate(state, Action("PlaceTile", tileId: p2Tile), "p2");

        error.Should().NotBeNull();
        error.Should().Contain("turn");
    }

    [Fact]
    public void Validate_AcceptsCurrentPlayerPlacingOwnTile()
    {
        var state  = _module.CreateInitialState(TwoPlayers(), null);
        var p1Tile = state.Players[0].Hand[0];

        _module.Validate(state, Action("PlaceTile", tileId: p1Tile), "p1").Should().BeNull();
    }

    [Fact]
    public void Validate_RejectsTileNotInHand()
    {
        var state = _module.CreateInitialState(TwoPlayers(), null);
        var error = _module.Validate(state, Action("PlaceTile", tileId: "Z99"), "p1");

        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_RejectsAnyActionWhenGameOver()
    {
        var state = MakeState([P("p1"), P("p2")], gameOver: true);
        var error = _module.Validate(state, Action("PlaceTile", tileId: "A1"), "p1");

        error.Should().NotBeNull();
        error.Should().Contain("over");
    }

    [Fact]
    public void Validate_RejectsUnknownActionType()
    {
        var state = _module.CreateInitialState(TwoPlayers(), null);
        var error = _module.Validate(state, Action("Teleport"), "p1");

        error.Should().NotBeNull();
        error.Should().Contain("Unknown action type");
    }

    [Fact]
    public void Validate_PlaceTile_WrongPhase_Rejected()
    {
        var state = MakeState([P("p1", hand: ["A1"]), P("p2")], phase: "buy");
        var error = _module.Validate(state, Action("PlaceTile", tileId: "A1"), "p1");

        error.Should().NotBeNull();
        error.Should().Contain("phase");
    }

    [Fact]
    public void Validate_BuyStocks_RejectsMoreThan3()
    {
        var chains = AllHotels.ToDictionary(h => h,
            h => h is "luxor" or "tower" ? new ChainState(true, 5, []) : new ChainState(false, 0, []));
        var state = MakeState([P("p1"), P("p2")], chains: chains, phase: "buy");
        var purchases = new Dictionary<string, int> { ["luxor"] = 2, ["tower"] = 2 };
        var error = _module.Validate(state, Action("BuyStocks", purchases: purchases), "p1");

        error.Should().NotBeNull();
        error.Should().Contain("3");
    }

    [Fact]
    public void Validate_BuyStocks_RejectsInactiveChain()
    {
        var state = MakeState([P("p1"), P("p2")], phase: "buy");
        var error = _module.Validate(state,
            Action("BuyStocks", purchases: new Dictionary<string, int> { ["luxor"] = 1 }), "p1");

        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_BuyStocks_RejectsInsufficientCash()
    {
        var chains = AllHotels.ToDictionary(h => h,
            h => h == "luxor" ? new ChainState(true, 5, []) : new ChainState(false, 0, []));
        var state = MakeState([P("p1", cash: 0), P("p2")], chains: chains, phase: "buy");
        var error = _module.Validate(state,
            Action("BuyStocks", purchases: new Dictionary<string, int> { ["luxor"] = 1 }), "p1");

        error.Should().NotBeNull();
        error.Should().Contain("cash", Exactly.Once());
    }

    // ── Apply — PlaceTile ─────────────────────────────────────────────────────

    [Fact]
    public void Apply_PlaceTile_IsolatedTile_PlacedAsNeutral()
    {
        var players = new List<PlayerState> { P("p1", hand: ["A1"]), P("p2") };
        var state   = MakeState(players);
        var next    = _module.Apply(state, Action("PlaceTile", tileId: "A1"));

        next.Board.Should().ContainKey("A1");
        next.Board["A1"].Should().Be("neutral");
    }

    [Fact]
    public void Apply_PlaceTile_IsolatedTile_PhaseIsBuy()
    {
        var state = MakeState([P("p1", hand: ["A1"]), P("p2")]);
        var next  = _module.Apply(state, Action("PlaceTile", tileId: "A1"));

        next.Phase.Should().Be("buy");
    }

    [Fact]
    public void Apply_PlaceTile_TileRemovedFromHand()
    {
        var state = MakeState([P("p1", hand: ["A1", "B2"]), P("p2")]);
        var next  = _module.Apply(state, Action("PlaceTile", tileId: "A1"));

        next.Players[0].Hand.Should().NotContain("A1");
        next.Players[0].Hand.Should().Contain("B2");
    }

    [Fact]
    public void Apply_PlaceTile_AdjacentToNeutral_PhaseIsFound()
    {
        // A1 neutral already on board; player places A2 (adjacent) → must found a chain
        var board   = new Dictionary<string, string> { ["A1"] = "neutral" };
        var players = new List<PlayerState> { P("p1", hand: ["A2"]), P("p2") };
        var state   = MakeState(players, board: board);
        var next    = _module.Apply(state, Action("PlaceTile", tileId: "A2"));

        next.Phase.Should().Be("found");
        next.Pending.Should().NotBeNull();
        next.Pending!.Type.Should().Be("found");
    }

    [Fact]
    public void Apply_PlaceTile_AdjacentToHotel_ExtendsChain()
    {
        var board  = new Dictionary<string, string> { ["A1"] = "luxor" };
        var chains = AllHotels.ToDictionary(h => h,
            h => h == "luxor" ? new ChainState(true, 1, ["A1"]) : new ChainState(false, 0, []));
        var state  = MakeState([P("p1", hand: ["A2"]), P("p2")], board: board, chains: chains);
        var next   = _module.Apply(state, Action("PlaceTile", tileId: "A2"));

        next.Phase.Should().Be("buy");
        next.Chains["luxor"].Size.Should().Be(2);
        next.Board["A2"].Should().Be("luxor");
    }

    // ── Apply — FoundHotel ────────────────────────────────────────────────────

    [Fact]
    public void Apply_FoundHotel_ActivatesChainWithCorrectSize()
    {
        var board   = new Dictionary<string, string> { ["A1"] = "neutral", ["A2"] = "neutral" };
        var pending = FoundPending("A2", ["A1", "A2"]);
        var state   = MakeState([P("p1"), P("p2")], board: board, phase: "found", pending: pending);
        var next    = _module.Apply(state, Action("FoundHotel", hotel: "luxor"));

        next.Chains["luxor"].Active.Should().BeTrue();
        next.Chains["luxor"].Size.Should().Be(2);
    }

    [Fact]
    public void Apply_FoundHotel_GivesFreeShareToFounder()
    {
        var board   = new Dictionary<string, string> { ["A1"] = "neutral", ["A2"] = "neutral" };
        var pending = FoundPending("A2", ["A1", "A2"]);
        var state   = MakeState([P("p1"), P("p2")], board: board, phase: "found", pending: pending);
        var next    = _module.Apply(state, Action("FoundHotel", hotel: "luxor"));

        next.Players[0].Stocks["luxor"].Should().Be(1);
        next.StockBank["luxor"].Should().Be(24);
    }

    [Fact]
    public void Apply_FoundHotel_PhaseMovesToBuyAndPendingCleared()
    {
        var board   = new Dictionary<string, string> { ["A1"] = "neutral", ["A2"] = "neutral" };
        var pending = FoundPending("A2", ["A1", "A2"]);
        var state   = MakeState([P("p1"), P("p2")], board: board, phase: "found", pending: pending);
        var next    = _module.Apply(state, Action("FoundHotel", hotel: "luxor"));

        next.Phase.Should().Be("buy");
        next.Pending.Should().BeNull();
    }

    // ── Apply — BuyStocks ─────────────────────────────────────────────────────

    [Fact]
    public void Apply_BuyStocks_DeductsCashAndAddsStock()
    {
        // Luxor size=5 → tier 4 → $500
        var chains = AllHotels.ToDictionary(h => h,
            h => h == "luxor" ? new ChainState(true, 5, []) : new ChainState(false, 0, []));
        var state  = MakeState([P("p1", cash: 6000), P("p2")], chains: chains, phase: "buy");
        var next   = _module.Apply(state,
            Action("BuyStocks", purchases: new Dictionary<string, int> { ["luxor"] = 1 }));

        next.Players[0].Cash.Should().Be(5500);
        next.Players[0].Stocks["luxor"].Should().Be(1);
        next.StockBank["luxor"].Should().Be(24);
    }

    [Fact]
    public void Apply_BuyStocks_EmptyPurchases_PhaseMovesToDraw()
    {
        var state = MakeState([P("p1"), P("p2")], phase: "buy");
        var next  = _module.Apply(state, Action("BuyStocks", purchases: []));

        next.Phase.Should().Be("draw");
    }

    // ── Apply — EndTurn ───────────────────────────────────────────────────────

    [Fact]
    public void Apply_EndTurn_AdvancesToNextPlayer()
    {
        var bag   = new List<string> { "I12" };
        var state = MakeState([P("p1"), P("p2")], bag: bag, phase: "draw");
        var next  = _module.Apply(state, Action("EndTurn"));

        next.CurrentPlayer.Should().Be(1);
        next.Phase.Should().Be("place");
    }

    [Fact]
    public void Apply_EndTurn_DrawsTileFromBagToRefillHand()
    {
        var hand  = new List<string> { "A1", "A2", "A3", "A4", "A5" };
        var bag   = new List<string> { "I12" };
        var state = MakeState([P("p1", hand: hand), P("p2")], bag: bag, phase: "draw");
        var next  = _module.Apply(state, Action("EndTurn"));

        next.Players[0].Hand.Should().HaveCount(6);
        next.Players[0].Hand.Should().Contain("I12");
        next.Bag.Should().BeEmpty();
    }

    [Fact]
    public void Apply_EndTurn_WrapsAroundAfterLastPlayer()
    {
        var bag   = Enumerable.Range(1, 12).Select(i => $"I{i}").ToList();
        var state = MakeState([P("p1"), P("p2")], bag: bag, phase: "draw", currentPlayer: 1);
        var next  = _module.Apply(state, Action("EndTurn"));

        next.CurrentPlayer.Should().Be(0, because: "turn must wrap to player 0 after the last player");
    }

    [Fact]
    public void Apply_EndTurn_SkipsBuyPhase()
    {
        // EndTurn is also valid from "buy" phase (skip buying)
        var bag   = new List<string> { "I12" };
        var state = MakeState([P("p1"), P("p2")], bag: bag, phase: "buy");
        var next  = _module.Apply(state, Action("EndTurn"));

        next.CurrentPlayer.Should().Be(1);
        next.Phase.Should().Be("place");
    }

    // ── Apply — EndGame ───────────────────────────────────────────────────────

    [Fact]
    public void Apply_EndGame_SetsGameOverAndWinner()
    {
        // One active chain, size >= 11 → end-game valid
        var chains = AllHotels.ToDictionary(h => h,
            h => h == "luxor"
                ? new ChainState(true, 11, Enumerable.Range(1, 11).Select(i => $"A{i}").ToList())
                : new ChainState(false, 0, []));
        var state = MakeState([P("p1", cash: 5000), P("p2", cash: 3000)], chains: chains, phase: "buy");
        var next  = _module.Apply(state, Action("EndGame"));

        next.GameOver.Should().BeTrue();
        next.Winner.Should().NotBeNull();
        next.RankedOrder.Should().NotBeNull();
    }

    [Fact]
    public void Apply_EndGame_RicherPlayerWins()
    {
        var chains = AllHotels.ToDictionary(h => h,
            h => h == "luxor"
                ? new ChainState(true, 11, Enumerable.Range(1, 11).Select(i => $"A{i}").ToList())
                : new ChainState(false, 0, []));
        // p1 cash=5000, p2 cash=3000 → p1 should win
        var state = MakeState([P("p1", cash: 5000), P("p2", cash: 3000)], chains: chains, phase: "buy");
        var next  = _module.Apply(state, Action("EndGame"));

        next.RankedOrder![0].Should().Be(0, because: "p1 has more cash and should be ranked first");
    }

    // ── Handle ────────────────────────────────────────────────────────────────

    [Fact]
    public void Handle_OutOfTurnAction_ReturnsRejection()
    {
        var state  = _module.CreateInitialState(TwoPlayers(), null);
        var p2Tile = state.Players[1].Hand[0];
        var ctx    = MakeContext(state, Action("PlaceTile", tileId: p2Tile), "p2");
        var result = _module.Handle(ctx);

        result.RejectionReason.Should().NotBeNull();
        result.RejectionReason.Should().Contain("turn");
    }

    [Fact]
    public void Handle_ValidPlaceTile_StateBoardUpdated()
    {
        var state  = _module.CreateInitialState(TwoPlayers(), null);
        var p1Tile = state.Players[0].Hand[0];
        var ctx    = MakeContext(state, Action("PlaceTile", tileId: p1Tile), "p1");
        var result = _module.Handle(ctx);

        result.RejectionReason.Should().BeNull();
        var newState = JsonSerializer.Deserialize<SkylineState>(result.NewState.RootElement.GetRawText())!;
        newState.Board.Should().ContainKey(p1Tile);
    }

    // ── State projection ──────────────────────────────────────────────────────

    [Fact]
    public void ProjectForPlayer_RequestingPlayerSeesOwnHand()
    {
        var state     = _module.CreateInitialState(TwoPlayers(), null);
        var p1Hand    = state.Players[0].Hand;
        var projected = ProjectViaInterface(state, "p1");

        projected.Players[0].Hand.Should().BeEquivalentTo(p1Hand);
    }

    [Fact]
    public void ProjectForPlayer_OpponentHandIsHidden()
    {
        var state     = _module.CreateInitialState(TwoPlayers(), null);
        var projected = ProjectViaInterface(state, "p1");

        projected.Players[1].Hand.Should().BeEmpty(because: "opponents' hands must not be revealed");
    }

    [Fact]
    public void ProjectForPlayer_UnknownPlayerSeesNoHands()
    {
        var state     = _module.CreateInitialState(TwoPlayers(), null);
        var projected = ProjectViaInterface(state, "spectator-xyz");

        projected.Players.Should().AllSatisfy(p => p.Hand.Should().BeEmpty());
    }

    [Fact]
    public void ProjectForPlayer_BoardStocksAndCashAreFullyVisible()
    {
        var state     = _module.CreateInitialState(TwoPlayers(), null);
        var projected = ProjectViaInterface(state, "p1");

        projected.Board.Should().BeEquivalentTo(state.Board);
        projected.StockBank.Should().BeEquivalentTo(state.StockBank);
        projected.Players.Select(p => p.Cash)
            .Should().BeEquivalentTo(state.Players.Select(p => p.Cash));
    }

    [Fact]
    public void ProjectForPlayer_DoesNotMutateInputDocument()
    {
        var state    = _module.CreateInitialState(TwoPlayers(), null);
        var doc      = JsonDocument.Parse(JsonSerializer.Serialize(state));
        var original = doc.RootElement.GetRawText();

        ((IGameModule)_module).ProjectStateForPlayer(doc, "p1");

        doc.RootElement.GetRawText().Should().Be(original,
            because: "ProjectStateForPlayer must be pure and must not mutate the input document");
    }
}
