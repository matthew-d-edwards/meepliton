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
        // Use SELECT FOR UPDATE to acquire a per-room exclusive row lock before reading state.
        // The lock is held until the transaction commits, so concurrent requests for the same
        // room queue up at the database — each sees the fully-committed state of the previous.
        // Different rooms lock different rows and never block each other.
        // This works correctly across multiple backend instances without any retry logic.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var room = await db.Rooms
            .FromSqlRaw("SELECT * FROM rooms WHERE id = {0} FOR UPDATE", roomId)
            .FirstOrDefaultAsync(ct)
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

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

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
    }
}
