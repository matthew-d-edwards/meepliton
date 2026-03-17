using System.Text.Json;
using Meepliton.Contracts;
using Meepliton.Games.Skyline.Models;

namespace Meepliton.Games.Skyline;

/// <summary>
/// Skyline — the first Meepliton game module.
/// Players take turns placing numbered tiles on a shared grid, scoring points
/// for completing rows and columns. The player with the most points wins.
/// </summary>
public class SkylineModule : ReducerGameModule<SkylineState, SkylineAction, object>
{
    public override string GameId      => "skyline";
    public override string Name        => "Skyline";
    public override string Description => "Place tiles to build the city skyline. Complete rows and columns to score.";
    public override int    MinPlayers  => 2;
    public override int    MaxPlayers  => 4;
    public override bool   SupportsUndo => true;
    public override bool   HasStateProjection => true;

    private static readonly Random Rng = new();
    private const int BoardSize  = 5;
    private const int HandSize   = 3;
    private const int TileMax    = 9;

    public override SkylineState CreateInitialState(IReadOnlyList<PlayerInfo> players, object? options)
    {
        var playerStates = players.Select(p => new PlayerState(
            p.Id, p.DisplayName, p.AvatarUrl, p.SeatIndex,
            Score: 0,
            Hand: DrawTiles(HandSize)
        )).ToList();

        var board = Enumerable.Range(0, BoardSize)
            .Select(_ => Enumerable.Repeat<int?>(null, BoardSize).ToList())
            .ToList();

        return new SkylineState(
            Players:         playerStates,
            Board:           board,
            CurrentPlayerId: players[0].Id,
            Phase:           SkylinePhase.PlacingTile,
            Turn:            1,
            WinnerId:        null
        );
    }

    public override string? Validate(SkylineState state, SkylineAction action, string playerId)
    {
        if (state.Phase == SkylinePhase.GameOver)
            return "The game is over.";

        if (state.CurrentPlayerId != playerId)
            return "It is not your turn.";

        if (action.Type == "PlaceTile")
        {
            if (action.PlaceTile is null) return "Missing PlaceTile payload.";
            var (row, col, tileValue) = action.PlaceTile;
            if (row < 0 || row >= BoardSize || col < 0 || col >= BoardSize)
                return "Cell is out of bounds.";
            if (state.Board[row][col] is not null)
                return "Cell is already occupied.";
            var player = state.Players.First(p => p.Id == playerId);
            if (!player.Hand.Contains(tileValue))
                return "You do not have that tile in your hand.";
            return null;
        }

        if (action.Type == "Undo")
            return null; // handled by Apply

        return $"Unknown action type: {action.Type}";
    }

    public override SkylineState Apply(SkylineState state, SkylineAction action)
    {
        if (action.Type == "PlaceTile" && action.PlaceTile is not null)
        {
            var (row, col, tileValue) = action.PlaceTile;
            var players  = state.Players.ToList();
            var board    = state.Board.Select(r => r.ToList()).ToList();
            var playerIdx = players.FindIndex(p => p.Id == state.CurrentPlayerId);
            var player   = players[playerIdx];

            // Place tile
            board[row][col] = tileValue;

            // Remove tile from hand and draw a replacement
            var newHand = player.Hand.ToList();
            newHand.Remove(tileValue);
            newHand.AddRange(DrawTiles(1));

            // Score completed rows/columns
            var score = player.Score
                + ScoreRow(board, row)
                + ScoreCol(board, col);

            players[playerIdx] = player with { Hand = newHand, Score = score };

            // Advance turn
            var nextIdx = (playerIdx + 1) % players.Count;
            var nextPlayerId = players[nextIdx].Id;
            var turn = state.Turn + 1;

            // Check game over: board is full
            var boardFull = board.All(r => r.All(c => c is not null));
            if (boardFull)
            {
                var winner = players.OrderByDescending(p => p.Score).First();
                return state with
                {
                    Players = players,
                    Board   = board,
                    Phase   = SkylinePhase.GameOver,
                    WinnerId = winner.Id,
                    Turn    = turn,
                };
            }

            return state with
            {
                Players         = players,
                Board           = board,
                CurrentPlayerId = nextPlayerId,
                Turn            = turn,
            };
        }

        // Undo — not implemented in initial scaffold; return state unchanged
        return state;
    }

    // ── State projection (hide opponents' tile hands) ─────────────────────────

    protected override SkylineState ProjectForPlayer(SkylineState state, string playerId)
    {
        var players = state.Players
            .Select(p => p.Id == playerId
                ? p                      // full state for this player
                : p with { Hand = [] })  // hide hand from other players
            .ToList();
        return state with { Players = players };
    }

    // ── Scoring helpers ──────────────────────────────────────────────────────

    private static int ScoreRow(List<List<int?>> board, int row)
    {
        var cells = board[row];
        if (cells.Any(c => c is null)) return 0;
        return cells.Sum(c => c!.Value);
    }

    private static int ScoreCol(List<List<int?>> board, int col)
    {
        var cells = board.Select(r => r[col]).ToList();
        if (cells.Any(c => c is null)) return 0;
        return cells.Sum(c => c!.Value);
    }

    private static List<int> DrawTiles(int count) =>
        Enumerable.Range(0, count).Select(_ => Rng.Next(1, TileMax + 1)).ToList();
}
