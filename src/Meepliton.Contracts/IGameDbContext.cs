namespace Meepliton.Contracts;

public interface IGameDbContext
{
    string GameId { get; }
    Task MigrateAsync(CancellationToken ct = default);
}
