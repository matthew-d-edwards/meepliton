using System.Text.Json;

namespace Meepliton.Contracts;

/// <summary>
/// Metadata and initial-state factory for a game module.
/// Every game must implement this interface.
/// </summary>
public interface IGameModule
{
    string  GameId        { get; }
    string  Name          { get; }
    string  Description   { get; }
    int     MinPlayers    { get; }
    int     MaxPlayers    { get; }
    bool    AllowLateJoin { get; }
    bool    SupportsAsync { get; }
    bool    SupportsUndo  { get; }
    string? ThumbnailUrl  { get; }

    /// <summary>
    /// Called once when the host starts the game.
    /// Returns the initial JSON state blob stored in rooms.game_state.
    /// </summary>
    JsonDocument CreateInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options);

    /// <summary>
    /// Returns true if this game module implements per-player state projection.
    /// When true, GameDispatcher will call ProjectStateForPlayer for each player
    /// rather than broadcasting the full state to the group.
    /// </summary>
    bool HasStateProjection => false;

    /// <summary>
    /// Projects the full game state for a specific player, hiding information
    /// the player should not see. Must be pure, deterministic, and side-effect-free.
    /// Must not mutate the input document. Returns null to broadcast the full state.
    /// </summary>
    JsonDocument? ProjectStateForPlayer(JsonDocument fullState, string playerId) => null;
}
