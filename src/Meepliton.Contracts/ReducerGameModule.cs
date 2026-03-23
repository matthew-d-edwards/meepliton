using System.Text.Json;

namespace Meepliton.Contracts;

/// <summary>
/// Optional base class for games that are naturally action→state reducers.
/// Games with complex internal logic should implement IGameModule + IGameHandler directly.
/// </summary>
public abstract class ReducerGameModule<TState, TAction, TOptions>
    : IGameModule, IGameHandler
    where TState   : class
    where TAction  : class
    where TOptions : class
{
    public abstract string GameId      { get; }
    public abstract string Name        { get; }
    public abstract string Description { get; }
    public abstract int    MinPlayers  { get; }
    public abstract int    MaxPlayers  { get; }

    public virtual bool    AllowLateJoin      => false;
    public virtual bool    SupportsAsync      => false;
    public virtual bool    SupportsUndo       => false;
    public virtual string? ThumbnailUrl       => null;
    public virtual bool    HasStateProjection => false;

    public abstract TState  CreateInitialState(IReadOnlyList<PlayerInfo> players, TOptions? options);
    public abstract string? Validate(TState state, TAction action, string playerId);
    public abstract TState  Apply(TState state, TAction action);

    public GameResult Handle(GameContext ctx)
    {
        var state  = Deserialize<TState>(ctx.CurrentState);
        var action = Deserialize<TAction>(ctx.Action);
        var error  = Validate(state, action, ctx.PlayerId);
        if (error is not null) return new GameResult(ctx.CurrentState, error);
        var newState = Apply(state, action);
        return new GameResult(Serialize(newState));
    }

    JsonDocument IGameModule.CreateInitialState(IReadOnlyList<PlayerInfo> players, JsonDocument? options)
        => Serialize(CreateInitialState(players, options is null ? null : Deserialize<TOptions>(options)));

    /// <summary>
    /// Override to project the full game state for a specific player.
    /// Return null/default to broadcast the full state unchanged.
    /// Must be pure, deterministic, and side-effect-free.
    /// </summary>
    protected virtual TState? ProjectForPlayer(TState fullState, string playerId) => default;

    /// <inheritdoc />
    JsonDocument? IGameModule.ProjectStateForPlayer(JsonDocument fullState, string playerId)
    {
        var state = Deserialize<TState>(fullState);
        if (state is null) return null;
        var projected = ProjectForPlayer(state, playerId);
        if (projected is null) return null;
        return Serialize(projected);
    }

    protected static T Deserialize<T>(JsonDocument doc) =>
        JsonSerializer.Deserialize<T>(doc.RootElement.GetRawText())!;

    protected static JsonDocument Serialize<T>(T obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj));
}
