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
}
