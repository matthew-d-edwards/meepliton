using Meepliton.Api.Data;
using Meepliton.Api.Hubs;
using Meepliton.Api.Models;
using Meepliton.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Meepliton.Api.Endpoints;

public static class RoomEndpoints
{
    public static void MapRoomEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapGet("/lobby", async (HttpContext ctx, PlatformDbContext db) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var myRooms = await db.RoomPlayers
                .Where(rp => rp.UserId == userId)
                .Join(db.Rooms, rp => rp.RoomId, r => r.Id, (rp, r) => r)
                .Where(r => r.Status != RoomStatus.Finished)
                .ToListAsync();
            var games = await db.Games.ToListAsync();
            return Results.Ok(new { MyRooms = myRooms, Games = games });
        });

        group.MapGet("/games", async (PlatformDbContext db) =>
            Results.Ok(await db.Games.ToListAsync()));

        group.MapPost("/rooms", async (CreateRoomRequest req, HttpContext ctx, PlatformDbContext db) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!;
            var code   = GenerateJoinCode();
            var room   = new Room
            {
                GameId   = req.GameId,
                HostId   = userId,
                JoinCode = code,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(48),
            };
            db.Rooms.Add(room);
            db.RoomPlayers.Add(new RoomPlayer { RoomId = room.Id, UserId = userId, SeatIndex = 0 });
            await db.SaveChangesAsync();
            return Results.Created($"/api/rooms/{room.Id}", room);
        });

        group.MapGet("/rooms/{roomId}", async (string roomId, PlatformDbContext db) =>
        {
            var room = await db.Rooms.FindAsync(roomId);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        group.MapPost("/rooms/join", async (JoinRoomRequest req, HttpContext ctx, PlatformDbContext db) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!;
            var room   = await db.Rooms.FirstOrDefaultAsync(r => r.JoinCode == req.Code);
            if (room is null) return Results.NotFound();

            var alreadyIn = await db.RoomPlayers.AnyAsync(rp => rp.RoomId == room.Id && rp.UserId == userId);
            if (!alreadyIn)
            {
                var seat = await db.RoomPlayers.CountAsync(rp => rp.RoomId == room.Id);
                db.RoomPlayers.Add(new RoomPlayer { RoomId = room.Id, UserId = userId, SeatIndex = seat });
                await db.SaveChangesAsync();
            }
            return Results.Ok(room);
        });

        group.MapPost("/rooms/{roomId}/start", async (string roomId, HttpContext ctx, PlatformDbContext db, IEnumerable<IGameModule> modules, IHubContext<GameHub> hubContext, CancellationToken ct) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!;
            var room   = await db.Rooms.FindAsync(new object[] { roomId }, ct);
            if (room is null || room.HostId != userId) return Results.Forbid();

            var module = modules.FirstOrDefault(m => m.GameId == room.GameId);
            if (module is null) return Results.Problem($"Unknown game: {room.GameId}");

            var playerCount = await db.RoomPlayers.CountAsync(rp => rp.RoomId == roomId, ct);
            if (playerCount < module.MinPlayers)
                return Results.BadRequest(new { message = $"Need at least {module.MinPlayers} players to start." });

            var players = await db.RoomPlayers
                .Where(rp => rp.RoomId == roomId)
                .Join(db.Users, rp => rp.UserId, u => u.Id, (rp, u) => new PlayerInfo(u.Id, u.DisplayName, u.AvatarUrl, rp.SeatIndex))
                .ToListAsync(ct);

            room.GameState    = module.CreateInitialState(players, room.GameOptions);
            room.Status       = RoomStatus.InProgress;
            room.StateVersion = 1;
            await db.SaveChangesAsync(ct);
            await hubContext.Clients.Group(roomId).SendAsync("GameStarted", new { roomId }, ct);
            return Results.NoContent();
        });

        group.MapGet("/rooms/{roomId}/players", async (string roomId, PlatformDbContext db, CancellationToken ct) =>
        {
            var players = await db.RoomPlayers
                .Where(rp => rp.RoomId == roomId)
                .Join(db.Users, rp => rp.UserId, u => u.Id,
                    (rp, u) => new { id = u.Id, displayName = u.DisplayName, avatarUrl = u.AvatarUrl, seatIndex = rp.SeatIndex })
                .OrderBy(p => p.seatIndex)
                .ToListAsync(ct);
            return Results.Ok(players);
        });

        group.MapDelete("/rooms/{roomId}/players/{userId}", async (string roomId, string userId, HttpContext ctx, PlatformDbContext db, IHubContext<GameHub> hubContext, CancellationToken ct) =>
        {
            var callerId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!;
            var room     = await db.Rooms.FindAsync(new object[] { roomId }, ct);
            if (room is null || room.HostId != callerId) return Results.Forbid();
            if (callerId == userId) return Results.Forbid();
            if (room.Status != RoomStatus.Waiting) return Results.Conflict(new { message = "Cannot remove a player while the game is in progress or finished." });

            var entry = await db.RoomPlayers.FirstOrDefaultAsync(rp => rp.RoomId == roomId && rp.UserId == userId, ct);
            if (entry is null) return Results.NotFound();

            db.RoomPlayers.Remove(entry);
            await db.SaveChangesAsync(ct);
            await hubContext.Clients.User(userId).SendAsync("PlayerRemoved", new { roomId, reason = "Removed by host" }, ct);
            return Results.NoContent();
        });

        group.MapDelete("/rooms/{roomId}", async (string roomId, HttpContext ctx, PlatformDbContext db) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!;
            var room   = await db.Rooms.FindAsync(roomId);
            if (room is null || room.HostId != userId) return Results.Forbid();
            db.Rooms.Remove(room);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        app.MapGet("/api/health", () => Results.Ok(new { Status = "healthy" }));
    }

    static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }

    record CreateRoomRequest(string GameId);
    record JoinRoomRequest(string Code);
}
