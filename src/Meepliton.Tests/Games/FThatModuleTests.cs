using System.Text.Json;
using FluentAssertions;
using Meepliton.Contracts;
using Meepliton.Games.FThat;
using Meepliton.Games.FThat.Models;
using Xunit;

namespace Meepliton.Tests.Games;

public class FThatModuleTests
{
    private readonly FThatModule _module = new();

    // ── Player-list helpers ───────────────────────────────────────────────────

    private static IReadOnlyList<PlayerInfo> Players(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new PlayerInfo($"p{i + 1}", $"Player{i + 1}", null, i))
            .ToList();

    private static IReadOnlyList<PlayerInfo> ThreePlayers() => Players(3);
    private static IReadOnlyList<PlayerInfo> SevenPlayers() => Players(7);

    // ── Action helpers ────────────────────────────────────────────────────────

    private static FThatAction Take() => new(FThatActionType.Take);
    private static FThatAction Pass() => new(FThatActionType.Pass);

    // ── State builder helpers ─────────────────────────────────────────────────

    private static FThatPlayer MakePlayer(string id, int seatIndex, int chips = 11, List<int>? cards = null) =>
        new(id, $"Player {id}", null, seatIndex, chips, cards ?? []);

    private static FThatState MakeState(
        List<FThatPlayer>   players,
        List<int>?          deck            = null,
        int                 faceUpCard      = 10,
        int                 chipsOnCard     = 0,
        int                 currentPlayerIndex = 0,
        FThatPhase          phase           = FThatPhase.Playing,
        List<FThatScore>?   scores          = null,
        List<string>?       winners         = null) =>
        new(phase, players, currentPlayerIndex, deck ?? [], faceUpCard, chipsOnCard, scores, winners);

    // ── Module metadata ───────────────────────────────────────────────────────

    [Fact]
    public void Module_GameId_IsFThat() =>
        _module.GameId.Should().Be("fthat");

    [Fact]
    public void Module_PlayerLimits_Are3To7()
    {
        _module.MinPlayers.Should().Be(3);
        _module.MaxPlayers.Should().Be(7);
    }

    [Fact]
    public void Module_HasStateProjection_IsTrue() =>
        ((IGameModule)_module).HasStateProjection.Should().BeTrue();

    // ── CreateInitialState (AC-1) ─────────────────────────────────────────────

    [Fact]
    public void CreateInitialState_ThreePlayers_PhaseIsPlaying()
    {
        var state = _module.CreateInitialState(ThreePlayers(), null);
        state.Phase.Should().Be(FThatPhase.Playing);
        state.CurrentPlayerIndex.Should().Be(0);
    }

    [Fact]
    public void CreateInitialState_DeckHas23Cards()
    {
        var state = _module.CreateInitialState(ThreePlayers(), null);
        state.Deck.Should().HaveCount(23);
    }

    [Fact]
    public void CreateInitialState_DeckPlusFaceUpAre24UniqueCardsFrom3To35()
    {
        var state      = _module.CreateInitialState(ThreePlayers(), null);
        var allPresent = new List<int>(state.Deck) { state.FaceUpCard };

        allPresent.Should().HaveCount(24);
        allPresent.Should().OnlyContain(c => c >= 3 && c <= 35);
        allPresent.Distinct().Should().HaveCount(24, because: "all 24 playable cards are unique");
    }

    [Fact]
    public void CreateInitialState_ChipsOnCardIsZero()
    {
        var state = _module.CreateInitialState(ThreePlayers(), null);
        state.ChipsOnCard.Should().Be(0);
    }

    [Fact]
    public void CreateInitialState_DefaultStartingChipsIs11()
    {
        var state = _module.CreateInitialState(ThreePlayers(), null);
        state.Players.Should().AllSatisfy(p => p.Chips.Should().Be(11));
    }

    [Fact]
    public void CreateInitialState_StartingChipsClampedToMin7()
    {
        var state = _module.CreateInitialState(ThreePlayers(), new FThatOptions(StartingChips: 3));
        state.Players.Should().AllSatisfy(p => p.Chips.Should().Be(7));
    }

    [Fact]
    public void CreateInitialState_StartingChipsClampedToMax15()
    {
        var state = _module.CreateInitialState(ThreePlayers(), new FThatOptions(StartingChips: 99));
        state.Players.Should().AllSatisfy(p => p.Chips.Should().Be(15));
    }

    [Fact]
    public void CreateInitialState_SevenPlayers_ValidState()
    {
        var state = _module.CreateInitialState(SevenPlayers(), null);
        state.Players.Should().HaveCount(7);
        state.Deck.Should().HaveCount(23);
    }

    [Fact]
    public void CreateInitialState_AllPlayersStartWithNoCards()
    {
        var state = _module.CreateInitialState(ThreePlayers(), null);
        state.Players.Should().AllSatisfy(p => p.Cards.Should().BeEmpty());
    }

    // ── Validate — AC-7 wrong turn ────────────────────────────────────────────

    [Fact]
    public void Validate_AnyAction_NotYourTurn_IsRejected()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players, deck: [5, 6, 7], currentPlayerIndex: 0);

        _module.Validate(state, Take(), "p2").Should().NotBeNull().And.Contain("turn");
        _module.Validate(state, Pass(), "p3").Should().NotBeNull().And.Contain("turn");
    }

    // ── Validate — AC-3 no chips ──────────────────────────────────────────────

    [Fact]
    public void Validate_Pass_WithZeroChips_IsRejected()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players, deck: [5, 6]);

        var error = _module.Validate(state, Pass(), "p1");
        error.Should().NotBeNull();
        error.Should().Contain("no chips");
    }

    [Fact]
    public void Validate_Pass_WithChips_IsValid()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 1), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players, deck: [5, 6]);

        _module.Validate(state, Pass(), "p1").Should().BeNull();
    }

    [Fact]
    public void Validate_Take_IsAlwaysValidOnYourTurn()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players, deck: [5, 6]);

        _module.Validate(state, Take(), "p1").Should().BeNull();
    }

    [Fact]
    public void Validate_AnyAction_WhenGameOver_IsRejected()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players, deck: [], phase: FThatPhase.GameOver);

        _module.Validate(state, Take(), "p1").Should().NotBeNull();
        _module.Validate(state, Pass(), "p1").Should().NotBeNull();
    }

    // ── Apply — Pass (AC-2) ───────────────────────────────────────────────────

    [Fact]
    public void Apply_Pass_DeductsOneChipFromCurrentPlayer()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 5), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state       = MakeState(players, deck: [5, 6], chipsOnCard: 0);
        var (next, _)   = _module.Apply(state, Pass());

        next.Players[0].Chips.Should().Be(4);
    }

    [Fact]
    public void Apply_Pass_IncrementsChipsOnCard()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 5), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state     = MakeState(players, deck: [5, 6], chipsOnCard: 2);
        var (next, _) = _module.Apply(state, Pass());

        next.ChipsOnCard.Should().Be(3);
    }

    [Fact]
    public void Apply_Pass_AdvancesCurrentPlayerClockwise()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 5), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state     = MakeState(players, deck: [5, 6], currentPlayerIndex: 0);
        var (next, _) = _module.Apply(state, Pass());

        next.CurrentPlayerIndex.Should().Be(1);
    }

    [Fact]
    public void Apply_Pass_WrapsClockwise_FromLastToFirst()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2, chips: 5)
        };
        var state     = MakeState(players, deck: [5, 6], currentPlayerIndex: 2);
        var (next, _) = _module.Apply(state, Pass());

        next.CurrentPlayerIndex.Should().Be(0);
    }

    // ── Apply — Take mid-game (AC-4) ──────────────────────────────────────────

    [Fact]
    public void Apply_Take_MidGame_MovesCardAndChipsToPlayer()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 5), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state     = MakeState(players, deck: [15, 20], faceUpCard: 10, chipsOnCard: 3);
        var (next, _) = _module.Apply(state, Take());

        next.Players[0].Cards.Should().Contain(10);
        next.Players[0].Chips.Should().Be(8); // 5 + 3 chips on card
    }

    [Fact]
    public void Apply_Take_MidGame_AdvancesDeckAndResetsChips()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 5), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state     = MakeState(players, deck: [15, 20], faceUpCard: 10, chipsOnCard: 3);
        var (next, _) = _module.Apply(state, Take());

        next.FaceUpCard.Should().Be(15);
        next.Deck.Should().HaveCount(1);
        next.Deck[0].Should().Be(20);
        next.ChipsOnCard.Should().Be(0);
    }

    [Fact]
    public void Apply_Take_MidGame_TurnStaysWithSamePlayer()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 5), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state     = MakeState(players, deck: [15, 20], faceUpCard: 10, currentPlayerIndex: 0);
        var (next, _) = _module.Apply(state, Take());

        next.CurrentPlayerIndex.Should().Be(0, because: "after taking, the turn remains with the same player");
    }

    [Fact]
    public void Apply_Take_MidGame_PhaseRemainsPlaying()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 5), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state     = MakeState(players, deck: [15], faceUpCard: 10);
        var (next, _) = _module.Apply(state, Take());

        next.Phase.Should().Be(FThatPhase.Playing);
    }

    // ── Apply — Take last card (AC-5) ─────────────────────────────────────────

    [Fact]
    public void Apply_Take_LastCard_PhaseBecomesGameOver()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 5), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state        = MakeState(players, deck: [], faceUpCard: 10);
        var (next, effects) = _module.Apply(state, Take());

        next.Phase.Should().Be(FThatPhase.GameOver);
        effects.Should().ContainSingle(e => e is GameOverEffect);
    }

    [Fact]
    public void Apply_Take_LastCard_ScoresAreComputed()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 5), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state      = MakeState(players, deck: [], faceUpCard: 10);
        var (next, _)  = _module.Apply(state, Take());

        next.Scores.Should().NotBeNull();
        next.Scores.Should().HaveCount(3);
    }

    [Fact]
    public void Apply_Take_LastCard_WinnersListPopulated()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 5), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state     = MakeState(players, deck: [], faceUpCard: 10);
        var (next, _) = _module.Apply(state, Take());

        next.Winners.Should().NotBeNull();
        next.Winners.Should().NotBeEmpty();
    }

    [Fact]
    public void Apply_Take_LastCard_LowestTotalScoreWins()
    {
        // p1 takes card 10, has 5 chips → total = 10 - 5 = 5
        // p2 holds card 20, 11 chips     → total = 20 - 11 = 9
        // p3 holds card 30, 11 chips     → total = 30 - 11 = 19
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 5,  cards: []),     // takes card 10 → total 5
            MakePlayer("p2", 1, chips: 11, cards: [20]),   // total 9
            MakePlayer("p3", 2, chips: 11, cards: [30])    // total 19
        };
        var state     = MakeState(players, deck: [], faceUpCard: 10);
        var (next, _) = _module.Apply(state, Take());

        next.Winners.Should().Contain("p1");
        next.Winners.Should().HaveCount(1);
    }

    [Fact]
    public void Apply_Take_LastCard_GameOverEffect_CarriesLowestSeatWinner()
    {
        // p1 and p2 tie; p1 is lower seat index → GameOverEffect carries p1
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 10, cards: []),   // takes 10, total = 10-10=0
            MakePlayer("p2", 1, chips: 10, cards: []),   // no cards, chips=10 → total = 0-10=-10
            MakePlayer("p3", 2, chips: 11, cards: [30])  // total 30-11=19
        };
        // p2 has no cards and 10 chips → score = 0 cardScore - 10 chips = -10
        // p1 takes 10 with 10 chips → score = 10 - 10 = 0
        // Winner is p2 (lowest total)
        var state        = MakeState(players, deck: [], faceUpCard: 10);
        var (next, effects) = _module.Apply(state, Take());

        var gameOver = effects.OfType<GameOverEffect>().SingleOrDefault();
        gameOver.Should().NotBeNull();
        // The winner is whoever has lowest total; p2 has total -10
        next.Winners![0].Should().Be("p2");
        gameOver!.WinnerId.Should().Be("p2");
    }

    // ── Chain scoring (AC-6) ──────────────────────────────────────────────────

    [Fact]
    public void Scoring_ChainMinimum_CorrectForConsecutiveRun()
    {
        // Player holds [7, 8, 9, 20] with 3 chips remaining
        // Sorted: 7, 8, 9, 20
        // Chains: {7,8,9} → min is 7; {20} → min is 20
        // cardScore = 7 + 20 = 27; total = 27 - 3 = 24
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 3, cards: [7, 8, 9, 20]),
            MakePlayer("p2", 1, chips: 11, cards: []),
            MakePlayer("p3", 2, chips: 11, cards: [])
        };
        var state     = MakeState(players, deck: [], faceUpCard: 5);
        var (next, _) = _module.Apply(state, Take());

        var score = next.Scores!.First(s => s.PlayerId == "p1");
        score.CardScore.Should().Be(27);
        score.Chips.Should().Be(3);
        score.Total.Should().Be(24);
    }

    [Fact]
    public void Scoring_StoredCardListNotMutated()
    {
        // Verify cards stored out of order are scored correctly and original list preserved
        var originalCards = new List<int> { 9, 7, 20, 8 }; // insertion order
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 3, cards: originalCards),
            MakePlayer("p2", 1, chips: 11, cards: []),
            MakePlayer("p3", 2, chips: 11, cards: [])
        };
        var state     = MakeState(players, deck: [], faceUpCard: 5);
        var (next, _) = _module.Apply(state, Take());

        // Score is same regardless of insertion order
        var score = next.Scores!.First(s => s.PlayerId == "p1");
        score.CardScore.Should().Be(27);

        // Original player cards in next state should remain insertion-order
        // (not re-sorted by scoring function)
        var p1Cards = next.Players[0].Cards;
        p1Cards.Should().ContainInOrder(9, 7, 20, 8, 5);
    }

    [Fact]
    public void Scoring_PlayerWithNoCards_NegativeScore()
    {
        // Player with no cards and 5 chips → cardScore=0, total=-5
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 5, cards: []),
            MakePlayer("p2", 1, chips: 11, cards: [20]),
            MakePlayer("p3", 2, chips: 11, cards: [30])
        };
        var state     = MakeState(players, deck: [], faceUpCard: 3);
        var (next, _) = _module.Apply(state, Take());

        var score = next.Scores!.First(s => s.PlayerId == "p1");
        score.CardScore.Should().Be(3); // took the faceUpCard 3
        score.Total.Should().Be(3 - 5);
    }

    [Fact]
    public void Scoring_SingleCard_NoChain()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 0, cards: [15]),
            MakePlayer("p2", 1, chips: 11, cards: []),
            MakePlayer("p3", 2, chips: 11, cards: [])
        };
        var state     = MakeState(players, deck: [], faceUpCard: 3);
        var (next, _) = _module.Apply(state, Take());

        var score = next.Scores!.First(s => s.PlayerId == "p1");
        // p1 had [15] + takes 3 → sorted [3, 15]
        // chains: {3}, {15} → cardScore = 3 + 15 = 18
        score.CardScore.Should().Be(18);
    }

    // ── AC-8 private chip projection ──────────────────────────────────────────

    private FThatView ProjectViaInterface(FThatState state, string playerId)
    {
        var doc = JsonDocument.Parse(JsonSerializer.Serialize(state,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var projected = ((IGameModule)_module).ProjectStateForPlayer(doc, playerId);
        projected.Should().NotBeNull();
        return JsonSerializer.Deserialize<FThatView>(
            projected!.RootElement.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    [Fact]
    public void ProjectForPlayer_SelfChips_ExactValue_NotHidden()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 7), MakePlayer("p2", 1, chips: 3), MakePlayer("p3", 2, chips: 11)
        };
        var state = MakeState(players, deck: [5, 6]);
        var view  = ProjectViaInterface(state, "p1");

        var selfView = view.Players.First(p => p.Id == "p1");
        selfView.Chips.Should().Be(7);
        selfView.ChipsHidden.Should().BeFalse();
    }

    [Fact]
    public void ProjectForPlayer_OpponentChips_AreMasked()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0, chips: 7), MakePlayer("p2", 1, chips: 3), MakePlayer("p3", 2, chips: 11)
        };
        var state = MakeState(players, deck: [5, 6]);
        var view  = ProjectViaInterface(state, "p1");

        var opponents = view.Players.Where(p => p.Id != "p1").ToList();
        opponents.Should().AllSatisfy(p =>
        {
            p.Chips.Should().Be(-1);
            p.ChipsHidden.Should().BeTrue();
        });
    }

    // ── AC-9 deck count visible ────────────────────────────────────────────────

    [Fact]
    public void ProjectForPlayer_DeckCountEqualsRemainingDeck()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var deck  = new List<int> { 5, 6, 7, 8, 9 };
        var state = MakeState(players, deck: deck);
        var view  = ProjectViaInterface(state, "p1");

        view.DeckCount.Should().Be(5);
    }

    [Fact]
    public void ProjectForPlayer_FaceUpCardAndChipsVisible()
    {
        var players = new List<FThatPlayer>
        {
            MakePlayer("p1", 0), MakePlayer("p2", 1), MakePlayer("p3", 2)
        };
        var state = MakeState(players, deck: [5, 6], faceUpCard: 13, chipsOnCard: 4);
        var view  = ProjectViaInterface(state, "p1");

        view.FaceUpCard.Should().Be(13);
        view.ChipsOnCard.Should().Be(4);
    }

    // ── Handle (round-trip via IGameHandler) ──────────────────────────────────

    [Fact]
    public void Handle_Pass_OnYourTurn_WithChips_NoRejection()
    {
        var state = _module.CreateInitialState(ThreePlayers(), null);
        var stateDoc  = JsonDocument.Parse(JsonSerializer.Serialize(state,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var actionDoc = JsonDocument.Parse(JsonSerializer.Serialize(Pass(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var ctx    = new GameContext(stateDoc, actionDoc, "p1", "room-1", 1);
        var result = _module.Handle(ctx);

        result.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void Handle_Pass_NotYourTurn_ReturnsRejection()
    {
        var state = _module.CreateInitialState(ThreePlayers(), null);
        var stateDoc  = JsonDocument.Parse(JsonSerializer.Serialize(state,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var actionDoc = JsonDocument.Parse(JsonSerializer.Serialize(Pass(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var ctx    = new GameContext(stateDoc, actionDoc, "p2", "room-1", 1);
        var result = _module.Handle(ctx);

        result.RejectionReason.Should().NotBeNull();
    }
}
