using Meepliton.Contracts;
using Meepliton.Games.Skyline.Models;

namespace Meepliton.Games.Skyline;

/// <summary>
/// Skyline — an Acquire-style hotel merger game on a 9x12 grid.
/// 7 hotel chains, 2-6 players, 6 tiles per hand, 25 stock per hotel.
/// </summary>
public class SkylineModule : ReducerGameModule<SkylineState, SkylineAction, object>
{
    public override string GameId      => "skyline";
    public override string Name        => "Skyline";
    public override string Description => "Build hotel empires, trigger mergers, and outwit rivals on a 9x12 city grid.";
    public override int    MinPlayers  => 2;
    public override int    MaxPlayers  => 6;
    public override bool   SupportsUndo => false;

    // ── Constants ─────────────────────────────────────────────────────────────

    private static readonly string[] Hotels =
        ["luxor", "tower", "american", "festival", "worldwide", "continental", "imperial"];

    private static readonly char[] Rows = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I'];
    private const int ColCount = 12;
    private const int TilesPerHand = 6;
    private const int Safe = 11;
    private const int TotalStock = 25;
    private const int StartingCash = 6000;
    private const int EndGameChainSize = 41;

    private static readonly string[] PlayerColors =
        ["#f5c842", "#ff4f6d", "#3b8eff", "#2ecc71", "#c353f5", "#ff7c2a"];

    // Stock price tiers indexed [0..10]
    // tier: size<=0->0, <=2->1, <=3->2, <=4->3, <=5->4, <=6->5, <=10->6, <=20->7, <=30->8, <=40->9, else->10
    private static readonly int[] LuxorTowerPrices =
        [0, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100];

    private static readonly int[] AmericanFestivalWorldwidePrices =
        [0, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200];

    private static readonly int[] ContinentalImperialPrices =
        [0, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300];

    // ── Setup ─────────────────────────────────────────────────────────────────

    private static List<string> AllTiles()
    {
        var tiles = new List<string>(108);
        foreach (var row in Rows)
            for (var col = 1; col <= ColCount; col++)
                tiles.Add($"{row}{col}");
        return tiles;
    }

    public override SkylineState CreateInitialState(IReadOnlyList<PlayerInfo> players, object? options)
    {
        var bag = AllTiles();
        Shuffle(bag);

        var chains = Hotels.ToDictionary(
            h => h,
            _ => new ChainState(Active: false, Size: 0, Tiles: []));

        var stockBank = Hotels.ToDictionary(h => h, _ => TotalStock);

        var playerStates = new List<PlayerState>(players.Count);
        for (var i = 0; i < players.Count; i++)
        {
            var p    = players[i];
            var hand = bag.Take(TilesPerHand).ToList();
            bag.RemoveRange(0, TilesPerHand);
            var stocks = Hotels.ToDictionary(h => h, _ => 0);
            playerStates.Add(new PlayerState(
                Id:     p.Id,
                Name:   p.DisplayName,
                Color:  PlayerColors[i % PlayerColors.Length],
                Cash:   StartingCash,
                Stocks: stocks,
                Hand:   hand));
        }

        return new SkylineState(
            Players:     playerStates,
            CurrentPlayer: 0,
            Board:       new Dictionary<string, string>(),
            Chains:      chains,
            StockBank:   stockBank,
            Bag:         bag,
            Log:         [$"Game started. {playerStates[0].Name} goes first."],
            GameOver:    false,
            Winner:      null,
            RankedOrder: null,
            Phase:       "place",
            Pending:     null);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    public override string? Validate(SkylineState state, SkylineAction action, string playerId)
    {
        if (state.GameOver) return "The game is over.";

        if (action.Type == "Undo") return null;

        // Dispose phase: acting player is drawn from the queue, not the current player
        if (state.Phase == "dispose" && action.Type == "Dispose")
        {
            var queue = state.Pending?.DisposeQueue;
            var idx   = state.Pending?.DisposeIdx ?? 0;
            if (queue is null || idx >= queue.Count) return "No pending disposal.";
            if (state.Players[queue[idx].PlayerIdx].Id != playerId) return "It is not your turn to dispose.";
            return null;
        }

        // All other phases require the current player
        if (state.Players[state.CurrentPlayer].Id != playerId) return "It is not your turn.";

        return action.Type switch
        {
            "PlaceTile"       => ValidatePlaceTile(state, action),
            "FoundHotel"      => ValidateFoundHotel(state, action),
            "ChooseSurvivor"  => ValidateChooseSurvivor(state, action),
            "ConfirmSurvivor" => ValidateConfirmSurvivor(state),
            "Dispose"         => "It is not the dispose phase.",
            "BuyStocks"       => ValidateBuyStocks(state, action),
            "EndTurn"         => ValidateEndTurn(state),
            "EndGame"         => ValidateEndGame(state),
            _                 => $"Unknown action type: {action.Type}"
        };
    }

    private static string? ValidatePlaceTile(SkylineState state, SkylineAction action)
    {
        if (state.Phase != "place") return "Wrong phase -- expected 'place'.";
        if (string.IsNullOrEmpty(action.TileId)) return "Missing tileId.";
        if (!state.Players[state.CurrentPlayer].Hand.Contains(action.TileId))
            return "You do not have that tile.";
        var result = Analyze(action.TileId, state);
        if (result.Kind == "illegal")
            return "That tile cannot be placed (would merge two safe chains, or all hotels are already active).";
        return null;
    }

    private static string? ValidateFoundHotel(SkylineState state, SkylineAction action)
    {
        if (state.Phase != "found") return "Wrong phase -- expected 'found'.";
        if (string.IsNullOrEmpty(action.Hotel)) return "Missing hotel name.";
        if (!Hotels.Contains(action.Hotel)) return "Unknown hotel.";
        if (state.Chains[action.Hotel].Active) return "That hotel is already active.";
        return null;
    }

    private static string? ValidateChooseSurvivor(SkylineState state, SkylineAction action)
    {
        if (state.Phase != "merge") return "Wrong phase -- expected 'merge'.";
        if (state.Pending?.SurvivorChosen == true) return "Survivor already chosen -- send ConfirmSurvivor.";
        if (string.IsNullOrEmpty(action.Hotel)) return "Missing hotel name.";
        var survivors = state.Pending?.Survivors;
        if (survivors is null || !survivors.Contains(action.Hotel))
            return "That hotel is not a valid survivor choice.";
        return null;
    }

    private static string? ValidateConfirmSurvivor(SkylineState state)
    {
        if (state.Phase != "merge") return "Wrong phase -- expected 'merge'.";
        if (string.IsNullOrEmpty(state.Pending?.Survivor))
            return "No survivor chosen yet -- send ChooseSurvivor first.";
        return null;
    }

    private static string? ValidateBuyStocks(SkylineState state, SkylineAction action)
    {
        if (state.Phase != "buy") return "Wrong phase -- expected 'buy'.";
        var purchases = action.Purchases ?? new Dictionary<string, int>();
        if (purchases.Values.Sum() > 3) return "You may buy at most 3 stocks per turn.";

        var player = state.Players[state.CurrentPlayer];
        var cost   = 0;
        foreach (var (hotel, qty) in purchases)
        {
            if (qty <= 0) continue;
            if (!Hotels.Contains(hotel)) return $"Unknown hotel: {hotel}.";
            if (!state.Chains[hotel].Active) return $"{hotel} is not an active chain.";
            if (state.StockBank[hotel] < qty) return $"Not enough {hotel} stock in the bank.";
            cost += StockPrice(hotel, state.Chains[hotel].Size) * qty;
        }
        if (player.Cash < cost) return "Insufficient cash.";
        return null;
    }

    private static string? ValidateEndTurn(SkylineState state)
    {
        if (state.Phase != "draw" && state.Phase != "buy")
            return "Wrong phase -- expected 'draw' or 'buy'.";
        return null;
    }

    private static string? ValidateEndGame(SkylineState state)
    {
        if (!CanEndGame(state)) return "End-game conditions not met.";
        return null;
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    public override SkylineState Apply(SkylineState state, SkylineAction action) =>
        action.Type switch
        {
            "PlaceTile"       => ApplyPlaceTile(state, action),
            "FoundHotel"      => ApplyFoundHotel(state, action),
            "ChooseSurvivor"  => ApplyChooseSurvivor(state, action),
            "ConfirmSurvivor" => ApplyConfirmSurvivor(state),
            "Dispose"         => ApplyDispose(state, action),
            "BuyStocks"       => ApplyBuyStocks(state, action),
            "EndTurn"         => ApplyEndTurn(state),
            "EndGame"         => ApplyEndGame(state),
            _                 => state
        };

    // ── PlaceTile ─────────────────────────────────────────────────────────────

    private static SkylineState ApplyPlaceTile(SkylineState state, SkylineAction action)
    {
        var tid      = action.TileId!;
        var players  = ClonePlayers(state.Players);
        var board    = new Dictionary<string, string>(state.Board);
        var chains   = CloneChains(state.Chains);
        var stockBank = new Dictionary<string, int>(state.StockBank);
        var log      = new List<string>(state.Log);

        // Remove tile from current player's hand
        var hand = players[state.CurrentPlayer].Hand.ToList();
        hand.Remove(tid);
        players[state.CurrentPlayer] = players[state.CurrentPlayer] with { Hand = hand };

        var analysis = Analyze(tid, state);

        switch (analysis.Kind)
        {
            case "isolated":
            {
                board[tid] = "neutral";
                log.Add($"{players[state.CurrentPlayer].Name} placed {tid} (isolated).");
                return state with
                {
                    Players = players, Board = board, Chains = chains,
                    StockBank = stockBank, Log = log,
                    Phase = "buy", Pending = null
                };
            }

            case "extend":
            {
                var hotel = analysis.Hotels![0];
                board[tid] = hotel;
                FloodNeutrals(tid, hotel, board);
                RebuildChain(hotel, board, chains);
                log.Add($"{players[state.CurrentPlayer].Name} placed {tid}, extending {hotel} to {chains[hotel].Size} tiles.");
                return state with
                {
                    Players = players, Board = board, Chains = chains,
                    StockBank = stockBank, Log = log,
                    Phase = "buy", Pending = null
                };
            }

            case "found":
            {
                board[tid] = "neutral";
                var pendingTiles = FindConnectedNeutrals(tid, board);
                log.Add($"{players[state.CurrentPlayer].Name} placed {tid} -- choose a hotel to found.");
                return state with
                {
                    Players = players, Board = board, Chains = chains,
                    StockBank = stockBank, Log = log,
                    Phase = "found",
                    Pending = new PendingState(
                        Type:           "found",
                        Tiles:          pendingTiles,
                        Chosen:         null,
                        Tid:            tid,
                        Hotels:         null,
                        Survivors:      null,
                        Survivor:       null,
                        Defunct:        null,
                        SurvivorChosen: null,
                        DefunctSizes:   null,
                        DisposeQueue:   null,
                        DisposeIdx:     null,
                        DisposeDecisions: null)
                };
            }

            case "merge":
            {
                board[tid] = "neutral";
                var mergeHotels = analysis.Hotels!;
                var survivors   = analysis.Survivors!;
                var defunct     = analysis.Defunct!;
                var defunctSizes = defunct.ToDictionary(h => h, h => chains[h].Size);

                log.Add($"{players[state.CurrentPlayer].Name} placed {tid}, triggering a merger: {string.Join(" + ", mergeHotels)}.");

                var nextState = state with
                {
                    Players = players, Board = board, Chains = chains,
                    StockBank = stockBank, Log = log,
                    Phase = "merge",
                    Pending = new PendingState(
                        Type:           "merge",
                        Tiles:          null,
                        Chosen:         null,
                        Tid:            tid,
                        Hotels:         mergeHotels,
                        Survivors:      survivors,
                        Survivor:       survivors.Count == 1 ? survivors[0] : null,
                        Defunct:        defunct,
                        SurvivorChosen: survivors.Count == 1,
                        DefunctSizes:   defunctSizes,
                        DisposeQueue:   null,
                        DisposeIdx:     null,
                        DisposeDecisions: null)
                };

                // If survivor is unambiguous, proceed directly to processing
                return survivors.Count == 1 ? ProcessMerger(nextState) : nextState;
            }

            default:
                return state;
        }
    }

    // ── FoundHotel ────────────────────────────────────────────────────────────

    private static SkylineState ApplyFoundHotel(SkylineState state, SkylineAction action)
    {
        var hotel   = action.Hotel!;
        var pending = state.Pending!;
        var tiles   = pending.Tiles!;

        var players   = ClonePlayers(state.Players);
        var board     = new Dictionary<string, string>(state.Board);
        var chains    = CloneChains(state.Chains);
        var stockBank = new Dictionary<string, int>(state.StockBank);
        var log       = new List<string>(state.Log);

        foreach (var t in tiles)
            board[t] = hotel;

        RebuildChain(hotel, board, chains);

        if (stockBank[hotel] > 0)
        {
            stockBank[hotel]--;
            var p      = players[state.CurrentPlayer];
            var stocks = new Dictionary<string, int>(p.Stocks) { [hotel] = p.Stocks[hotel] + 1 };
            players[state.CurrentPlayer] = p with { Stocks = stocks };
            log.Add($"{players[state.CurrentPlayer].Name} founded {hotel} ({chains[hotel].Size} tiles) and received 1 free share.");
        }
        else
        {
            log.Add($"{players[state.CurrentPlayer].Name} founded {hotel} ({chains[hotel].Size} tiles).");
        }

        return state with
        {
            Players = players, Board = board, Chains = chains,
            StockBank = stockBank, Log = log,
            Phase = "buy", Pending = null
        };
    }

    // ── ChooseSurvivor ────────────────────────────────────────────────────────

    private static SkylineState ApplyChooseSurvivor(SkylineState state, SkylineAction action)
    {
        var log = new List<string>(state.Log)
        {
            $"{state.Players[state.CurrentPlayer].Name} chose {action.Hotel} as the surviving chain."
        };
        return state with { Pending = state.Pending! with { Survivor = action.Hotel }, Log = log };
    }

    // ── ConfirmSurvivor ───────────────────────────────────────────────────────

    private static SkylineState ApplyConfirmSurvivor(SkylineState state)
    {
        var pending = state.Pending! with { SurvivorChosen = true };
        return ProcessMerger(state with { Pending = pending });
    }

    // ── ProcessMerger (internal) ──────────────────────────────────────────────

    private static SkylineState ProcessMerger(SkylineState state)
    {
        var pending      = state.Pending!;
        var survivor     = pending.Survivor!;
        var defunct      = pending.Defunct!;
        var tid          = pending.Tid!;
        var defunctSizes = pending.DefunctSizes!;

        var players   = ClonePlayers(state.Players);
        var board     = new Dictionary<string, string>(state.Board);
        var chains    = CloneChains(state.Chains);
        var stockBank = new Dictionary<string, int>(state.StockBank);
        var log       = new List<string>(state.Log);

        // Pay merger bonuses for each defunct chain before dissolving
        foreach (var dh in defunct)
        {
            var price = StockPrice(dh, defunctSizes[dh]);
            PayMergerBonuses(dh, price, players, log);
        }

        // Dissolve defunct chains into survivor
        foreach (var dh in defunct)
        {
            var defunctTiles = chains[dh].Tiles.ToList();
            foreach (var t in defunctTiles)
                board[t] = survivor;
            chains[dh] = chains[dh] with { Active = false, Size = 0, Tiles = [] };
            log.Add($"{dh} is dissolved into {survivor}.");
        }

        // Place the trigger tile and flood connected neutrals
        board[tid] = survivor;
        FloodNeutrals(tid, survivor, board);
        RebuildChain(survivor, board, chains);
        log.Add($"{survivor} now controls {chains[survivor].Size} tiles.");

        // Build disposal queue: for each defunct, players clockwise from current who hold stock
        var queue = BuildDisposeQueue(defunct, players, state.CurrentPlayer);

        if (queue.Count == 0)
        {
            return state with
            {
                Players = players, Board = board, Chains = chains,
                StockBank = stockBank, Log = log,
                Phase = "buy", Pending = null
            };
        }

        return state with
        {
            Players = players, Board = board, Chains = chains,
            StockBank = stockBank, Log = log,
            Phase = "dispose",
            Pending = new PendingState(
                Type:           "merge",
                Tiles:          null,
                Chosen:         null,
                Tid:            tid,
                Hotels:         pending.Hotels,
                Survivors:      pending.Survivors,
                Survivor:       survivor,
                Defunct:        defunct,
                SurvivorChosen: true,
                DefunctSizes:   defunctSizes,
                DisposeQueue:   queue,
                DisposeIdx:     0,
                DisposeDecisions: new Dictionary<string, DisposeDecision>())
        };
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    private static SkylineState ApplyDispose(SkylineState state, SkylineAction action)
    {
        var pending    = state.Pending!;
        var queue      = pending.DisposeQueue!;
        var idx        = pending.DisposeIdx ?? 0;
        var item       = queue[idx];
        var survivor   = pending.Survivor!;
        var defunct    = item.Defunct;
        var playerIdx  = item.PlayerIdx;

        var sell  = action.Sell;
        var trade = action.Trade;

        var players   = ClonePlayers(state.Players);
        var stockBank = new Dictionary<string, int>(state.StockBank);
        var log       = new List<string>(state.Log);

        var p         = players[playerIdx];
        var heldStock = p.Stocks[defunct];
        var keep      = heldStock - sell - trade;
        var price     = StockPrice(defunct, pending.DefunctSizes![defunct]);
        var cashGain  = sell * price;
        var tradeGet  = trade / 2;

        var newStocks = new Dictionary<string, int>(p.Stocks)
        {
            [defunct]  = keep,
            [survivor] = p.Stocks[survivor] + tradeGet
        };

        players[playerIdx] = p with { Cash = p.Cash + cashGain, Stocks = newStocks };

        // Return sold + traded defunct stock to bank; deduct survivor stock given out
        stockBank[defunct]  += sell + trade;
        stockBank[survivor] -= tradeGet;

        var parts = new List<string>();
        if (sell  > 0) parts.Add($"sold {sell}");
        if (trade > 0) parts.Add($"traded {trade} for {tradeGet} {survivor}");
        if (keep  > 0) parts.Add($"kept {keep}");
        if (parts.Count == 0) parts.Add("passed");
        log.Add($"{p.Name} disposed {defunct} stock: {string.Join(", ", parts)}.");

        var nextIdx = idx + 1;

        if (nextIdx >= queue.Count)
        {
            return state with
            {
                Players = players, StockBank = stockBank, Log = log,
                Phase = "buy", Pending = null
            };
        }

        return state with
        {
            Players = players, StockBank = stockBank, Log = log,
            Phase = "dispose", Pending = pending with { DisposeIdx = nextIdx }
        };
    }

    // ── BuyStocks ─────────────────────────────────────────────────────────────

    private static SkylineState ApplyBuyStocks(SkylineState state, SkylineAction action)
    {
        var purchases = action.Purchases ?? new Dictionary<string, int>();
        var players   = ClonePlayers(state.Players);
        var stockBank = new Dictionary<string, int>(state.StockBank);
        var log       = new List<string>(state.Log);

        var p        = players[state.CurrentPlayer];
        var newCash  = p.Cash;
        var newStocks = new Dictionary<string, int>(p.Stocks);

        foreach (var (hotel, qty) in purchases)
        {
            if (qty <= 0) continue;
            var price  = StockPrice(hotel, state.Chains[hotel].Size);
            newCash   -= price * qty;
            newStocks[hotel] += qty;
            stockBank[hotel] -= qty;
            log.Add($"{p.Name} bought {qty} {hotel} share(s) at ${price:N0} each.");
        }

        players[state.CurrentPlayer] = p with { Cash = newCash, Stocks = newStocks };

        return state with
        {
            Players = players, StockBank = stockBank, Log = log,
            Phase = "draw", Pending = null
        };
    }

    // ── EndTurn ───────────────────────────────────────────────────────────────

    private static SkylineState ApplyEndTurn(SkylineState state)
    {
        // Allow skipping buy
        if (state.Phase == "buy")
            state = state with { Phase = "draw" };

        var players = ClonePlayers(state.Players);
        var bag     = state.Bag.ToList();
        var log     = new List<string>(state.Log);

        var p    = players[state.CurrentPlayer];
        var hand = p.Hand.ToList();
        while (hand.Count < TilesPerHand && bag.Count > 0)
        {
            hand.Add(bag[0]);
            bag.RemoveRange(0, 1);
        }
        players[state.CurrentPlayer] = p with { Hand = hand };
        log.Add($"{p.Name} ends their turn.");

        var stateWithDraw = state with { Players = players, Bag = bag, Log = log };
        if (ShouldEndGame(stateWithDraw))
            return FinaliseGame(stateWithDraw);

        var nextPlayer = (state.CurrentPlayer + 1) % players.Count;
        log.Add($"{players[nextPlayer].Name}'s turn.");

        return stateWithDraw with
        {
            CurrentPlayer = nextPlayer,
            Phase = "place", Pending = null
        };
    }

    // ── EndGame ───────────────────────────────────────────────────────────────

    private static SkylineState ApplyEndGame(SkylineState state)
    {
        var log = new List<string>(state.Log) { "A player has called game end." };
        return FinaliseGame(state with { Log = log });
    }

    // ── End-game helpers ──────────────────────────────────────────────────────

    private static bool CanEndGame(SkylineState state)
    {
        var active = state.Chains.Values.Where(c => c.Active).ToList();
        if (active.Count == 0) return false;
        if (active.Any(c => c.Size >= EndGameChainSize)) return true;
        return active.All(c => c.Size >= Safe);
    }

    private static bool ShouldEndGame(SkylineState state) => CanEndGame(state);

    private static SkylineState FinaliseGame(SkylineState state)
    {
        var players   = ClonePlayers(state.Players);
        var stockBank = new Dictionary<string, int>(state.StockBank);
        var log       = new List<string>(state.Log);

        // Pay final bonuses for all active chains
        foreach (var hotel in Hotels)
        {
            var chain = state.Chains[hotel];
            if (!chain.Active) continue;
            var price = StockPrice(hotel, chain.Size);
            PayMergerBonuses(hotel, price, players, log);
        }

        // Sell all remaining stock at market price
        for (var i = 0; i < players.Count; i++)
        {
            var p        = players[i];
            var newCash  = p.Cash;
            var newStocks = new Dictionary<string, int>(p.Stocks);

            foreach (var hotel in Hotels)
            {
                var qty = p.Stocks[hotel];
                if (qty <= 0) continue;
                var price  = StockPrice(hotel, state.Chains[hotel].Size);
                newCash   += price * qty;
                stockBank[hotel] += qty;
                newStocks[hotel] = 0;
            }

            players[i] = p with { Cash = newCash, Stocks = newStocks };
        }

        var ranked = players
            .Select((p, i) => (Index: i, p.Cash))
            .OrderByDescending(x => x.Cash)
            .ToList();

        var rankedOrder = ranked.Select(x => x.Index).ToList();
        var winner      = players[ranked[0].Index].Name;

        log.Add($"Game over! {winner} wins with ${players[ranked[0].Index].Cash:N0}.");

        return state with
        {
            Players     = players,
            StockBank   = stockBank,
            Log         = log,
            GameOver    = true,
            Winner      = winner,
            RankedOrder = rankedOrder,
            Phase       = "draw",
            Pending     = null
        };
    }

    // ── Tile analysis ─────────────────────────────────────────────────────────

    private sealed record AnalyzeResult(
        string Kind,
        List<string>? Hotels    = null,
        List<string>? Survivors = null,
        List<string>? Defunct   = null);

    private static AnalyzeResult Analyze(string tid, SkylineState state)
    {
        var neighbors = GetNeighbors(tid)
            .Where(n => state.Board.ContainsKey(n))
            .ToList();

        if (neighbors.Count == 0) return new AnalyzeResult("isolated");

        var hotelNeighbors = neighbors
            .Where(n => state.Board[n] != "neutral")
            .Select(n => state.Board[n])
            .Distinct()
            .ToList();

        if (hotelNeighbors.Count == 0)
        {
            // Adjacent only to neutral tiles -- founding a new chain
            var activeCount = state.Chains.Values.Count(c => c.Active);
            if (activeCount >= Hotels.Length) return new AnalyzeResult("illegal");
            return new AnalyzeResult("found");
        }

        if (hotelNeighbors.Count == 1)
            return new AnalyzeResult("extend", Hotels: hotelNeighbors);

        // Multiple hotel neighbors -- merger
        var safeCount = hotelNeighbors.Count(h => state.Chains[h].Size >= Safe);
        if (safeCount >= 2) return new AnalyzeResult("illegal");

        var maxSize   = hotelNeighbors.Max(h => state.Chains[h].Size);
        var survivors = hotelNeighbors.Where(h => state.Chains[h].Size == maxSize).ToList();
        var defunct   = hotelNeighbors.Where(h => state.Chains[h].Size < maxSize).ToList();

        return new AnalyzeResult("merge", Hotels: hotelNeighbors, Survivors: survivors, Defunct: defunct);
    }

    // ── Neighbor helpers ──────────────────────────────────────────────────────

    private static IEnumerable<string> GetNeighbors(string tid)
    {
        var row    = tid[0];
        var col    = int.Parse(tid[1..]);
        var rowIdx = Array.IndexOf(Rows, row);

        if (rowIdx > 0)               yield return $"{Rows[rowIdx - 1]}{col}";
        if (rowIdx < Rows.Length - 1) yield return $"{Rows[rowIdx + 1]}{col}";
        if (col > 1)                  yield return $"{row}{col - 1}";
        if (col < ColCount)           yield return $"{row}{col + 1}";
    }

    private static void FloodNeutrals(string origin, string hotel, Dictionary<string, string> board)
    {
        var visited = new HashSet<string>();
        var stack   = new Stack<string>();
        stack.Push(origin);

        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (!visited.Add(cur)) continue;
            if (!board.TryGetValue(cur, out var val)) continue;
            if (val != "neutral" && val != hotel) continue;

            board[cur] = hotel;

            foreach (var nb in GetNeighbors(cur))
            {
                if (!visited.Contains(nb)
                    && board.TryGetValue(nb, out var nbVal)
                    && nbVal == "neutral")
                {
                    stack.Push(nb);
                }
            }
        }
    }

    private static List<string> FindConnectedNeutrals(string origin, Dictionary<string, string> board)
    {
        var visited = new HashSet<string>();
        var result  = new List<string>();
        var stack   = new Stack<string>();
        stack.Push(origin);

        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (!visited.Add(cur)) continue;
            if (!board.TryGetValue(cur, out var val) || val != "neutral") continue;

            result.Add(cur);

            foreach (var nb in GetNeighbors(cur))
            {
                if (!visited.Contains(nb)
                    && board.TryGetValue(nb, out var nbVal)
                    && nbVal == "neutral")
                {
                    stack.Push(nb);
                }
            }
        }

        return result;
    }

    private static void RebuildChain(string hotel, Dictionary<string, string> board, Dictionary<string, ChainState> chains)
    {
        var tiles = board.Where(kv => kv.Value == hotel).Select(kv => kv.Key).ToList();
        chains[hotel] = chains[hotel] with { Active = tiles.Count > 0, Size = tiles.Count, Tiles = tiles };
    }

    // ── Bonus helpers ─────────────────────────────────────────────────────────

    private static void PayMergerBonuses(string hotel, int price, List<PlayerState> players, List<string> log)
    {
        var holdings = players
            .Select((p, i) => (Idx: i, Count: p.Stocks[hotel]))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .ToList();

        if (holdings.Count == 0) return;

        var majorityPot = price * 10;
        var minorityPot = price * 5;

        if (holdings.Count == 1)
        {
            var bonus = SplitBonus(majorityPot + minorityPot, 1);
            GiveBonus(players, holdings[0].Idx, bonus, hotel, "sole", log);
            return;
        }

        var topCount = holdings[0].Count;
        var majority = holdings.Where(x => x.Count == topCount).ToList();

        if (majority.Count > 1)
        {
            // Tie for majority: split both pots, no separate minority payout
            var split = SplitBonus(majorityPot + minorityPot, majority.Count);
            foreach (var m in majority)
                GiveBonus(players, m.Idx, split, hotel, "majority (tied)", log);
            return;
        }

        // Single majority holder
        GiveBonus(players, holdings[0].Idx, SplitBonus(majorityPot, 1), hotel, "majority", log);

        // Minority among remaining holders
        var secondCount = holdings.Skip(1).Max(x => x.Count);
        var minority    = holdings.Skip(1).Where(x => x.Count == secondCount).ToList();
        var minSplit    = SplitBonus(minorityPot, minority.Count);
        foreach (var m in minority)
            GiveBonus(players, m.Idx, minSplit, hotel, "minority", log);
    }

    private static void GiveBonus(
        List<PlayerState> players, int idx, int amount,
        string hotel, string label, List<string> log)
    {
        players[idx] = players[idx] with { Cash = players[idx].Cash + amount };
        log.Add($"{players[idx].Name} receives ${amount:N0} {label} bonus for {hotel}.");
    }

    /// <summary>
    /// Rounds total to nearest $100, then splits evenly, each share rounded down to $100.
    /// </summary>
    private static int SplitBonus(int total, int n)
    {
        var pot = (int)Math.Round((double)total / 100) * 100;
        return (int)Math.Floor((double)pot / n / 100) * 100;
    }

    // ── Disposal queue ────────────────────────────────────────────────────────

    private static List<DisposeQueueItem> BuildDisposeQueue(
        List<string> defunct, List<PlayerState> players, int currentPlayer)
    {
        var queue = new List<DisposeQueueItem>();
        var n     = players.Count;

        foreach (var dh in defunct)
        {
            for (var offset = 0; offset < n; offset++)
            {
                var pIdx = (currentPlayer + offset) % n;
                if (players[pIdx].Stocks[dh] > 0)
                    queue.Add(new DisposeQueueItem(dh, pIdx));
            }
        }

        return queue;
    }

    // ── Stock pricing ─────────────────────────────────────────────────────────

    private static int StockPrice(string hotel, int size)
    {
        var tier = SizeTier(size);
        return hotel switch
        {
            "luxor" or "tower"                      => LuxorTowerPrices[tier],
            "american" or "festival" or "worldwide" => AmericanFestivalWorldwidePrices[tier],
            "continental" or "imperial"             => ContinentalImperialPrices[tier],
            _                                       => 0
        };
    }

    private static int SizeTier(int size) => size switch
    {
        <= 0  => 0,
        <= 2  => 1,
        <= 3  => 2,
        <= 4  => 3,
        <= 5  => 4,
        <= 6  => 5,
        <= 10 => 6,
        <= 20 => 7,
        <= 30 => 8,
        <= 40 => 9,
        _     => 10
    };

    // ── Clone helpers (immutable pattern) ────────────────────────────────────

    private static List<PlayerState> ClonePlayers(List<PlayerState> players) =>
        players.Select(p => p with
        {
            Stocks = new Dictionary<string, int>(p.Stocks),
            Hand   = p.Hand.ToList()
        }).ToList();

    private static Dictionary<string, ChainState> CloneChains(Dictionary<string, ChainState> chains) =>
        chains.ToDictionary(
            kv => kv.Key,
            kv => kv.Value with { Tiles = kv.Value.Tiles.ToList() });

    // ── Utility ───────────────────────────────────────────────────────────────

    private static readonly Random Rng = new();

    private static void Shuffle<T>(List<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
