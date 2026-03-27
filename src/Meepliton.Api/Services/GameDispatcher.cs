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
        // Retry loop for optimistic concurrency conflicts.
        // Simultaneous-pick games (e.g. SushiGo) can have multiple players sending
        // actions at the same time. IsConcurrencyToken() on StateVersion means EF Core
        // includes the original version in the UPDATE WHERE clause. If another request
        // already committed, SaveChangesAsync throws DbUpdateConcurrencyException and we
        // reload the room and retry — the action is re-validated and re-applied against
        // the fresh state, correctly seeing the other player's pick already recorded.
        const int maxRetries = 5;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            // Reload on every attempt so EF does not use a stale tracked entity.
            db.ChangeTracker.Clear();

            var room = await db.Rooms
                .FirstOrDefaultAsync(r => r.Id == roomId, ct)
                ?? throw new InvalidOperationException($"Room {roomId} not found.");

            var handler = handlers.FirstOrDefault(h => h.GameId == room.GameId)
                ?? throw new InvalidOperationException($"No handler registered for game '{room.GameId}'.");

            var module = modules.FirstOrDefault(m => m.GameId == room.GameId);

            var ctx = new GameContext(
                CurrentState: room.GameState ?? JsonDocument.Parse("{}"),
                Action:       action,
                PlayerId:     playerId,
                RoomId:       roomId,
                StateVersion: room.StateVersion
            );

            var result = handler.Handle(ctx);

            if (result.RejectionReason is not null)
            {
                logger.LogInformation("Action rejected in room {RoomId}: {Reason}", roomId, result.RejectionReason);
                return result;
            }

            // Persist new state
            room.GameState    = result.NewState;
            room.StateVersion++;

            db.ActionLog.Add(new ActionLog
            {
                RoomId       = roomId,
                PlayerId     = playerId,
                Action       = action,
                StateVersion = room.StateVersion,
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries - 1)
            {
                // Another request committed between our read and our write.
                // Clear tracked entities and retry from the top of the loop.
                logger.LogDebug(
                    "Concurrency conflict on room {RoomId} (attempt {Attempt}), retrying",
                    roomId, attempt + 1);
                continue;
            }

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
                    .SendAsync("StateUpdated", result.NewState, ct);
            }

            // Handle side effects
            foreach (var effect in result.Effects)
            {
                if (effect is GameOverEffect gameOver)
                {
                    room.Status = RoomStatus.Finished;
                    await db.SaveChangesAsync(ct);
                    await hubContext.Clients.Group(roomId)
                        .SendAsync("GameFinished", new { WinnerId = gameOver.WinnerId }, ct);
                }
                else if (effect is NotifyEffect notify)
                {
                    await hubContext.Clients.User(notify.PlayerId)
                        .SendAsync("Notification", notify.Message, ct);
                }
            }

            return result;
        } // end retry loop

        // Should never reach here (maxRetries exhausted means last attempt re-throws)
        throw new InvalidOperationException($"Failed to commit action in room {roomId} after {maxRetries} attempts.");
    }
}
