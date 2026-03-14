namespace Meepliton.Contracts;

/// <summary>
/// Handles incoming game actions. Every game must implement this interface
/// (either directly or via ReducerGameModule).
/// </summary>
public interface IGameHandler
{
    string GameId { get; }

    /// <summary>
    /// Receives the full game context and returns the result.
    /// If RejectionReason is non-null, the action is rejected and state is unchanged.
    /// </summary>
    GameResult Handle(GameContext context);
}
