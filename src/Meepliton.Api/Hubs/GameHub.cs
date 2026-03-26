using System.Collections.Concurrent;
using System.Text.Json;
using Meepliton.Api.Data;
using Meepliton.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Meepliton.Api.Hubs;

[Authorize]
public class GameHub(GameDispatcher dispatcher, PlatformDbContext db, ILogger<GameHub> logger) : Hub
{
    private static readonly ConcurrentDictionary<string, string> _connectionRooms = new();

    public async Task JoinRoom(string roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        _connectionRooms[Context.ConnectionId] = roomId;

        var playerId = Context.UserIdentifier!;

        // Check if player has an existing seat (reconnect scenario)
        var room = await db.Rooms.FindAsync(roomId);
        if (room?.GameState is not null)
        {
            // Push current state to reconnecting client only (projected if module uses per-player state)
            var stateToSend = dispatcher.ProjectStateForPlayerOrFull(room.GameId, room.GameState, playerId);
            await Clients.Caller.SendAsync("StateUpdated", stateToSend);
        }

        // Notify other players this player connected
        await Clients.OthersInGroup(roomId).SendAsync("PlayerConnected", playerId);

        logger.LogInformation("Player {PlayerId} joined/rejoined room {RoomId}", playerId, roomId);
    }

    /// <summary>
    /// Sends the caller their current projected game state without any side effects
    /// (no group join, no PlayerConnected broadcast). Called by clients when they
    /// receive a StateChanged notification after an action or game start.
    /// </summary>
    public async Task GetState(string roomId)
    {
        var playerId = Context.UserIdentifier!;
        var room = await db.Rooms.FindAsync(roomId);
        if (room?.GameState is not null)
        {
            var stateToSend = dispatcher.ProjectStateForPlayerOrFull(room.GameId, room.GameState, playerId);
            await Clients.Caller.SendAsync("StateUpdated", stateToSend);
        }
    }

    public async Task LeaveRoom(string roomId)
    {
        _connectionRooms.TryRemove(Context.ConnectionId, out _);
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
        var playerId = Context.UserIdentifier;
        if (_connectionRooms.TryRemove(Context.ConnectionId, out var roomId) && playerId is not null)
        {
            await Clients.OthersInGroup(roomId).SendAsync("PlayerDisconnected", playerId);
            logger.LogInformation("Player {PlayerId} disconnected from room {RoomId}", playerId, roomId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
