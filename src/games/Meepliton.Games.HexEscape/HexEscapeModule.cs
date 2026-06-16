using System.Text.Json;
using System.Text.Json.Serialization;
using Meepliton.Contracts;
using Meepliton.Games.HexEscape.Models;
using Microsoft.Extensions.Logging;

namespace Meepliton.Games.HexEscape;

/// <summary>
/// Hex Escape — cooperative tile-placement game for 1–6 players.
/// Players connect survivors to the exit before the zombie threat overruns them.
/// Implements IGameModule and IGameHandler directly (per AD-1 — ReducerGameModule
/// cannot emit GameOverEffect; only IGameHandler.Handle can).
/// </summary>
public class HexEscapeModule : IGameModule, IGameHandler
{
    private readonly ILogger<HexEscapeModule>? _logger;

    public HexEscapeModule(ILogger<HexEscapeModule>? logger = null)
    {
        _logger = logger;
    }

    // ── IGameModule metadata ──────────────────────────────────────────────────

    public string  GameId        => "hexescape";
    public string  Name          => "Hex Escape";
    public string  Description   => "Cooperate to connect survivors to the exit before the zombie horde arrives.";
    public int     MinPlayers    => 1;
    public int     MaxPlayers    => 6;
    public bool    AllowLateJoin => false;
    public bool    SupportsAsync => false;
    public bool    SupportsUndo  => false;
    public string? ThumbnailUrl  => null;
    public bool    HasStateProjection => false;

    // ── Axial hex geometry ────────────────────────────────────────────────────

    // Six direction offsets: index → (Δq, Δr)
    private static readonly (int Dq, int Dr)[] Directions =
    [
        (+1,  0),  // 0 = E
        (+1, -1),  // 1 = NE
        ( 0, -1),  // 2 = N
        (-1,  0),  // 3 = W
        (-1, +1),  // 4 = SW
        ( 0, +1),  // 5 = S
    ];

    // Base open edges per tile type (before rotation)
    private static readonly Dictionary<HexTileType, int[]> BaseEdges = new()
    {
        { HexTileType.Straight, [0, 3] },
        { HexTileType.Elbow,    [0, 1] },
        { HexTileType.Tee,      [0, 1, 2] },
        { HexTileType.Cross,    [0, 1, 2, 3] },
        { HexTileType.Deadend,  [0] },
    };

    // Compute open edges for a tile type at given rotation
    private static HashSet<int> OpenEdges(HexTileType type, int rotation) =>
        [..BaseEdges[type].Select(e => (e + rotation) % 6)];

    private static string CoordKey(int q, int r) => $"{q},{r}";

    private static (int Q, int R) ParseCoord(string key)
    {
        var parts = key.Split(',');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    // ── BFS connectivity check ────────────────────────────────────────────────

    /// <summary>
    /// BFS from the exit cell outward over open shared edges.
    /// Returns the number of survivor start cells reachable from the exit.
    /// Per AD-3 and the spec: runs from exitCell outward; dead edges at grid
    /// boundary are not errors.
    /// </summary>
    private static int ComputeConnectedSurvivors(
        HexEscapeState state)
    {
        var grid     = state.Grid;
        var cellSet  = new HashSet<string>(state.Cells);
        var exitCell = state.ExitCell;

        if (!grid.ContainsKey(exitCell)) return 0;

        var visited = new HashSet<string>();
        var queue   = new Queue<string>();
        queue.Enqueue(exitCell);
        visited.Add(exitCell);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var (cq, cr) = ParseCoord(current);
            var currentTile = grid[current];
            var currentEdges = OpenEdges(currentTile.TileType, currentTile.Rotation);

            for (int dir = 0; dir < 6; dir++)
            {
                if (!currentEdges.Contains(dir)) continue;

                var (dq, dr) = Directions[dir];
                var neighbour = CoordKey(cq + dq, cr + dr);

                if (!cellSet.Contains(neighbour)) continue;   // off-board dead end
                if (visited.Contains(neighbour))  continue;
                if (!grid.ContainsKey(neighbour)) continue;   // empty cell — not traversable

                // Check connection rule: neighbour must have open edge in opposite direction
                var neighbourTile  = grid[neighbour];
                var neighbourEdges = OpenEdges(neighbourTile.TileType, neighbourTile.Rotation);
                int opposite       = (dir + 3) % 6;
                if (!neighbourEdges.Contains(opposite)) continue;

                visited.Add(neighbour);
                queue.Enqueue(neighbour);
            }
        }

        return state.SurvivorStartCells.Count(s => visited.Contains(s));
    }

    // ── IGameModule.CreateInitialState ────────────────────────────────────────

    JsonDocument IGameModule.CreateInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options)
    {
        var state = BuildInitialState(players, options);
        return Serialize(state);
    }

    private HexEscapeState BuildInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options)
    {
        // Resolve level ID from options (AD-10: fall back to tutorial-01 on null/malformed/unknown)
        string? requestedId = null;
        if (options is not null)
        {
            try
            {
                var parsed = Deserialize<HexEscapeOptions>(options);
                requestedId = parsed?.LevelId;
            }
            catch
            {
                requestedId = null;
            }
        }

        HexEscapeLevel level;
        if (requestedId is not null && HexEscapeLevels.All.TryGetValue(requestedId, out var found))
        {
            level = found;
        }
        else
        {
            // AD-10: emit WARNING for null, malformed, or unknown level
            var idForLog = requestedId ?? "<null>";
            _logger?.LogWarning(
                "HexEscape: options missing/unknown level '{LevelId}', falling back to tutorial-01",
                idForLog);
            level = HexEscapeLevels.Tutorial01;
        }

        // AC-10: reject levels with 0 survivors
        if (level.SurvivorStartCells.Count == 0)
            throw new ArgumentException($"Level '{level.Id}' has no survivor start cells.");

        // Build initial grid from pre-placed tiles
        var grid = new Dictionary<string, HexCell>();
        foreach (var tile in level.PrePlacedTiles)
        {
            grid[tile.Coord] = new HexCell(tile.TileType, tile.Rotation, Fixed: true);
        }

        // Build player slots (AC-1: seat index assigned in join order)
        var playerSlots = players
            .Select(p => new PlayerSlot(p.Id, p.DisplayName, p.AvatarUrl, p.SeatIndex))
            .ToList();

        // Copy hand counts
        var handCounts = new Dictionary<HexTileType, int>(level.TileHandCounts);

        // AC-1: CreateInitialState does NOT evaluate win condition
        return new HexEscapeState(
            Phase:               HexEscapePhase.Playing,
            Outcome:             null,
            LevelId:             level.Id,
            LevelName:           level.Name,
            Cells:               [..level.Cells],
            Walls:               [..level.Walls],
            Grid:                grid,
            SurvivorStartCells:  [..level.SurvivorStartCells],
            ExitCell:            level.ExitCell,
            HandCounts:          handCounts,
            ThreatCounter:       0,
            ThreatThreshold:     level.ThreatThreshold,
            ConnectedSurvivors:  0,  // not evaluated at init
            TotalSurvivors:      level.SurvivorStartCells.Count,
            SeatsActedThisRound: [],
            Players:             playerSlots
        );
    }

    // ── IGameHandler.Handle ───────────────────────────────────────────────────

    public GameResult Handle(GameContext ctx)
    {
        var state  = Deserialize<HexEscapeState>(ctx.CurrentState);
        var action = Deserialize<HexEscapeAction>(ctx.Action);

        // Reject all actions when game is over
        if (state.Phase == HexEscapePhase.GameOver)
            return Reject(ctx, "The game is over.");

        // AC-7: turn order — free-order model, reject if seat already acted this round
        var actingPlayer = state.Players.FirstOrDefault(p => p.Id == ctx.PlayerId);
        if (actingPlayer is null)
            return Reject(ctx, "Player not found in this game.");

        if (state.SeatsActedThisRound.Contains(actingPlayer.SeatIndex))
            return Reject(ctx, "It is not your turn.");

        return action.Type switch
        {
            HexActionType.PlaceTile  => HandlePlaceTile(ctx, state, action, actingPlayer),
            HexActionType.RotateTile => HandleRotateTile(ctx, state, action, actingPlayer),
            HexActionType.Pass       => HandlePass(ctx, state, actingPlayer),
            _                        => Reject(ctx, "Unknown action type.")
        };
    }

    // ── PlaceTile ─────────────────────────────────────────────────────────────

    private GameResult HandlePlaceTile(GameContext ctx, HexEscapeState state, HexEscapeAction action, PlayerSlot actor)
    {
        var coord     = action.Coord;
        var tileType  = action.TileType;
        var rotation  = action.Rotation;

        // AC-12: rotation must be 0–5
        if (rotation is null or < 0 or > 5)
            return Reject(ctx, "Invalid rotation.");

        if (coord is null || tileType is null)
            return Reject(ctx, "Invalid action.");

        // AC-13: coord must be in the level's cell list
        if (!state.Cells.Contains(coord))
            return Reject(ctx, "Cell is not on the board.");

        // Walls are on the board but impassable — treat as occupied for placement purposes
        if (state.Walls.Contains(coord))
            return Reject(ctx, "Cell is already occupied.");

        // AC-14: cannot place on an occupied cell
        if (state.Grid.ContainsKey(coord))
            return Reject(ctx, "Cell is already occupied.");

        // AC-11: hand must have at least 1 tile of this type
        if (!state.HandCounts.TryGetValue(tileType.Value, out int count) || count <= 0)
            return Reject(ctx, "No tiles of that type remaining.");

        // Apply placement
        var newGrid = new Dictionary<string, HexCell>(state.Grid)
        {
            [coord] = new HexCell(tileType.Value, rotation.Value, Fixed: false)
        };
        var newHandCounts = new Dictionary<HexTileType, int>(state.HandCounts)
        {
            [tileType.Value] = count - 1
        };

        var newState = state with
        {
            Grid       = newGrid,
            HandCounts = newHandCounts,
        };

        return FinishAction(ctx, newState, actor);
    }

    // ── RotateTile ────────────────────────────────────────────────────────────

    private GameResult HandleRotateTile(GameContext ctx, HexEscapeState state, HexEscapeAction action, PlayerSlot actor)
    {
        var coord    = action.Coord;
        var rotation = action.Rotation;

        // AC-12: rotation must be 0–5
        if (rotation is null or < 0 or > 5)
            return Reject(ctx, "Invalid rotation.");

        if (coord is null)
            return Reject(ctx, "Invalid action.");

        // AC-3: empty cell
        if (!state.Grid.ContainsKey(coord))
            return Reject(ctx, "No tile to rotate.");

        var tile = state.Grid[coord];

        // AC-3: cannot rotate a fixed tile
        if (tile.Fixed)
            return Reject(ctx, "Cannot rotate a fixed tile.");

        // Apply rotation (same-rotation is allowed per AC-3)
        var newGrid = new Dictionary<string, HexCell>(state.Grid)
        {
            [coord] = tile with { Rotation = rotation.Value }
        };

        var newState = state with { Grid = newGrid };
        return FinishAction(ctx, newState, actor);
    }

    // ── Pass ──────────────────────────────────────────────────────────────────

    private GameResult HandlePass(GameContext ctx, HexEscapeState state, PlayerSlot actor)
    {
        // Grid unchanged — pass to FinishAction
        return FinishAction(ctx, state, actor);
    }

    // ── Post-action logic: win check → threat → game-over ────────────────────

    /// <summary>
    /// After any accepted action:
    /// 1. Recompute BFS connectivity.
    /// 2. WIN CHECK FIRST (AC-5): if won, emit GameOver/Escaped — do NOT increment threat.
    /// 3. Add acting seat to seatsActedThisRound.
    /// 4. If all seats have acted: increment threat; if threat >= threshold → GameOver/Overrun.
    /// 5. Broadcast new state.
    /// </summary>
    private GameResult FinishAction(GameContext ctx, HexEscapeState state, PlayerSlot actor)
    {
        // Recompute BFS connectivity (AD-3)
        int connected = ComputeConnectedSurvivors(state);
        state = state with { ConnectedSurvivors = connected };

        // AC-5: WIN CHECK BEFORE THREAT INCREMENT
        if (connected == state.TotalSurvivors)
        {
            var winState = state with
            {
                Phase   = HexEscapePhase.GameOver,
                Outcome = HexEscapeOutcome.Escaped,
            };
            return new GameResult(Serialize(winState), Effects: [new GameOverEffect(WinnerId: null)]);
        }

        // Add acting seat to seatsActedThisRound (distinct guard — use List not HashSet)
        var newSeats = new List<int>(state.SeatsActedThisRound);
        if (!newSeats.Contains(actor.SeatIndex))
            newSeats.Add(actor.SeatIndex);

        state = state with { SeatsActedThisRound = newSeats };

        // AD-11: check if all currently-assigned seat indices have now acted
        var allSeatIndices = state.Players.Select(p => p.SeatIndex).ToHashSet();
        bool roundComplete = allSeatIndices.All(si => newSeats.Contains(si));

        if (roundComplete)
        {
            int newThreat = state.ThreatCounter + 1;
            state = state with
            {
                ThreatCounter       = newThreat,
                SeatsActedThisRound = [],
            };

            // AC-6: loss condition
            if (newThreat >= state.ThreatThreshold)
            {
                var loseState = state with
                {
                    Phase   = HexEscapePhase.GameOver,
                    Outcome = HexEscapeOutcome.Overrun,
                };
                return new GameResult(Serialize(loseState), Effects: [new GameOverEffect(WinnerId: null)]);
            }
        }

        return new GameResult(Serialize(state));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameResult Reject(GameContext ctx, string reason) =>
        new(ctx.CurrentState, RejectionReason: reason);

    // ── Serialization helpers (private, camelCase, case-insensitive) ──────────

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        // JsonStringEnumConverter is needed so that Dictionary<HexTileType, int>
        // serializes with PascalCase string keys ("Straight", "Elbow", etc.)
        // rather than integer keys. The [JsonConverter] attribute on the enum types
        // handles enum values; this Converters entry handles enum dictionary keys.
        Converters                  = { new JsonStringEnumConverter() },
    };

    private static T Deserialize<T>(JsonDocument doc) =>
        JsonSerializer.Deserialize<T>(doc.RootElement.GetRawText(), SerializerOptions)!;

    private static JsonDocument Serialize<T>(T obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj, SerializerOptions));
}
