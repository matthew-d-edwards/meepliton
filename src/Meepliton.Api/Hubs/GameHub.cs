using System.Text.Json;
using Meepliton.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Meepliton.Api.Hubs;

[Authorize]
public class GameHub(GameDispatcher dispatcher, ILogger<GameHub> logger) : Hub
{
    public async Task JoinRoom(string roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        logger.LogInformation("Player {PlayerId} joined room {RoomId}", Context.UserIdentifier, roomId);
    }

    public async Task LeaveRoom(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        logger.LogInformation("Player {PlayerId} left room {RoomId}", Context.UserIdentifier, roomId);
    }

    public async Task SendAction(string roomId, JsonDocument action)
    {
        var playerId = Context.UserIdentifier
            ?? throw new HubException("Unauthenticated.");

        var result = await dispatcher.DispatchAsync(roomId, playerId, action);

        if (result.RejectionReason is not null)
        {
            await Clients.Caller.SendAsync("ActionRejected", new { Reason = result.RejectionReason });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Player {PlayerId} disconnected", Context.UserIdentifier);
        await base.OnDisconnectedAsync(exception);
    }
}
