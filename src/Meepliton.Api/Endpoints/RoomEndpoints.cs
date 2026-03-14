using Meepliton.Api.Data;
using Meepliton.Api.Models;
using Meepliton.Contracts;
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

        group.MapPost("/rooms/{roomId}/start", async (string roomId, HttpContext ctx, PlatformDbContext db, IEnumerable<IGameModule> modules) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!;
            var room   = await db.Rooms.FindAsync(roomId);
            if (room is null || room.HostId != userId) return Results.Forbid();

            var module = modules.FirstOrDefault(m => m.GameId == room.GameId);
            if (module is null) return Results.Problem($"Unknown game: {room.GameId}");

            var players = await db.RoomPlayers
                .Where(rp => rp.RoomId == roomId)
                .Join(db.Users, rp => rp.UserId, u => u.Id, (rp, u) => new PlayerInfo(u.Id, u.DisplayName, u.AvatarUrl, rp.SeatIndex))
                .ToListAsync();

            room.GameState    = module.CreateInitialState(players, room.GameOptions);
            room.Status       = RoomStatus.InProgress;
            room.StateVersion = 1;
            await db.SaveChangesAsync();
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
