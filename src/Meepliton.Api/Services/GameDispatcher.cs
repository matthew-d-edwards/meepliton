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
    public async Task<GameResult> DispatchAsync(
        string roomId,
        string playerId,
        JsonDocument action,
        CancellationToken ct = default)
    {
        var room = await db.Rooms
            .FirstOrDefaultAsync(r => r.Id == roomId, ct)
            ?? throw new InvalidOperationException($"Room {roomId} not found.");

        var handler = handlers.FirstOrDefault(h => h.GameId == room.GameId)
            ?? throw new InvalidOperationException($"No handler registered for game '{room.GameId}'.");

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

        // Broadcast to all players in the room group
        await hubContext.Clients
            .Group(roomId)
            .SendAsync("StateUpdated", result.NewState, ct);

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
