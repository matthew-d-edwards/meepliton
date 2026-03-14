using System.Security.Claims;
using System.Text.Json;
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

        // GET /api/lobby — rooms the user belongs to + all registered game modules
        group.MapGet("/lobby", async (
            HttpContext ctx,
            PlatformDbContext db,
            IEnumerable<IGameModule> modules) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var roomRows = await db.RoomPlayers
                .Where(rp => rp.UserId == userId)
                .Join(db.Rooms, rp => rp.RoomId, r => r.Id, (rp, r) => r)
                .Where(r => r.Status != RoomStatus.Finished && r.Status != RoomStatus.Closed)
                .Select(r => new
                {
                    r.Id,
                    r.GameId,
                    r.JoinCode,
                    r.Status,
                })
                .ToListAsync();

            // Collect player counts in a single query
            var roomIds = roomRows.Select(r => r.Id).ToList();
            var playerCounts = await db.RoomPlayers
                .Where(rp => roomIds.Contains(rp.RoomId))
                .GroupBy(rp => rp.RoomId)
                .Select(g => new { RoomId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoomId, x => x.Count);

            // Build a game name lookup from the registered modules
            var moduleMap = modules.ToDictionary(m => m.GameId);

            var rooms = roomRows.Select(r => new
            {
                roomId      = r.Id,
                gameId      = r.GameId,
                gameName    = moduleMap.TryGetValue(r.GameId, out var mod) ? mod.Name : r.GameId,
                status      = MapStatus(r.Status),
                playerCount = playerCounts.GetValueOrDefault(r.Id, 0),
                joinCode    = r.JoinCode,
            });

            var games = modules.Select(m => new
            {
                gameId      = m.GameId,
                name        = m.Name,
                description = m.Description,
                minPlayers  = m.MinPlayers,
                maxPlayers  = m.MaxPlayers,
            });

            return Results.Ok(new { rooms, games });
        });

        // POST /api/rooms — create a room; returns { roomId, joinCode }
        group.MapPost("/rooms", async (
            CreateRoomRequest req,
            HttpContext ctx,
            PlatformDbContext db,
            IEnumerable<IGameModule> modules) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (!modules.Any(m => m.GameId == req.GameId))
                return Results.NotFound(new { error = $"Unknown game: {req.GameId}" });

            var code = GenerateJoinCode();
            var room = new Room
            {
                GameId      = req.GameId,
                HostId      = userId,
                JoinCode    = code,
                GameOptions = req.Options,
                ExpiresAt   = DateTimeOffset.UtcNow.AddHours(48),
            };
            db.Rooms.Add(room);
            db.RoomPlayers.Add(new RoomPlayer { RoomId = room.Id, UserId = userId, SeatIndex = 0 });
            await db.SaveChangesAsync();

            return Results.Created($"/api/rooms/{room.Id}", new { roomId = room.Id, joinCode = room.JoinCode });
        });

        // GET /api/rooms/{roomId}
        group.MapGet("/rooms/{roomId}", async (string roomId, PlatformDbContext db) =>
        {
            var room = await db.Rooms.FindAsync(roomId);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        // POST /api/rooms/join — join by code; returns { roomId }; 409 if not waiting
        group.MapPost("/rooms/join", async (
            JoinRoomRequest req,
            HttpContext ctx,
            PlatformDbContext db) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var room   = await db.Rooms.FirstOrDefaultAsync(r => r.JoinCode == req.Code.ToUpperInvariant());
            if (room is null) return Results.NotFound(new { error = "Room not found." });

            if (room.Status == RoomStatus.InProgress)
                return Results.Conflict(new { error = "The game is already in progress." });

            if (room.Status == RoomStatus.Finished)
                return Results.Conflict(new { error = "The game has already finished." });

            if (room.Status == RoomStatus.Closed)
                return Results.Conflict(new { error = "This room is closed." });

            var alreadyIn = await db.RoomPlayers.AnyAsync(rp => rp.RoomId == room.Id && rp.UserId == userId);
            if (!alreadyIn)
            {
                var seat = await db.RoomPlayers.CountAsync(rp => rp.RoomId == room.Id);
                db.RoomPlayers.Add(new RoomPlayer { RoomId = room.Id, UserId = userId, SeatIndex = seat });
                await db.SaveChangesAsync();
            }

            return Results.Ok(new { roomId = room.Id });
        });

        // POST /api/rooms/{roomId}/start
        group.MapPost("/rooms/{roomId}/start", async (
            string roomId,
            HttpContext ctx,
            PlatformDbContext db,
            IEnumerable<IGameModule> modules) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var room   = await db.Rooms.FindAsync(roomId);
            if (room is null || room.HostId != userId) return Results.Forbid();

            var module = modules.FirstOrDefault(m => m.GameId == room.GameId);
            if (module is null) return Results.Problem($"Unknown game: {room.GameId}");

            var players = await db.RoomPlayers
                .Where(rp => rp.RoomId == roomId)
                .Join(db.Users, rp => rp.UserId, u => u.Id,
                    (rp, u) => new PlayerInfo(u.Id, u.DisplayName, u.AvatarUrl, rp.SeatIndex))
                .ToListAsync();

            room.GameState    = module.CreateInitialState(players, room.GameOptions);
            room.Status       = RoomStatus.InProgress;
            room.StateVersion = 1;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // DELETE /api/rooms/{roomId}
        group.MapDelete("/rooms/{roomId}", async (
            string roomId,
            HttpContext ctx,
            PlatformDbContext db) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var room   = await db.Rooms.FindAsync(roomId);
            if (room is null || room.HostId != userId) return Results.Forbid();
            db.Rooms.Remove(room);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));
    }

    static string GenerateJoinCode()
    {
        // Uppercase A-Z (excluding O and I) + digits 2-9 (no 0 and 1)
        // This avoids ambiguous characters: 0/O, 1/I
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }

    static string MapStatus(RoomStatus status) => status switch
    {
        RoomStatus.Waiting    => "waiting",
        RoomStatus.InProgress => "playing",
        RoomStatus.Finished   => "finished",
        RoomStatus.Closed     => "closed",
        _                     => "waiting",
    };

    record CreateRoomRequest(string GameId, JsonDocument? Options = null);
    record JoinRoomRequest(string Code);
}
