using System.Text.Json;
using System.Text.Json.Serialization;
using Meepliton.Contracts;
using Meepliton.Games.HexEscape.Models;
using Microsoft.Extensions.Logging;

namespace Meepliton.Games.HexEscape;

/// <summary>
/// Hex Escape (Outbreak) v2 — cooperative zombie survival tile-placement game for 1–6 players.
/// Players collectively place and rotate tiles to path their characters to the exit
/// before the zombie horde overruns them.
///
/// Implements IGameModule + IGameHandler directly (AD-1 — ReducerGameModule cannot emit
/// GameOverEffect; only IGameHandler.Handle can).
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
    public string  Description   => "Cooperate to escape before the zombie outbreak overruns you.";
    public int     MinPlayers    => 1;
    public int     MaxPlayers    => 6;
    public bool    AllowLateJoin => false;
    public bool    SupportsAsync => false;
    public bool    SupportsUndo  => false;
    public string? ThumbnailUrl  => null;
    public bool    HasStateProjection => true;

    public IReadOnlyList<GameSetupOption> SetupOptions =>
    [
        new GameSetupOption("levelId", "Level",
            [.. HexEscapeLevels.Ordered.Select(l => new GameSetupChoice(l.Id, l.Name))]),
    ];

    // ── Axial hex geometry ────────────────────────────────────────────────────

    // Six direction offsets: index → (Δq, Δr)
    internal static readonly (int Dq, int Dr)[] Directions =
    [
        (+1,  0),  // 0 = E
        (+1, -1),  // 1 = NE
        ( 0, -1),  // 2 = N
        (-1,  0),  // 3 = W
        (-1, +1),  // 4 = SW
        ( 0, +1),  // 5 = S
    ];

    // Base open edges per tile type (before rotation)
    internal static readonly Dictionary<HexTileType, int[]> BaseEdges = new()
    {
        { HexTileType.Straight, [0, 3] },
        { HexTileType.Elbow,    [0, 1] },
        { HexTileType.Tee,      [0, 1, 2] },
        { HexTileType.Cross,    [0, 1, 2, 3] },
        { HexTileType.Deadend,  [0] },
    };

    /// <summary>Compute open edges for a tile type at given rotation.</summary>
    internal static HashSet<int> OpenEdges(HexTileType type, int rotation) =>
        [..BaseEdges[type].Select(e => (e + rotation) % 6)];

    internal static string CoordKey(int q, int r) => $"{q},{r}";

    internal static (int Q, int R) ParseCoord(string key)
    {
        var parts = key.Split(',');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    /// <summary>Rotation that gives <paramref name="type"/> an open edge in direction <paramref name="d"/>
    /// (puts the tile's first base edge toward d).</summary>
    internal static int RotationToOpen(HexTileType type, int d) => ((d - BaseEdges[type][0]) % 6 + 6) % 6;

    /// <summary>Axial hex distance between two cells (used to steer the zombie spread at survivors).</summary>
    internal static int HexDistance(int q1, int r1, int q2, int r2)
    {
        int dq = q1 - q2, dr = r1 - r2;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
    }

    /// <summary>
    /// Check the connection rule between two adjacent cells.
    /// A tile at C with open edge in direction d connects to neighbour N if N exists
    /// in the grid AND N has an open edge in direction (d+3)%6.
    /// </summary>
    internal static bool AreConnected(
        Dictionary<string, HexCell> grid,
        HashSet<string> cellSet,
        string fromCoord,
        int direction)
    {
        if (!grid.TryGetValue(fromCoord, out var fromTile)) return false;
        var fromEdges = OpenEdges(fromTile.TileType, fromTile.Rotation);
        if (!fromEdges.Contains(direction)) return false;

        var (fq, fr) = ParseCoord(fromCoord);
        var (dq, dr) = Directions[direction];
        var toCoord = CoordKey(fq + dq, fr + dr);

        if (!cellSet.Contains(toCoord)) return false;  // off-board dead end
        if (!grid.TryGetValue(toCoord, out var toTile)) return false;  // empty cell

        var toEdges = OpenEdges(toTile.TileType, toTile.Rotation);
        int opposite = (direction + 3) % 6;
        return toEdges.Contains(opposite);
    }

    // ── BFS: connected-pipe reachability for movement (v10) ───────────────────

    /// <summary>
    /// Every cell the character can slide to from <paramref name="start"/> in a single
    /// MoveCharacter action: flood-fill outward over shared open pipe edges
    /// (<see cref="AreConnected"/>). Zombies block the tunnel — a zombie-occupied tile can be
    /// neither entered nor passed through — so the reachable set routes around them.
    /// The start cell is excluded (you must move somewhere else). This is the heart of the
    /// movement asymmetry: the player travels the full clear length of the connected network
    /// for one AP, while a zombie advances only one tile per round.
    /// </summary>
    internal static HashSet<string> ConnectedReachable(HexEscapeState state, string start)
    {
        var grid     = state.Grid;
        var cellSet  = new HashSet<string>(state.Cells);
        var zombies  = new HashSet<string>(state.Zombies.Select(z => z.Pos));

        var visited = new HashSet<string> { start };
        var queue   = new Queue<string>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current  = queue.Dequeue();
            var (cq, cr) = ParseCoord(current);
            for (int dir = 0; dir < 6; dir++)
            {
                if (!AreConnected(grid, cellSet, current, dir)) continue;
                var (dq, dr)  = Directions[dir];
                var neighbour = CoordKey(cq + dq, cr + dr);
                if (visited.Contains(neighbour)) continue;
                if (zombies.Contains(neighbour)) continue;   // zombie blocks the pipe
                visited.Add(neighbour);
                queue.Enqueue(neighbour);
            }
        }

        visited.Remove(start);
        return visited;
    }

    // ── BFS: exitConnectedCount ───────────────────────────────────────────────

    /// <summary>
    /// BFS from the exit cell outward over open shared edges.
    /// Returns the number of placed non-eliminated character positions reachable from the exit.
    /// Returns 0 when exitRevealed is false.
    /// </summary>
    private static int ComputeExitConnectedCount(HexEscapeState state)
    {
        if (!state.ExitRevealed || state.ExitCell is null) return 0;

        var grid    = state.Grid;
        var cellSet = new HashSet<string>(state.Cells);
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

                if (!cellSet.Contains(neighbour)) continue;
                if (visited.Contains(neighbour)) continue;
                if (!grid.ContainsKey(neighbour)) continue;

                var neighbourTile  = grid[neighbour];
                var neighbourEdges = OpenEdges(neighbourTile.TileType, neighbourTile.Rotation);
                int opposite       = (dir + 3) % 6;
                if (!neighbourEdges.Contains(opposite)) continue;

                visited.Add(neighbour);
                queue.Enqueue(neighbour);
            }
        }

        // Count placed non-eliminated characters reachable from exit
        return state.Characters.Count(c => c.Pos is not null && !c.Eliminated && visited.Contains(c.Pos));
    }

    // ── IGameModule.CreateInitialState ────────────────────────────────────────

    JsonDocument IGameModule.CreateInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options)
    {
        var state = BuildInitialState(players, options);
        return Serialize(state);
    }

    private HexEscapeState BuildInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options)
    {
        // Resolve level from options (AC-v2-2: fall back to tutorial-01 on null/malformed/unknown)
        string? requestedId = null;
        var optionsProvided = options is not null;
        var parseFailed = false;
        if (options is not null)
        {
            try
            {
                var parsed = Deserialize<HexEscapeOptions>(options);
                requestedId = parsed?.LevelId;
            }
            catch
            {
                parseFailed = true;
            }
        }

        HexEscapeLevel level;
        if (requestedId == "tutorial-01")
        {
            level = HexEscapeLevels.Tutorial01;
        }
        else if (requestedId is not null && HexEscapeLevels.All.TryGetValue(requestedId, out var found))
        {
            level = found;
        }
        else
        {
            if (optionsProvided && (parseFailed || requestedId is not null))
            {
                _logger?.LogWarning(
                    "HexEscape: options missing/unknown level '{LevelId}', falling back to generated standard level",
                    requestedId ?? "<malformed>");
            }
            // FEATURE 2.2: use procedurally generated level scaled to player count as default.
            level = HexEscapeLevels.GenerateStandard(players.Count);
        }

        // AC-v2-3: reject levels with empty spawn zone
        if (level.SpawnZoneCells.Count == 0)
            throw new ArgumentException($"Level '{level.Id}' has no spawn zone cells.");

        int playerCount = players.Count;

        // Build initial grid from pre-placed tiles
        var grid = new Dictionary<string, HexCell>();
        foreach (var tile in level.PrePlacedTiles)
        {
            grid[tile.Coord] = new HexCell(tile.TileType, tile.Rotation, Fixed: true, IsZombieTile: false);
        }

        // Build player slots
        var playerSlots = players
            .Select(p => new PlayerSlot(p.Id, p.DisplayName, p.AvatarUrl, p.SeatIndex))
            .ToList();

        // AC-v2-5b: assign reserved spawn cells (seat index N → spawnZoneCells[N])
        var reservedSpawnCells = new Dictionary<string, string>();
        for (int i = 0; i < players.Count; i++)
        {
            reservedSpawnCells[players[i].Id] = level.SpawnZoneCells[i];
        }

        // Initialize characters (all unplaced — no pos until first tile placed)
        var characters = players
            .Select(p => new CharacterState(p.Id, Pos: null, Eliminated: false))
            .ToList();

        // Initialize starting zombies with stable ids
        var zombies = new List<ZombieToken>();
        int zombieIdCounter = 0;
        foreach (var sz in level.StartingZombies)
        {
            zombies.Add(new ZombieToken($"z{zombieIdCounter++}", sz.Coord));
        }

        // Initialize hands (all empty before deck construction)
        var hands = players.ToDictionary(p => p.Id, _ => new List<HeldTile>());

        // ── AC-v2-1b: Deck construction ───────────────────────────────────────
        //
        // postDealSize = total deck size after starting hands are dealt
        // rawPoolSize  = postDealSize + (StartingHandSize × playerCount)
        // safeCount    = floor(rawPoolSize × SafeOpeningFraction)
        //
        // Construction:
        // 1. Assemble raw pool of normal tiles (no zombie, no exit)
        // 2. Top safeCount slots are guaranteed safe
        // 3. Deal StartingHandSize tiles per player from top safeCount raw-pool slots
        // 4. Place exit tile at random position in exit band of postDealSize deck
        // 5. Distribute zombie tiles in middle band
        // 6. Fill remaining with normal tiles

        // FEATURE 2.1: derive deck size from board geometry so the deck always has enough tiles
        // to build out the map, regardless of whether a fixed-size or generated level is used.
        int zombieTileCount = HexEscapeConstants.ZombieTileCount[playerCount];
        int buildable = level.Cells.Count - level.ExitZoneCells.Count - level.PrePlacedTiles.Count;
        int postDealSize = (int)Math.Ceiling(buildable * HexEscapeConstants.PathFillFactor)
                         + 1                                                                   // exit card
                         + zombieTileCount * (HexEscapeConstants.AvgAutoDrawPerZombie - 1)    // cascade slots
                         + HexEscapeConstants.SlackBuffer[playerCount];
        int rawPoolSize = postDealSize + (HexEscapeConstants.StartingHandSize * playerCount);
        int safeCount = (int)(rawPoolSize * HexEscapeConstants.SafeOpeningFraction);

        // Build normal tile pool based on level's pool weights
        // Total normal tiles needed: rawPoolSize - zombieTileCount - 1 (exit)
        int normalTilesNeeded = rawPoolSize - zombieTileCount - 1;
        var normalTilePool = BuildNormalTilePool(level.NormalTilePool, normalTilesNeeded);
        Shuffle(normalTilePool, Random.Shared);

        // Step 2 & 3: Fill safe opening from normal pool; deal starting hands
        var rawPool = new List<DeckEntry>();
        int normalIdx = 0;

        // Fill safeCount normal tiles (safe opening)
        for (int i = 0; i < safeCount && normalIdx < normalTilePool.Count; i++)
        {
            rawPool.Add(normalTilePool[normalIdx++]);
        }
        // If fewer normal tiles than safeCount (shouldn't happen with correct pool), pad
        while (rawPool.Count < safeCount)
            rawPool.Add(new DeckEntry(HexTileType.Straight, IsZombieTile: false, IsExitTile: false));

        // Deal starting hands from top of raw pool (safeCount positions)
        int dealtCount = 0;
        foreach (var player in players)
        {
            for (int i = 0; i < HexEscapeConstants.StartingHandSize && dealtCount < safeCount; i++)
            {
                hands[player.Id].Add(new HeldTile(rawPool[dealtCount].TileType, IsZombieTile: false, IsExitTile: false));
                dealtCount++;
            }
        }
        // Remove dealt tiles from raw pool (they were at the top)
        // Post-deal deck starts AFTER the dealt portion of raw pool
        // remaining safe positions in post-deal deck: [dealtCount .. safeCount-1] (safe)
        // then middle band: [safeCount .. exitBandStart-1] (zombies + normal fills)
        // then exit band:   [exitBandStart .. postDealSize-1] (exit tile + normal fills)

        // Build the post-deal deck array of length postDealSize
        // Positions 0..(safeCount-dealtCount-1): safe (remaining after dealing)
        // Positions (safeCount-dealtCount)..(exitBandStart-1): zombie+normal middle band
        // Positions exitBandStart..(postDealSize-1): exit tile + normal fill

        int exitBandStart = postDealSize - (int)(postDealSize * HexEscapeConstants.ExitBandFraction[playerCount]);
        int exitPos = exitBandStart + Random.Shared.Next(postDealSize - exitBandStart);

        // Collect remaining normal tiles (not yet used)
        var remainingNormal = normalTilePool.Skip(normalIdx).ToList();

        // Also grab more from normalTilePool if needed (the safe-remaining tiles are from rawPool[dealtCount..safeCount-1])
        // Those tiles are still in rawPool positions dealtCount..(safeCount-1)
        var safeRemaining = rawPool.Skip(dealtCount).Take(safeCount - dealtCount).ToList();
        // Middle band (zombie/normal mix): positions from (safeCount-dealtCount) to (exitBandStart-1) in deck
        // Exit band (exit+normal): positions from exitBandStart to postDealSize-1

        // Positions in deck space:
        //   [0 .. (safeCount-dealtCount)-1]: safe-remaining (from safeRemaining list)
        //   [(safeCount-dealtCount) .. exitBandStart-1]: middle band (zombies + normal)
        //   [exitBandStart .. postDealSize-1]: exit band (exit tile at exitPos + normal)

        int safeRemainingCount = safeCount - dealtCount;
        int middleBandSize = exitBandStart - safeRemainingCount;
        int exitBandSize = postDealSize - exitBandStart;

        // ── Middle band construction (AC-v2-1b step 5: zombie minimum spacing) ──
        // Build the middle band as an array; place zombie tiles at spaced intervals,
        // fill remaining slots with normal tiles.
        var middleBand = new DeckEntry?[middleBandSize];

        if (zombieTileCount > 0 && middleBandSize > 0)
        {
            // Check whether the band is large enough to honour ZombieTileMinSpacing.
            // The placement loop advances by (ZombieTileMinSpacing+1) positions per zombie,
            // so minimum slots = zombieTileCount + (zombieTileCount-1)*(ZombieTileMinSpacing+1).
            // (The old formula zombieTileCount+(zombieTileCount-1)*ZombieTileMinSpacing was
            //  incorrect — it undercounted by omitting the +1 advancement step per zombie.)
            int minSlotsRequired = zombieTileCount + (zombieTileCount - 1) * (HexEscapeConstants.ZombieTileMinSpacing + 1);
            if (middleBandSize < minSlotsRequired)
            {
                // Band too small — best effort: place zombie tiles at uniform random positions
                _logger?.LogWarning(
                    "HexEscape: zombie tile spacing constraint could not be satisfied for {PlayerCount}p — band too small",
                    playerCount);
                // Shuffle normal+zombie tiles uniformly
                var middleNormalsSmall = new List<DeckEntry>();
                for (int i = 0; i < middleBandSize - zombieTileCount && remainingNormal.Count > 0; i++)
                {
                    middleNormalsSmall.Add(remainingNormal[0]);
                    remainingNormal.RemoveAt(0);
                }
                var zombieEntriesSmall = Enumerable.Range(0, zombieTileCount)
                    .Select(_ => new DeckEntry(HexTileType.Straight, IsZombieTile: true, IsExitTile: false))
                    .ToList();
                var middleMixed = middleNormalsSmall.Concat(zombieEntriesSmall).ToList();
                while (middleMixed.Count < middleBandSize)
                    middleMixed.Add(new DeckEntry(HexTileType.Straight, IsZombieTile: false, IsExitTile: false));
                Shuffle(middleMixed, Random.Shared);
                for (int i = 0; i < middleBandSize; i++)
                    middleBand[i] = middleMixed[i];
            }
            else
            {
                // Distribute zombie tiles at roughly even intervals with small random jitter,
                // enforcing a minimum gap of ZombieTileMinSpacing between consecutive zombie positions.
                // interval = (bandSize - zombieTileCount) / zombieTileCount ≈ spacing between zombies
                // Place zombie i at approx: i * (bandSize / zombieTileCount), then jitter within interval.
                // Use a greedy approach: track lastZombiePos, for each zombie compute candidate position
                // as basePos + jitter in [0, maxJitter], clamped so gap >= ZombieTileMinSpacing.
                double interval = (double)middleBandSize / zombieTileCount;
                int lastZombiePos = -HexEscapeConstants.ZombieTileMinSpacing - 1; // before band start

                for (int zi = 0; zi < zombieTileCount; zi++)
                {
                    // Earliest valid position: lastZombiePos + ZombieTileMinSpacing + 1
                    int earliest = lastZombiePos + HexEscapeConstants.ZombieTileMinSpacing + 1;
                    // Latest valid position: leave room for remaining zombies
                    int latest = middleBandSize - 1 - (zombieTileCount - zi - 1) * (HexEscapeConstants.ZombieTileMinSpacing + 1);
                    // Clamp latest so it's in range
                    latest = Math.Min(latest, middleBandSize - 1);
                    earliest = Math.Max(earliest, 0);

                    // Ideal base position (centre of this zombie's interval)
                    int basePos = (int)(zi * interval + interval / 2.0);
                    basePos = Math.Max(earliest, Math.Min(latest, basePos));

                    // Apply random jitter within [earliest, latest]
                    int jitterRange = latest - earliest + 1;
                    int zombiePos = earliest + (jitterRange > 0 ? Random.Shared.Next(jitterRange) : 0);
                    // Ensure zombie position doesn't conflict with basePos constraint — just use jittered
                    lastZombiePos = zombiePos;
                    middleBand[zombiePos] = new DeckEntry(HexTileType.Straight, IsZombieTile: true, IsExitTile: false);
                }
            }
        }

        // Fill remaining middle band slots with normal tiles
        for (int i = 0; i < middleBandSize; i++)
        {
            if (middleBand[i] is not null) continue;
            if (remainingNormal.Count > 0)
            {
                middleBand[i] = remainingNormal[0];
                remainingNormal.RemoveAt(0);
            }
            else
            {
                middleBand[i] = new DeckEntry(HexTileType.Straight, IsZombieTile: false, IsExitTile: false);
            }
        }

        // Exit band: exit tile + normal fill
        var exitBand = new List<DeckEntry?>(new DeckEntry?[exitBandSize]);
        exitBand[exitPos - exitBandStart] = new DeckEntry(HexTileType.Cross, IsZombieTile: false, IsExitTile: true);
        int exitBandFillIdx = 0;
        for (int i = 0; i < exitBandSize; i++)
        {
            if (exitBand[i] is not null) continue;
            if (exitBandFillIdx < remainingNormal.Count)
            {
                exitBand[i] = remainingNormal[exitBandFillIdx++];
            }
            else
            {
                exitBand[i] = new DeckEntry(HexTileType.Straight, IsZombieTile: false, IsExitTile: false);
            }
        }

        // Assemble the post-deal deck
        var deck = new List<DeckEntry>(postDealSize);
        deck.AddRange(safeRemaining);
        deck.AddRange(middleBand.Select(e => e!));
        deck.AddRange(exitBand.Select(e => e!));

        // AC-v2-1: initial state
        return new HexEscapeState(
            Phase:                   HexEscapePhase.Actions,
            Outcome:                 null,
            LevelId:                 level.Id,
            LevelName:               level.Name,
            Cells:                   [..level.Cells],
            Grid:                    grid,
            Deck:                    deck,
            Hands:                   hands,
            DiscardPile:             [],
            Zombies:                 zombies,
            Characters:              characters,
            ActiveSeat:              null,
            ActionPointsRemaining:   0,
            QualifyingActionsThisTurn: 0,
            SeatsActedThisRound:     [],
            RoundNumber:             1,
            LastZombieRolls:         [],
            ExitRevealed:            false,
            ExitCell:                null,
            ExitConnectedCount:      0,
            Players:                 playerSlots,
            SpawnZoneCells:          [..level.SpawnZoneCells],
            ExitZoneCells:           [..level.ExitZoneCells],
            HordeOriginCells:        [..level.HordeOriginCells],
            ReservedSpawnCells:      reservedSpawnCells,
            NextZombieId:            zombieIdCounter,
            HandSizes:               null,
            DeckSize:                null
        );
    }

    // ── Normal tile pool builder ───────────────────────────────────────────────

    private static List<DeckEntry> BuildNormalTilePool(List<TilePoolEntry> pool, int count)
    {
        int totalWeight = pool.Sum(e => e.Weight);
        if (totalWeight == 0) totalWeight = 1;
        var result = new List<DeckEntry>(count);

        // Distribute proportionally by weight
        int assigned = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            int share = (i < pool.Count - 1)
                ? (int)((double)pool[i].Weight / totalWeight * count)
                : count - assigned;
            for (int j = 0; j < share; j++)
                result.Add(new DeckEntry(pool[i].TileType, IsZombieTile: false, IsExitTile: false));
            assigned += share;
        }
        return result;
    }

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ── IGameModule.ProjectStateForPlayer ─────────────────────────────────────

    JsonDocument? IGameModule.ProjectStateForPlayer(JsonDocument fullState, string playerId)
    {
        var state = Deserialize<HexEscapeState>(fullState);
        if (state is null) return null;
        return Serialize(ProjectForPlayer(state, playerId));
    }

    internal static HexEscapeState ProjectForPlayer(HexEscapeState state, string playerId)
    {
        // Build handSizes from live hands
        var handSizes = state.Hands.ToDictionary(kv => kv.Key, kv => kv.Value.Count);

        // Mask other players' hands (return empty list per AC-v2-50)
        var projectedHands = state.Hands.ToDictionary(
            kv => kv.Key,
            kv => kv.Key == playerId ? kv.Value : []
        );

        return state with
        {
            Hands    = projectedHands,
            Deck     = [],                  // strip deck contents
            DeckSize = state.Deck.Count,    // expose count
            HandSizes = handSizes,          // expose per-player counts
        };
    }

    // ── IGameHandler.Handle ───────────────────────────────────────────────────

    public GameResult Handle(GameContext ctx)
    {
        var state  = Deserialize<HexEscapeState>(ctx.CurrentState);
        var action = Deserialize<HexEscapeAction>(ctx.Action);

        // Reject all actions when game is over
        if (state.Phase == HexEscapePhase.GameOver)
            return Reject(ctx, "The game is over.");

        // Find acting player
        var actingPlayer = state.Players.FirstOrDefault(p => p.Id == ctx.PlayerId);
        if (actingPlayer is null)
            return Reject(ctx, "Player not found in this game.");

        // ── Seat claiming (AC-v2-6) ───────────────────────────────────────────
        // A turn is claimed by the first action from an unacted seat when activeSeat is null.

        int seatIdx = actingPlayer.SeatIndex;

        // Already acted this round?
        if (state.SeatsActedThisRound.Contains(seatIdx))
            return Reject(ctx, "It is not your turn.");

        // Active seat occupied by someone else?
        if (state.ActiveSeat.HasValue && state.ActiveSeat.Value != seatIdx)
            return Reject(ctx, "It is not your turn.");

        // Claim seat if no active seat (AC-v2-6: only on real actions — not checked for EndTurn
        // specifically, but EndTurn still goes through claiming to confirm the seat is active)
        bool justClaimed = false;
        if (!state.ActiveSeat.HasValue)
        {
            state = state with
            {
                ActiveSeat               = seatIdx,
                ActionPointsRemaining    = HexEscapeConstants.ApPoolSize[state.Players.Count],
                QualifyingActionsThisTurn = 0,
            };
            justClaimed = true;
        }

        return action.Type switch
        {
            HexActionType.DrawTile       => HandleDrawTile(ctx, state, actingPlayer),
            HexActionType.PlaceTile      => HandlePlaceTile(ctx, state, action, actingPlayer),
            HexActionType.PlaceZombieTile => HandlePlaceZombieTile(ctx, state, action, actingPlayer),
            HexActionType.RotateTile     => HandleRotateTile(ctx, state, action, actingPlayer),
            HexActionType.MoveCharacter  => HandleMoveCharacter(ctx, state, action, actingPlayer),
            HexActionType.EndTurn        => HandleEndTurn(ctx, state, actingPlayer, justClaimed),
            _                            => Reject(ctx, "Unknown action type."),
        };
    }

    // ── DrawTile (AC-v2-11, AC-v2-12, AC-v2-7, AC-v2-19, MF-1) ─────────────

    private GameResult HandleDrawTile(GameContext ctx, HexEscapeState state, PlayerSlot actor)
    {
        // AC-v2-36: AP check
        if (state.ActionPointsRemaining == 0)
            return Reject(ctx, "No action points remaining.");

        // AC-v2-46: forced zombie tile obligation check (must place zombie tile first)
        if (HasZombieTileInHand(state, actor.Id))
            return Reject(ctx, "You must place your zombie tile first.");

        // AC-v2-11: hand cap
        var hand = state.Hands[actor.Id];
        if (hand.Count >= HexEscapeConstants.HandSize)
            return Reject(ctx, "Hand is full.");

        // AC-v2-12: deck empty
        if (state.Deck.Count == 0)
            return Reject(ctx, "The deck is empty.");

        // Draw from top
        var drawnEntry = state.Deck[0];
        var newDeck = state.Deck.Skip(1).ToList();

        int newAp = state.ActionPointsRemaining - 1;
        int newQualifying = state.QualifyingActionsThisTurn + 1;  // DrawTile is qualifying

        // AC-v2-19: if exit tile drawn, server places it deterministically
        if (drawnEntry.IsExitTile)
        {
            state = state with { Deck = newDeck, ActionPointsRemaining = newAp, QualifyingActionsThisTurn = newQualifying };
            state = PlaceExitTileServerSide(state);

            // AC-v2-20: win check immediately after exit placement
            if (CheckWin(state))
            {
                // AC-v2-29c SF-4: WIN SHORT-CIRCUIT — return immediately, never fall through
                var winState = state with { Phase = HexEscapePhase.GameOver, Outcome = HexEscapeOutcome.Escaped };
                return EndWithGameOver(winState, true);
            }

            // AP check: if AP now 0, auto-end turn
            if (newAp == 0)
            {
                return EndTurnAndAdvance(ctx, state, actor);
            }

            return new GameResult(Serialize(state));
        }

        // Normal tile (not exit, not zombie): add to hand
        if (!drawnEntry.IsZombieTile)
        {
            var newHand = new List<HeldTile>(hand) { new HeldTile(drawnEntry.TileType, IsZombieTile: false, IsExitTile: false) };
            var newHands = new Dictionary<string, List<HeldTile>>(state.Hands) { [actor.Id] = newHand };
            state = state with
            {
                Deck                    = newDeck,
                Hands                   = newHands,
                ActionPointsRemaining   = newAp,
                QualifyingActionsThisTurn = newQualifying,
            };

            // AP exhausted: auto-end turn
            if (newAp == 0)
            {
                return EndTurnAndAdvance(ctx, state, actor);
            }

            return new GameResult(Serialize(state));
        }

        // ── Zombie card drawn (v11) ──────────────────────────────────────────
        // The horde grows from the centre spawn tiles, not the player's hand. The card is consumed;
        // a zombie spawns on a seed and shoves the existing line outward, laying road / rotating a
        // pipe to make room. The drawn card never enters the hand, so there is no placement
        // obligation. A shove can run a character over → the loss is caught at the next round boundary
        // (or immediately, when this draw also exhausts AP and auto-ends the turn).
        state = state with
        {
            Deck                    = newDeck,
            ActionPointsRemaining   = newAp,
            QualifyingActionsThisTurn = newQualifying,
        };
        state = SpawnHordeAtCentre(state);

        if (newAp == 0)
            return EndTurnAndAdvance(ctx, state, actor);

        return new GameResult(Serialize(state));
    }

    // ── PlaceTile (AC-v2-22) ─────────────────────────────────────────────────

    private GameResult HandlePlaceTile(GameContext ctx, HexEscapeState state, HexEscapeAction action, PlayerSlot actor)
    {
        // AC-v2-36: AP check
        if (state.ActionPointsRemaining == 0)
            return Reject(ctx, "No action points remaining.");

        // AC-v2-46: forced zombie tile obligation check
        if (HasZombieTileInHand(state, actor.Id))
            return Reject(ctx, "You must place your zombie tile first.");

        var coord    = action.Coord;
        var tileType = action.TileType;
        var rotation = action.Rotation;

        if (coord is null || tileType is null)
            return Reject(ctx, "Invalid action.");

        // AC-v2-41: rotation must be 0–5
        if (rotation is null or < 0 or > 5)
            return Reject(ctx, "Invalid rotation.");

        // AC-v2-37: coord must be on board
        if (!state.Cells.Contains(coord))
            return Reject(ctx, "Cell is not on the board.");

        // AC-v2-39: cannot place in exit zone
        if (state.ExitZoneCells.Contains(coord))
            return Reject(ctx, "Cannot place tiles in the exit zone.");

        // AC-v2-38: cannot place on occupied cell (tile present; zombies do NOT block — H6/M)
        if (state.Grid.ContainsKey(coord))
            return Reject(ctx, "Cell is already occupied.");

        var hand = state.Hands[actor.Id];
        var character = state.Characters.FirstOrDefault(c => c.PlayerId == actor.Id);

        // AC-v2-13: first tile must go in reserved spawn cell
        bool isFirstPlacement = character?.Pos is null;
        if (isFirstPlacement)
        {
            if (!state.ReservedSpawnCells.TryGetValue(actor.Id, out var reservedCell))
                return Reject(ctx, "No reserved spawn cell found.");
            if (coord != reservedCell)
                return Reject(ctx, "First tile must be placed in your assigned spawn cell.");
        }
        else
        {
            // AC-v2-13b: non-first placements cannot target another player's still-unused reserved cell
            foreach (var otherChar in state.Characters.Where(c => c.PlayerId != actor.Id && c.Pos is null))
            {
                if (state.ReservedSpawnCells.TryGetValue(otherChar.PlayerId, out var otherReserved) && otherReserved == coord)
                    return Reject(ctx, "That cell is reserved for another player's spawn.");
            }
        }

        // AC-v2-40: hand must have a tile of this type (non-zombie, non-exit)
        int tileInHand = hand.Count(t => t.TileType == tileType.Value && !t.IsZombieTile && !t.IsExitTile);
        if (tileInHand == 0)
            return Reject(ctx, "No tiles of that type remaining.");

        // Apply placement
        var newGrid = new Dictionary<string, HexCell>(state.Grid)
        {
            [coord] = new HexCell(tileType.Value, rotation.Value, Fixed: false, IsZombieTile: false)
        };

        // Remove one tile of this type from hand
        var newHand = new List<HeldTile>(hand);
        int idx = newHand.FindIndex(t => t.TileType == tileType.Value && !t.IsZombieTile && !t.IsExitTile);
        newHand.RemoveAt(idx);

        var newHands = new Dictionary<string, List<HeldTile>>(state.Hands) { [actor.Id] = newHand };

        // AC-v2-14: character spawns on first placed tile
        var newCharacters = state.Characters.ToList();
        if (isFirstPlacement)
        {
            int charIdx = newCharacters.FindIndex(c => c.PlayerId == actor.Id);
            if (charIdx >= 0)
                newCharacters[charIdx] = newCharacters[charIdx] with { Pos = coord };
        }

        state = state with
        {
            Grid                    = newGrid,
            Hands                   = newHands,
            Characters              = newCharacters,
            ActionPointsRemaining   = state.ActionPointsRemaining - 1,
            QualifyingActionsThisTurn = state.QualifyingActionsThisTurn + 1,
        };

        // Recompute exitConnectedCount
        state = state with { ExitConnectedCount = ComputeExitConnectedCount(state) };

        // Win check (AC-v2-22, AC-v2-29c, SF-4)
        if (state.ExitRevealed && CheckWin(state))
        {
            var winState = state with { Phase = HexEscapePhase.GameOver, Outcome = HexEscapeOutcome.Escaped };
            return EndWithGameOver(winState, true);
        }

        // AP exhausted: auto-end turn
        if (state.ActionPointsRemaining == 0)
            return EndTurnAndAdvance(ctx, state, actor);

        return new GameResult(Serialize(state));
    }

    // ── PlaceZombieTile (AC-v2-23, AC-v2-24, AC-v2-25) ──────────────────────

    private GameResult HandlePlaceZombieTile(GameContext ctx, HexEscapeState state, HexEscapeAction action, PlayerSlot actor)
    {
        // AC-v2-36: AP check
        if (state.ActionPointsRemaining == 0)
            return Reject(ctx, "No action points remaining.");

        // Must hold a zombie tile
        var hand = state.Hands[actor.Id];
        if (!hand.Any(t => t.IsZombieTile))
            return Reject(ctx, "You do not hold a zombie tile.");

        var coord = action.Coord;

        // AC-v2-25: if no legal in-grid tiled non-exit-zone non-zombie-occupied cell exists,
        // discard the zombie tile regardless of coord sent. Frontend may send null coord in this case.
        var candidates = GetZombieSpawnCandidates(state);
        if (candidates.Count == 0)
        {
            // Forced discard path — no valid spawn target anywhere
            state = DoForcedZombieDiscard(state, actor);

            // Win check (if somehow triggered — unlikely but correct)
            if (state.ExitRevealed && CheckWin(state))
            {
                var wState = state with { Phase = HexEscapePhase.GameOver, Outcome = HexEscapeOutcome.Escaped };
                return EndWithGameOver(wState, true);
            }

            if (state.ActionPointsRemaining == 0)
                return EndTurnAndAdvance(ctx, state, actor);

            return new GameResult(Serialize(state));
        }

        // Normal path: coord is required
        if (coord is null)
            return Reject(ctx, "Invalid action.");

        // AC-v2-17: cannot place in exit zone
        if (state.ExitZoneCells.Contains(coord))
            return Reject(ctx, "Cannot place tiles in the exit zone.");

        // AC-v2-49: must target a cell with a placed tile
        if (!state.Grid.ContainsKey(coord))
            return Reject(ctx, "Cannot place zombie on a cell without a tile.");

        // AC-v2-24: cannot place on zombie-occupied cell
        if (state.Zombies.Any(z => z.Pos == coord))
            return Reject(ctx, "Cell is already occupied.");

        // Remove zombie tile from hand
        var newHand = new List<HeldTile>(hand);
        int idx = newHand.FindIndex(t => t.IsZombieTile);
        newHand.RemoveAt(idx);
        var newHands = new Dictionary<string, List<HeldTile>>(state.Hands) { [actor.Id] = newHand };

        // Spawn zombie at coord
        state = state with { Hands = newHands };
        state = SpawnZombieAt(state, coord, out var newZombies);
        state = state with { Zombies = newZombies };

        // Co-location elimination (AC-v2-31c)
        state = EliminateCharactersAt(state, coord);

        state = state with
        {
            ActionPointsRemaining   = state.ActionPointsRemaining - 1,
            QualifyingActionsThisTurn = state.QualifyingActionsThisTurn + 1,
        };

        // Recompute exitConnectedCount
        state = state with { ExitConnectedCount = ComputeExitConnectedCount(state) };

        // Win check (AC-v2-23)
        if (state.ExitRevealed && CheckWin(state))
        {
            var winState = state with { Phase = HexEscapePhase.GameOver, Outcome = HexEscapeOutcome.Escaped };
            return EndWithGameOver(winState, true);
        }

        // AP exhausted: auto-end turn
        if (state.ActionPointsRemaining == 0)
            return EndTurnAndAdvance(ctx, state, actor);

        return new GameResult(Serialize(state));
    }

    // ── Forced zombie discard helper (AC-v2-25) ───────────────────────────────

    private static HexEscapeState DoForcedZombieDiscard(HexEscapeState state, PlayerSlot actor)
    {
        var hand = state.Hands[actor.Id];
        var newHand = new List<HeldTile>(hand);
        int idx = newHand.FindIndex(t => t.IsZombieTile);
        newHand.RemoveAt(idx);
        var newHands = new Dictionary<string, List<HeldTile>>(state.Hands) { [actor.Id] = newHand };

        var newDiscard = new List<DeckEntry>(state.DiscardPile)
        {
            new DeckEntry(HexTileType.Straight, IsZombieTile: true, IsExitTile: false)
        };

        return state with
        {
            Hands                   = newHands,
            DiscardPile             = newDiscard,
            ActionPointsRemaining   = state.ActionPointsRemaining - 1,
            QualifyingActionsThisTurn = state.QualifyingActionsThisTurn + 1,
        };
    }

    // ── RotateTile (AC-v2-26) ────────────────────────────────────────────────

    private GameResult HandleRotateTile(GameContext ctx, HexEscapeState state, HexEscapeAction action, PlayerSlot actor)
    {
        // AC-v2-36: AP check
        if (state.ActionPointsRemaining == 0)
            return Reject(ctx, "No action points remaining.");

        // AC-v2-46: forced zombie tile obligation check
        if (HasZombieTileInHand(state, actor.Id))
            return Reject(ctx, "You must place your zombie tile first.");

        var coord    = action.Coord;
        var rotation = action.Rotation;

        if (coord is null)
            return Reject(ctx, "Invalid action.");

        // AC-v2-41: rotation must be 0–5
        if (rotation is null or < 0 or > 5)
            return Reject(ctx, "Invalid rotation.");

        // AC-v2-42: no tile to rotate
        if (!state.Grid.TryGetValue(coord, out var tile))
            return Reject(ctx, "No tile to rotate.");

        // AC-v2-43 (v9): cannot rotate FIXED pre-placed level tiles (incl. the exit Cross).
        // Zombie-laid road tiles ARE rotatable — turning them is a core control mechanic: the
        // player redirects the spread and can deny a zombie the contained break-out that makes
        // the horde multiply.
        if (tile.Fixed)
            return Reject(ctx, "Cannot rotate a fixed tile.");

        var newGrid = new Dictionary<string, HexCell>(state.Grid)
        {
            [coord] = tile with { Rotation = rotation.Value }
        };

        state = state with
        {
            Grid                  = newGrid,
            ActionPointsRemaining = state.ActionPointsRemaining - 1,
            // RotateTile does NOT increment QualifyingActionsThisTurn (F1)
        };

        // Recompute exitConnectedCount
        state = state with { ExitConnectedCount = ComputeExitConnectedCount(state) };

        // Win check (AC-v2-26)
        if (state.ExitRevealed && CheckWin(state))
        {
            var winState = state with { Phase = HexEscapePhase.GameOver, Outcome = HexEscapeOutcome.Escaped };
            return EndWithGameOver(winState, true);
        }

        // AP exhausted: auto-end turn
        if (state.ActionPointsRemaining == 0)
            return EndTurnAndAdvance(ctx, state, actor);

        return new GameResult(Serialize(state));
    }

    // ── MoveCharacter (AC-v2-27) ──────────────────────────────────────────────

    private GameResult HandleMoveCharacter(GameContext ctx, HexEscapeState state, HexEscapeAction action, PlayerSlot actor)
    {
        // AC-v2-36: AP check
        if (state.ActionPointsRemaining == 0)
            return Reject(ctx, "No action points remaining.");

        // AC-v2-46: forced zombie tile obligation check
        if (HasZombieTileInHand(state, actor.Id))
            return Reject(ctx, "You must place your zombie tile first.");

        var toCoord = action.Coord;
        if (toCoord is null)
            return Reject(ctx, "Invalid action.");

        // Guard against malformed coords before ParseCoord (consistent with
        // PlaceTile/RotateTile, which validate board membership first).
        if (!state.Cells.Contains(toCoord))
            return Reject(ctx, "That cell is not on the board.");

        var character = state.Characters.FirstOrDefault(c => c.PlayerId == actor.Id);

        // AC-v2-28: character must be placed
        if (character?.Pos is null)
            return Reject(ctx, "Your character has not been placed yet.");

        // AC-v2-45: character must not be eliminated
        if (character.Eliminated)
            return Reject(ctx, "Your character has been eliminated.");

        // Movement (v10): the character slides along the connected pipe network as far as it
        // likes in a single action — not just one hex. Zombies block the tunnel, so the path
        // may neither pass through nor land on a zombie-occupied tile. One AP buys the whole
        // slide; a zombie, by contrast, advances only one tile per round. That asymmetry is
        // what makes a long pipe powerful and the rotate/sever lever worth using.
        string fromCoord = character.Pos;
        if (toCoord == fromCoord)
            return Reject(ctx, "Your character is already there.");

        var reachable = ConnectedReachable(state, fromCoord);
        if (!reachable.Contains(toCoord))
            return Reject(ctx, "No connected path to that cell.");

        // Move character
        var newCharacters = state.Characters.ToList();
        int cidx = newCharacters.FindIndex(c => c.PlayerId == actor.Id);
        newCharacters[cidx] = newCharacters[cidx] with { Pos = toCoord };

        state = state with
        {
            Characters              = newCharacters,
            ActionPointsRemaining   = state.ActionPointsRemaining - 1,
            QualifyingActionsThisTurn = state.QualifyingActionsThisTurn + 1,
        };

        // AC-v2-31a: co-location elimination BEFORE win check
        state = EliminateCharactersAt(state, toCoord);

        // Recompute exitConnectedCount
        state = state with { ExitConnectedCount = ComputeExitConnectedCount(state) };

        // Win check — only if character NOT eliminated (AC-v2-31a: elimination → no win)
        var movedChar = state.Characters.FirstOrDefault(c => c.PlayerId == actor.Id);
        if (state.ExitRevealed && movedChar is { Eliminated: false } && CheckWin(state))
        {
            var winState = state with { Phase = HexEscapePhase.GameOver, Outcome = HexEscapeOutcome.Escaped };
            return EndWithGameOver(winState, true);
        }

        // AP exhausted: auto-end turn
        if (state.ActionPointsRemaining == 0)
            return EndTurnAndAdvance(ctx, state, actor);

        return new GameResult(Serialize(state));
    }

    // ── EndTurn (AC-v2-9, AC-v2-10) ──────────────────────────────────────────

    private GameResult HandleEndTurn(GameContext ctx, HexEscapeState state, PlayerSlot actor, bool justClaimed)
    {
        // AC-v2-9, F5: forced zombie tile check FIRST (highest priority)
        if (HasZombieTileInHand(state, actor.Id))
            return Reject(ctx, "You must place your zombie tile first.");

        // AC-v2-10: minimum qualifying-actions check
        // Compute numberOfQualifyingActionsAvailableThisTurn
        int available = CountAvailableQualifyingActions(state, actor);
        int required = Math.Min(HexEscapeConstants.MinActionsPerTurn, available);

        if (state.QualifyingActionsThisTurn < required)
            return Reject(ctx, $"You must take at least {HexEscapeConstants.MinActionsPerTurn} actions this turn.");

        return EndTurnAndAdvance(ctx, state, actor);
    }

    // ── AC-v2-10: Count qualifying actions available ──────────────────────────

    private int CountAvailableQualifyingActions(HexEscapeState state, PlayerSlot actor)
    {
        var hand = state.Hands[actor.Id];
        int count = 0;

        // DrawTile available? Requires cards in the deck AND room in the hand.
        // (Counting this — not a flawed short-circuit — is what makes the
        // min(MinActionsPerTurn, available) escape hatch correct when DrawTile is
        // the only remaining qualifying action.)
        if (state.Deck.Count > 0 && hand.Count < HexEscapeConstants.HandSize)
            count++;

        // PlaceTile available?
        var character = state.Characters.FirstOrDefault(c => c.PlayerId == actor.Id);
        bool hasNormalTilesInHand = hand.Any(t => !t.IsZombieTile && !t.IsExitTile);
        if (hasNormalTilesInHand)
        {
            bool isFirstPlacement = character?.Pos is null;
            if (isFirstPlacement)
            {
                // Reserved spawn cell is always available (H10)
                if (state.ReservedSpawnCells.TryGetValue(actor.Id, out var reserved) && !state.Grid.ContainsKey(reserved))
                    count++;
            }
            else
            {
                // Any non-exit-zone, empty, non-reserved-by-another cell
                bool anyLegalPlacement = state.Cells.Any(c =>
                    !state.Grid.ContainsKey(c) &&
                    !state.ExitZoneCells.Contains(c) &&
                    !IsReservedByOther(state, actor.Id, c));
                if (anyLegalPlacement) count++;
            }
        }

        // PlaceZombieTile available? Holding a zombie tile always yields a
        // qualifying action — PlaceZombieTile, or the forced-discard path when
        // no spawn cell is available.
        if (hand.Any(t => t.IsZombieTile))
            count++;

        // MoveCharacter available?
        if (character?.Pos is not null && !character.Eliminated)
        {
            var cellSet = new HashSet<string>(state.Cells);
            bool canMove = Enumerable.Range(0, 6).Any(d =>
                AreConnected(state.Grid, cellSet, character.Pos, d));
            if (canMove) count++;
        }

        return count;
    }

    // ── Turn end and round-boundary cascade ───────────────────────────────────

    private GameResult EndTurnAndAdvance(GameContext ctx, HexEscapeState state, PlayerSlot actor)
    {
        // Add seat to seatsActedThisRound (distinct guard)
        var newSeats = new List<int>(state.SeatsActedThisRound);
        if (!newSeats.Contains(actor.SeatIndex))
            newSeats.Add(actor.SeatIndex);

        state = state with
        {
            ActiveSeat               = null,
            ActionPointsRemaining    = 0,
            QualifyingActionsThisTurn = 0,
            SeatsActedThisRound      = newSeats,
        };

        // AC-v2-32a: check if all seats have acted (0..players.Count-1)
        var allSeatIndices = Enumerable.Range(0, state.Players.Count).ToHashSet();
        bool roundComplete = allSeatIndices.All(si => newSeats.Contains(si));

        if (!roundComplete)
            return new GameResult(Serialize(state));

        // ── Round boundary cascade (AC-v2-32a through AC-v2-32f) ─────────────
        state = RunRoundBoundary(state);

        // After cascade — check if game ended
        if (state.Phase == HexEscapePhase.GameOver)
            return new GameResult(Serialize(state), Effects: [new GameOverEffect(WinnerId: null)]);

        return new GameResult(Serialize(state));
    }

    // ── Round boundary (AC-v2-32b through AC-v2-32f) ─────────────────────────

    private HexEscapeState RunRoundBoundary(HexEscapeState state)
    {
        // AC-v2-32b: Phase 1 — transition to ZombieMovement
        state = state with { Phase = HexEscapePhase.ZombieMovement, LastZombieRolls = [] };

        // Phase 2 — existing zombies chase the survivors (turn-and-move). This records each zombie's
        // action into LastZombieRolls so the client shows the horde-phase beat after the player's turn.
        state = RunPhase2HordeGrowth(state);

        // Phase 4 — legacy horde-origin spawn (tutorial-01 defines no origins → no-op here).
        state = RunPhase4HordeSpawn(state);

        // AC-v2-32f: Phase 5 — loss check and round advance
        if (CheckLoss(state))
        {
            return state with
            {
                Phase   = HexEscapePhase.GameOver,
                Outcome = HexEscapeOutcome.Overrun,
            };
        }

        // Advance round
        return state with
        {
            RoundNumber          = state.RoundNumber + 1,
            SeatsActedThisRound  = [],
            ActiveSeat           = null,
            ActionPointsRemaining = 0,
            QualifyingActionsThisTurn = 0,
            Phase                = HexEscapePhase.Actions,
        };
    }

    // ── Zombie phase (v11 chase model — bounded horde, turn-and-move) ─────────
    //
    // Existing zombies HUNT the nearest survivor along the SHARED road network (the streets the
    // players built). They never multiply on their own and never lay their own road. Each round a
    // zombie may TURN one pipe AND MOVE one tile toward a survivor: it can rotate the pipe between
    // itself and an adjacent survivor (its own tile and/or the survivor's, if non-fixed) and step
    // onto them — so a survivor who ends adjacent to a zombie can be attacked even through a shut
    // pipe. Movement is still one tile per round (the slide asymmetry stands). If it cannot step
    // closer, it rotates a pipe to set up an approach next round; fully isolated, it holds. New
    // zombies enter ONLY when a zombie card is drawn — which grows the horde from the centre seeds
    // (see SpawnHordeAtCentre) — so the horde is bounded by deck composition rather than the clock.
    // Each zombie's action is recorded into LastZombieRolls for the client's horde-phase beat.
    // The method name is historical; it no longer grows the horde.
    private HexEscapeState RunPhase2HordeGrowth(HexEscapeState state)
    {
        var cellSet     = new HashSet<string>(state.Cells);
        var exitZoneSet = new HashSet<string>(state.ExitZoneCells);
        var (eq, er)    = ParseCoord(state.ExitCell ?? "3,0");
        var survivors = state.Characters
            .Where(c => c.Pos is not null && !c.Eliminated)
            .Select(c => ParseCoord(c.Pos!))
            .ToList();
        // Distance from (q,r) to the nearest survivor — or the exit, if none placed yet.
        int Dist(int q, int r) => survivors.Count == 0
            ? HexDistance(q, r, eq, er)
            : survivors.Min(s => HexDistance(q, r, s.Q, s.R));

        // ── Chase: snapshot the current zombies; each TURNS-and-MOVES once toward a survivor ──
        // A zombie may rotate one pipe AND step one tile in the same round (v11): it can turn the
        // pipe between itself and an adjacent survivor — its own tile and/or the survivor's tile, if
        // non-fixed — and move onto them, eliminating them. So ending a turn on a tile next to a
        // zombie is dangerous even if the pipe is currently shut. Movement is still ONE tile per round
        // (the slide asymmetry stands). Each zombie's action is recorded for the UI horde-phase beat.
        var rolls = new List<ZombieRoll>();
        foreach (var snap in state.Zombies.ToList().OrderBy(z => z.Id))
        {
            var live = state.Zombies.FirstOrDefault(z => z.Id == snap.Id);
            if (live is null) continue;
            var c = live.Pos;
            var (cq, cr) = ParseCoord(c);
            if (!state.Grid.TryGetValue(c, out var cTile)) continue;
            var cEdges = OpenEdges(cTile.TileType, cTile.Rotation);
            bool cFixed = cTile.Fixed;
            var occ = new HashSet<string>(state.Zombies.Where(z => z.Id != live.Id).Select(z => z.Pos));
            int curDist = Dist(cq, cr);

            // MOVE one tile toward the nearest survivor — onto a tile we can already reach, OR one we
            // can reach by turning a non-fixed pipe (our tile and/or the destination). Prefer the cell
            // closest to a survivor, then the one needing the fewest rotations.
            int bestDir = -1, bestDist = curDist, bestRot = 99;
            int? selfRot = null, destRot = null; string? bestN = null;
            for (int d = 0; d < 6; d++)
            {
                var (dq, dr) = Directions[d]; var n = CoordKey(cq + dq, cr + dr);
                if (!cellSet.Contains(n) || exitZoneSet.Contains(n)) continue;
                if (!state.Grid.TryGetValue(n, out var nT)) continue;   // must step onto a placed tile
                if (occ.Contains(n)) continue;                          // never onto another zombie
                int rot = 0; int? sR = null, dR = null;
                if (!cEdges.Contains(d)) { if (cFixed) continue; sR = RotationToOpen(cTile.TileType, d); rot++; }
                int op = (d + 3) % 6;
                if (!OpenEdges(nT.TileType, nT.Rotation).Contains(op)) { if (nT.Fixed) continue; dR = RotationToOpen(nT.TileType, op); rot++; }
                var (nq, nr) = ParseCoord(n); int nd = Dist(nq, nr);
                if (nd >= curDist) continue;                            // only step if it gets closer (attack = nd 0)
                if (nd < bestDist || (nd == bestDist && rot < bestRot))
                { bestDist = nd; bestRot = rot; bestDir = d; selfRot = sR; destRot = dR; bestN = n; }
            }
            if (bestDir >= 0)
            {
                if (selfRot is not null || destRot is not null)
                {
                    var ng = new Dictionary<string, HexCell>(state.Grid);
                    if (selfRot is not null) ng[c] = cTile with { Rotation = selfRot.Value };
                    if (destRot is not null) ng[bestN!] = state.Grid[bestN!] with { Rotation = destRot.Value };
                    state = state with { Grid = ng };
                }
                var moved = state.Zombies.ToList(); int zi = moved.FindIndex(z => z.Id == live.Id);
                moved[zi] = moved[zi] with { Pos = bestN! };
                state = state with { Zombies = moved };
                state = EliminateCharactersAt(state, bestN!);
                rolls.Add(new ZombieRoll(live.Id, 0, bestDir, Moved: true));
                continue;
            }

            // Could not step closer this round — rotate an adjacent non-fixed pipe (and our own tile,
            // if non-fixed) to open a path toward the survivor, setting up an approach next round.
            int rotDir = -1, rotDist = curDist; HexCell? rotC = null;
            for (int d = 0; d < 6; d++)
            {
                var (dq, dr) = Directions[d]; var n = CoordKey(cq + dq, cr + dr);
                if (!cellSet.Contains(n) || exitZoneSet.Contains(n)) continue;
                if (!state.Grid.TryGetValue(n, out var nT) || nT.Fixed) continue; // only non-fixed pipes
                if (occ.Contains(n)) continue;
                HexCell? newC = null;
                if (!cEdges.Contains(d))
                {
                    if (cFixed) continue;                 // our own tile is fixed and shut this way
                    newC = cTile with { Rotation = RotationToOpen(cTile.TileType, d) };
                }
                var (nq, nr) = ParseCoord(n); int nd = Dist(nq, nr);
                if (nd < rotDist) { rotDist = nd; rotDir = d; rotC = newC; }
            }
            if (rotDir >= 0)
            {
                var (dq, dr) = Directions[rotDir]; var n = CoordKey(cq + dq, cr + dr);
                var nT = state.Grid[n];
                var newGrid = new Dictionary<string, HexCell>(state.Grid)
                {
                    [n] = nT with { Rotation = RotationToOpen(nT.TileType, (rotDir + 3) % 6) }
                };
                if (rotC is not null) newGrid[c] = rotC;
                state = state with { Grid = newGrid };
                rolls.Add(new ZombieRoll(live.Id, 0, rotDir, Moved: false));
                continue;  // chases along the new connection next round
            }

            // Isolated — nothing to step onto or rotate. It holds (no self-multiply, no self-laid road).
            rolls.Add(new ZombieRoll(live.Id, 0, -1, Moved: false));
        }

        return state with { LastZombieRolls = rolls };
    }

    // ── Phase 4: Horde spawn (AC-v2-32e, D2b) ────────────────────────────────

    private HexEscapeState RunPhase4HordeSpawn(HexEscapeState state)
    {
        // Early-round grace (AC: safe opening window). Boundaries before HordeStartRound
        // spawn no horde, so players get a couple of turns to build an opening path before
        // pressure begins. RoundNumber here is the round being closed out (pre-increment).
        if (state.RoundNumber < HexEscapeConstants.HordeStartRound)
            return state;

        int hordeCount = HexEscapeConstants.HordeRatePerRound[state.Players.Count];
        var exitZoneSet = new HashSet<string>(state.ExitZoneCells);

        // D4 (extended to horde spawns): prefer a spawn cell with no character on it, so the
        // horde never *deterministically* eliminates a survivor that had an empty alternative.
        var characterCells = state.Characters
            .Where(c => c.Pos is not null && !c.Eliminated)
            .Select(c => c.Pos!)
            .ToHashSet();

        for (int h = 0; h < hordeCount; h++)
        {
            if (state.Zombies.Count >= HexEscapeConstants.MaxZombies)
            {
                _logger?.LogWarning("HexEscape: MaxZombies cap reached, skipping horde spawn");
                break;
            }

            // Pick the first eligible hordeOriginCell (not zombie-occupied, not exit zone, tiled),
            // preferring one with no character; fall back to a character-occupied origin only when
            // every eligible origin is character-occupied (D4 — avoidable, not unavoidable, deaths).
            string? spawnCoord = null;
            string? occupiedFallback = null;
            foreach (var cell in state.HordeOriginCells)
            {
                if (exitZoneSet.Contains(cell)) continue;          // MF-3
                if (!state.Grid.ContainsKey(cell)) continue;       // C4: must have tile
                if (state.Zombies.Any(z => z.Pos == cell)) continue; // not zombie-occupied
                if (characterCells.Contains(cell)) { occupiedFallback ??= cell; continue; }
                spawnCoord = cell;
                break;
            }
            spawnCoord ??= occupiedFallback;

            if (spawnCoord is null) continue;  // no valid cell this round

            state = SpawnZombieAt(state, spawnCoord, out var newZombies);
            state = state with { Zombies = newZombies };
            state = EliminateCharactersAt(state, spawnCoord);
        }

        return state;
    }

    // ── Win / loss checks ─────────────────────────────────────────────────────

    /// <summary>
    /// AC-v2-29a: Win = exitRevealed AND at least one placed non-eliminated character
    /// AND ALL placed non-eliminated characters have pos == exitCell.
    /// </summary>
    internal static bool CheckWin(HexEscapeState state)
    {
        if (!state.ExitRevealed || state.ExitCell is null) return false;

        var placedNonEliminated = state.Characters.Where(c => c.Pos is not null && !c.Eliminated).ToList();
        if (placedNonEliminated.Count == 0) return false;

        return placedNonEliminated.All(c => c.Pos == state.ExitCell);
    }

    /// <summary>
    /// AC-v2-30a: Loss = at least one character was ever placed AND all placed characters eliminated.
    /// </summary>
    internal static bool CheckLoss(HexEscapeState state)
    {
        var placed = state.Characters.Where(c => c.Pos is not null).ToList();
        if (placed.Count == 0) return false;  // no one ever placed — not a loss yet
        return placed.All(c => c.Eliminated);
    }

    // ── Server exit placement (AC-v2-19) ─────────────────────────────────────

    private HexEscapeState PlaceExitTileServerSide(HexEscapeState state)
    {
        var cellSet = new HashSet<string>(state.Cells);

        // Collect empty exit-zone cells (no tile placed yet)
        var emptyCandidates = state.ExitZoneCells
            .Where(c => !state.Grid.ContainsKey(c))
            .ToList();

        if (emptyCandidates.Count == 0)
        {
            // Should not happen per MF-3 no-soft-lock invariant; log and skip
            _logger?.LogWarning("HexEscape: no empty exit-zone cell available for exit tile placement");
            return state;
        }

        // Select cell closest to board centroid; tie-break: lowest q, then lowest r (H6)
        var allCells = state.Cells.Select(ParseCoord).ToList();
        double centroidQ = allCells.Average(c => (double)c.Q);
        double centroidR = allCells.Average(c => (double)c.R);

        var exitCoord = emptyCandidates
            .Select(c => { var (q, r) = ParseCoord(c); return (Coord: c, Q: q, R: r, Dist: Math.Sqrt(Math.Pow(q - centroidQ, 2) + Math.Pow(r - centroidR, 2))); })
            .OrderBy(x => x.Dist)
            .ThenBy(x => x.Q)
            .ThenBy(x => x.R)
            .First()
            .Coord;

        // Place exit tile: cross r0 fixed (C3, AC-v2-19)
        var newGrid = new Dictionary<string, HexCell>(state.Grid)
        {
            [exitCoord] = new HexCell(HexTileType.Cross, 0, Fixed: true, IsZombieTile: false)
        };

        state = state with
        {
            Grid         = newGrid,
            ExitRevealed = true,
            ExitCell     = exitCoord,
        };

        // Recompute exitConnectedCount
        state = state with { ExitConnectedCount = ComputeExitConnectedCount(state) };

        return state;
    }

    // ── Zombie spawn helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Get candidate cells for zombie spawn: in-grid, tiled, non-exit-zone, non-zombie-occupied.
    /// Ordered by numeric (q, then r) — H6.
    /// </summary>
    private static List<string> GetZombieSpawnCandidates(HexEscapeState state)
    {
        var exitZoneSet = new HashSet<string>(state.ExitZoneCells);
        var zombiePositions = state.Zombies.Select(z => z.Pos).ToHashSet();

        return state.Cells
            .Where(c => state.Grid.ContainsKey(c) && !exitZoneSet.Contains(c) && !zombiePositions.Contains(c))
            .Select(c => { var (q, r) = ParseCoord(c); return (Coord: c, Q: q, R: r); })
            .OrderBy(x => x.Q)
            .ThenBy(x => x.R)
            .Select(x => x.Coord)
            .ToList();
    }

    /// <summary>
    /// Spawn a zombie at the given coord (must be a valid tiled cell).
    /// Generates a new stable id. Respects MaxZombies cap.
    /// Returns updated state. newZombies is the updated zombie list.
    /// </summary>
    private static HexEscapeState SpawnZombieAt(HexEscapeState state, string coord, out List<ZombieToken> newZombies)
    {
        newZombies = state.Zombies.ToList();
        if (newZombies.Count >= HexEscapeConstants.MaxZombies)
            return state;

        string id = $"z{state.NextZombieId}";
        newZombies.Add(new ZombieToken(id, coord));
        return state with { NextZombieId = state.NextZombieId + 1 };
    }

    // ── Co-location elimination ───────────────────────────────────────────────

    /// <summary>
    /// AC-v2-31: Eliminate all placed, non-eliminated characters at the given coord
    /// if any zombie is present at that coord.
    /// </summary>
    private static HexEscapeState EliminateCharactersAt(HexEscapeState state, string coord)
    {
        bool anyZombieAtCoord = state.Zombies.Any(z => z.Pos == coord);
        if (!anyZombieAtCoord) return state;

        var newCharacters = state.Characters.ToList();
        bool changed = false;
        for (int i = 0; i < newCharacters.Count; i++)
        {
            if (newCharacters[i].Pos == coord && !newCharacters[i].Eliminated)
            {
                newCharacters[i] = newCharacters[i] with { Eliminated = true };
                changed = true;
            }
        }

        return changed ? state with { Characters = newCharacters } : state;
    }

    // ── v11: zombie-card horde growth from the centre spawn tiles ──────────────

    /// <summary>
    /// A drawn zombie card grows the horde from the centre spawn tiles (the fixed seeds), not the
    /// player's hand. A fresh zombie takes a seed; the zombie already there is SHOVED outward toward
    /// the nearest survivor. It MOVES along existing connected road, or ROTATES an adjacent pipe to
    /// reach one (both free). If it can do neither it must EXTEND the road — and that costs a card:
    /// the engine AUTO-DRAWS from the deck until a tile turns up, lays it as zombie road, and the
    /// zombie advances onto it. An auto-drawn ZOMBIE card chains another centre spawn this resolution
    /// (the "exciting" cascade); an auto-drawn EXIT card reveals the exit. So the horde grows from the
    /// middle, spreads outward, and eats the deck as it goes — a single draw can cascade into several
    /// spawns. Bounded by MaxZombies and the deck running dry.
    /// </summary>
    private HexEscapeState SpawnHordeAtCentre(HexEscapeState state)
    {
        if (state.Zombies.Count >= HexEscapeConstants.MaxZombies) return state;

        var cellSet     = new HashSet<string>(state.Cells);
        var exitZoneSet = new HashSet<string>(state.ExitZoneCells);
        var (eq, er)    = ParseCoord(state.ExitCell ?? "3,0");
        var survivors   = state.Characters
            .Where(c => c.Pos is not null && !c.Eliminated)
            .Select(c => ParseCoord(c.Pos!))
            .ToList();
        int Dist(int q, int r) => survivors.Count == 0
            ? HexDistance(q, r, eq, er)
            : survivors.Min(s => HexDistance(q, r, s.Q, s.R));

        // Spawn seeds = fixed, non-exit-zone grid tiles. Push from the seed nearest a survivor first.
        var seeds = state.Grid
            .Where(kv => kv.Value.Fixed && !exitZoneSet.Contains(kv.Key))
            .Select(kv => kv.Key)
            .OrderBy(k => { var (q, r) = ParseCoord(k); return Dist(q, r); })
            .ThenBy(k => ParseCoord(k).Q).ThenBy(k => ParseCoord(k).R)
            .ToList();
        if (seeds.Count == 0) return state;

        int pending = 1;   // the zombie card the player drew; auto-drawn zombie cards add more
        int safety  = 0;   // backstop against a pathological cascade

        // Slide the zombie at `from` one step in `dir`, running over any character it lands on.
        // Returns false and leaves state unchanged if `to` is already zombie-occupied (M1).
        bool Shove(string from, int dir)
        {
            var (fq, fr) = ParseCoord(from);
            var (dq, dr) = Directions[dir];
            var to = CoordKey(fq + dq, fr + dr);
            // M1: recompute live occupancy at write time — reject if destination already has a zombie.
            if (state.Zombies.Any(z => z.Pos == to)) return false;
            var moved = state.Zombies.ToList();
            int zi = moved.FindIndex(z => z.Pos == from);
            if (zi >= 0) moved[zi] = moved[zi] with { Pos = to };
            state = state with { Zombies = moved };
            state = EliminateCharactersAt(state, to);
            return true;
        }

        // Free `cell` of its zombie by shoving it one step outward (toward a survivor), making room
        // with a rotation or a freshly-laid zombie road tile when no open move exists.
        bool Vacate(string cell)
        {
            if (!state.Zombies.Any(z => z.Pos == cell)) return true;
            var (cq, cr) = ParseCoord(cell);
            if (!state.Grid.TryGetValue(cell, out var cTile)) return false;
            var cEdges = OpenEdges(cTile.TileType, cTile.Rotation);
            bool cFixed = cTile.Fixed;
            var occ = new HashSet<string>(state.Zombies.Select(z => z.Pos));

            // 1) MOVE: connected, unoccupied road neighbour closest to a survivor.
            // M1: re-read live occupancy inside the scan (occ is rebuilt from state.Zombies each Vacate call,
            // but additional zombie writes may happen via Shove before this point in future iterations).
            int moveDir = -1, moveBest = int.MaxValue;
            for (int d = 0; d < 6; d++)
            {
                if (!cEdges.Contains(d)) continue;
                var (dq, dr) = Directions[d]; var n = CoordKey(cq + dq, cr + dr);
                if (!cellSet.Contains(n) || exitZoneSet.Contains(n)) continue;
                if (!state.Grid.TryGetValue(n, out var nT)) continue;
                if (!OpenEdges(nT.TileType, nT.Rotation).Contains((d + 3) % 6)) continue;
                // M1: use live state to exclude zombie-occupied cells in MOVE target selection.
                if (state.Zombies.Any(z => z.Pos == n)) continue;
                var (nq, nr) = ParseCoord(n); int nd = Dist(nq, nr);
                if (nd < moveBest) { moveBest = nd; moveDir = d; }
            }
            if (moveDir >= 0) { return Shove(cell, moveDir); }

            // 2) ROTATE an adjacent non-fixed pipe (and our own tile, if non-fixed) to open a move.
            // M1: use live state occupancy check (not stale occ snapshot).
            for (int d = 0; d < 6; d++)
            {
                var (dq, dr) = Directions[d]; var n = CoordKey(cq + dq, cr + dr);
                if (!cellSet.Contains(n) || exitZoneSet.Contains(n)) continue;
                if (!state.Grid.TryGetValue(n, out var nT) || nT.Fixed) continue;
                // M1: live occupancy — skip if destination already has a zombie.
                if (state.Zombies.Any(z => z.Pos == n)) continue;
                if (!cEdges.Contains(d) && cFixed) continue;  // our own tile is fixed and shut this way
                var newGrid = new Dictionary<string, HexCell>(state.Grid)
                {
                    [n] = nT with { Rotation = RotationToOpen(nT.TileType, (d + 3) % 6) }
                };
                if (!cEdges.Contains(d)) newGrid[cell] = cTile with { Rotation = RotationToOpen(cTile.TileType, d) };
                state = state with { Grid = newGrid };
                if (!Shove(cell, d)) { continue; }  // M1: Shove may fail if dest occupied at write time
                return true;
            }

            // 3) EXTEND the zombie road outward — this needs a fresh tile, which the engine AUTO-DRAWS
            //    from the deck. Choose the outward empty neighbour nearest a survivor to lay it on.
            int extDir = -1, extBest = int.MaxValue;
            for (int d = 0; d < 6; d++)
            {
                if (cFixed && !cEdges.Contains(d)) continue;
                var (dq, dr) = Directions[d]; var n = CoordKey(cq + dq, cr + dr);
                if (!cellSet.Contains(n) || exitZoneSet.Contains(n)) continue;
                if (state.Grid.ContainsKey(n)) continue;
                // M1: EXTEND target must not be zombie-occupied.
                if (state.Zombies.Any(z => z.Pos == n)) continue;
                var (nq, nr) = ParseCoord(n); int nd = Dist(nq, nr);
                if (nd < extBest) { extBest = nd; extDir = d; }
            }
            if (extDir < 0) return false;   // boxed: nowhere to extend

            // Auto-draw until a placeable tile turns up. A zombie card chains another centre spawn
            // (the "exciting" cascade); an exit card reveals the exit. Both consume the deck.
            while (state.Deck.Count > 0)
            {
                var card = state.Deck[0];
                state = state with { Deck = state.Deck.Skip(1).ToList() };
                if (card.IsZombieTile) { pending++; continue; }
                if (card.IsExitTile)   { if (!state.ExitRevealed) state = PlaceExitTileServerSide(state); continue; }

                // M2: re-validate target cell before laying — the auto-draw loop may have changed the grid
                // (e.g. PlaceExitTileServerSide laid a tile) or another zombie may now occupy the target.
                var (dqR, drR) = Directions[extDir]; var nR = CoordKey(cq + dqR, cr + drR);
                bool targetStillLegal = cellSet.Contains(nR)
                    && !exitZoneSet.Contains(nR)
                    && !state.Grid.ContainsKey(nR)
                    && !state.Zombies.Any(z => z.Pos == nR);
                if (!targetStillLegal)
                {
                    // Re-scan for a fresh outward empty non-zombie-occupied neighbour.
                    extDir = -1; extBest = int.MaxValue;
                    for (int d = 0; d < 6; d++)
                    {
                        if (cFixed && !cEdges.Contains(d)) continue;
                        var (dq2, dr2) = Directions[d]; var n2 = CoordKey(cq + dq2, cr + dr2);
                        if (!cellSet.Contains(n2) || exitZoneSet.Contains(n2)) continue;
                        if (state.Grid.ContainsKey(n2)) continue;
                        if (state.Zombies.Any(z => z.Pos == n2)) continue;
                        var (nq2, nr2) = ParseCoord(n2); int nd2 = Dist(nq2, nr2);
                        if (nd2 < extBest) { extBest = nd2; extDir = d; }
                    }
                    if (extDir < 0) return false;  // M2: no legal target — bail
                    var (dqF, drF) = Directions[extDir]; nR = CoordKey(cq + dqF, cr + drF);
                }

                int back = (extDir + 3) % 6;
                var newGrid = new Dictionary<string, HexCell>(state.Grid)
                {
                    [nR] = new HexCell(card.TileType, RotationToOpen(card.TileType, back), Fixed: false, IsZombieTile: true)
                };
                if (!cFixed && !cEdges.Contains(extDir))
                    newGrid[cell] = cTile with { Rotation = RotationToOpen(cTile.TileType, extDir) };
                state = state with { Grid = newGrid };
                if (!Shove(cell, extDir)) return false;  // M1: bail if destination occupied at write time
                return true;
            }
            return false;   // deck ran dry before a tile came up
        }

        while (pending > 0 && state.Zombies.Count < HexEscapeConstants.MaxZombies && safety++ < 256)
        {
            pending--;
            bool placed = false;
            foreach (var seed in seeds)
            {
                if (Vacate(seed))
                {
                    state = SpawnZombieAt(state, seed, out var nz);
                    state = state with { Zombies = nz };
                    state = EliminateCharactersAt(state, seed);
                    placed = true;
                    break;
                }
            }
            if (!placed) break;   // can't place anywhere (deck dry / boxed) — end the cascade
        }
        return state;
    }

    // ── Reserved cell helpers ─────────────────────────────────────────────────

    private static bool HasZombieTileInHand(HexEscapeState state, string playerId) =>
        state.Hands.TryGetValue(playerId, out var h) && h.Any(t => t.IsZombieTile);

    private static bool IsReservedByOther(HexEscapeState state, string actorId, string coord)
    {
        foreach (var kv in state.ReservedSpawnCells)
        {
            if (kv.Key == actorId) continue;
            if (kv.Value != coord) continue;
            // Reserved by another player — but only if that player hasn't placed yet
            var character = state.Characters.FirstOrDefault(c => c.PlayerId == kv.Key);
            if (character?.Pos is null) return true;  // still unplaced → reserved
        }
        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameResult Reject(GameContext ctx, string reason) =>
        new(ctx.CurrentState, RejectionReason: reason);

    private static GameResult EndWithGameOver(HexEscapeState state, bool _) =>
        new(Serialize(state), Effects: [new GameOverEffect(WinnerId: null)]);

    // ── Serialization helpers (private, camelCase, case-insensitive) ──────────

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        // JsonStringEnumConverter needed so that any enum-keyed dictionary serializes
        // with string keys ("Straight", "Elbow", etc.) rather than integer keys.
        // Also handles all enum values in state/actions.
        Converters                  = { new JsonStringEnumConverter() },
    };

    private static T Deserialize<T>(JsonDocument doc) =>
        JsonSerializer.Deserialize<T>(doc.RootElement.GetRawText(), SerializerOptions)!;

    private static JsonDocument Serialize<T>(T obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj, SerializerOptions));

    // ── Internal helpers exposed for testing (InternalsVisibleTo: Meepliton.Tests) ──

    /// <summary>
    /// Exposed for unit tests: resolve a single zombie move given a die face.
    /// Returns (newPos, moved) — does NOT apply to state.
    /// </summary>
    internal static (string NewPos, bool Moved) ResolveZombieMove(
        Dictionary<string, HexCell> grid,
        HashSet<string> cellSet,
        string zombiePos,
        int dieFace)
    {
        int direction = dieFace % 6;
        if (!grid.TryGetValue(zombiePos, out var tile)) return (zombiePos, false);

        var edges = OpenEdges(tile.TileType, tile.Rotation);
        if (!edges.Contains(direction)) return (zombiePos, false);

        var (zq, zr) = ParseCoord(zombiePos);
        var (dq, dr) = Directions[direction];
        var newPos = CoordKey(zq + dq, zr + dr);

        if (!cellSet.Contains(newPos) || !grid.TryGetValue(newPos, out var nTile)) return (zombiePos, false);

        int opposite = (direction + 3) % 6;
        if (!OpenEdges(nTile.TileType, nTile.Rotation).Contains(opposite)) return (zombiePos, false);

        return (newPos, true);
    }
}
