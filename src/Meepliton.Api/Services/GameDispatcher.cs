using System.Data;
using System.Text.Json;
using Meepliton.Api.Data;
using Meepliton.Api.Hubs;
using Meepliton.Api.Models;
using Meepliton.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Meepliton.Api.Services;

public class GameDispatcher(
    PlatformDbContext db,
    IEnumerable<IGameModule> modules,
    IEnumerable<IGameHandler> handlers,
    IHubContext<GameHub> hubContext,
    ILogger<GameDispatcher> logger)
{
    public JsonDocument ProjectStateForPlayerOrFull(string gameId, JsonDocument fullState, string playerId)
    {
        var module = modules.FirstOrDefault(m => m.GameId == gameId);
        if (module is null || !module.HasStateProjection) return fullState;
        return module.ProjectStateForPlayer(fullState, playerId) ?? fullState;
    }

    public async Task<GameResult> DispatchAsync(
        string roomId,
        string playerId,
        JsonDocument action,
        CancellationToken ct = default)
    {
        // NpgsqlRetryingExecutionStrategy requires all manual transactions to be wrapped
        // via CreateExecutionStrategy so the retry logic can re-run the entire unit of work.
        var strategy = db.Database.CreateExecutionStrategy();

        // Captured inside the retry lambda and read outside it.
        GameResult? result = null;
        string? gameId = null;

        await strategy.ExecuteAsync(async () =>
        {
            // Use SELECT FOR UPDATE to acquire a per-room exclusive row lock before reading state.
            // The lock is held until the transaction commits, so concurrent requests for the same
            // room queue up at the database — each sees the fully-committed state of the previous.
            // Different rooms lock different rows and never block each other.
            //
            // NOTE: ExecuteSqlAsync is used for the lock query rather than FromSqlRaw+FirstOrDefault.
            // EF Core wraps FromSqlRaw in a subquery to project columns, and PostgreSQL rejects
            // FOR UPDATE inside a derived table with "FOR UPDATE is not allowed in subqueries".
            // ExecuteSqlAsync runs the lock query directly, then a normal EF Core query loads the
            // entity within the same transaction (so the lock is preserved).
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            await db.Database.ExecuteSqlAsync(
                $"""SELECT 1 FROM rooms WHERE "Id" = {roomId} FOR UPDATE""", ct);

            var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId, ct)
                ?? throw new InvalidOperationException($"Room {roomId} not found.");

            gameId = room.GameId;

            var handler = handlers.FirstOrDefault(h => h.GameId == room.GameId)
                ?? throw new InvalidOperationException($"No handler registered for game '{room.GameId}'.");

            var ctx = new GameContext(
                CurrentState: room.GameState ?? JsonDocument.Parse("{}"),
                Action:       action,
                PlayerId:     playerId,
                RoomId:       roomId,
                StateVersion: room.StateVersion
            );

            result = handler.Handle(ctx);

            if (result.RejectionReason is not null)
            {
                logger.LogInformation("Action rejected in room {RoomId}: {Reason}", roomId, result.RejectionReason);
                await tx.RollbackAsync(ct);
                return;
            }

            // Persist new state
            room.GameState    = result.NewState;
            room.StateVersion++;

            // Apply GameOverEffect status change inside the transaction so the room Status
            // and the new game state are committed atomically.
            var gameOverEffect = result.Effects.OfType<GameOverEffect>().FirstOrDefault();
            if (gameOverEffect is not null)
                room.Status = RoomStatus.Finished;

            db.ActionLog.Add(new ActionLog
            {
                RoomId       = roomId,
                PlayerId     = playerId,
                Action       = action,
                StateVersion = room.StateVersion,
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        var finalResult = result!;

        if (finalResult.RejectionReason is not null)
            return finalResult;

        var module = modules.FirstOrDefault(m => m.GameId == gameId);

        // Broadcast state — fan out per-player if the module implements projection
        if (module?.HasStateProjection == true)
        {
            // Signal every player in the room to pull their own projected state via GetState.
            // This avoids User(pid) routing which requires NameIdentifier claim on SignalR
            // connections — the JWT uses "sub", so User(pid) silently delivers to nobody.
            await hubContext.Clients.Group(roomId).SendAsync("StateChanged", ct);
        }
        else
        {
            await hubContext.Clients
                .Group(roomId)
                .SendAsync("StateUpdated", finalResult.NewState, ct);
        }

        // Handle post-commit side effects (SignalR only — no more DB writes after commit)
        foreach (var effect in finalResult.Effects)
        {
            if (effect is GameOverEffect go)
            {
                await hubContext.Clients.Group(roomId)
                    .SendAsync("GameFinished", new { WinnerId = go.WinnerId }, ct);
            }
            else if (effect is NotifyEffect notify)
            {
                await hubContext.Clients.User(notify.PlayerId)
                    .SendAsync("Notification", notify.Message, ct);
            }
        }

        return finalResult;
    }
}
