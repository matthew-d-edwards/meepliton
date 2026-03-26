using Meepliton.Api.Data;
using Meepliton.Api.Helpers;
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

        group.MapGet("/lobby", async (HttpContext ctx, PlatformDbContext db, IEnumerable<IGameModule> modules) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // Rooms the user is a member of, excluding finished rooms.
            var userRooms = await db.RoomPlayers
                .Where(rp => rp.UserId == userId)
                .Join(db.Rooms, rp => rp.RoomId, r => r.Id, (rp, r) => r)
                .Where(r => r.Status != RoomStatus.Finished)
                .ToListAsync();

            // Count players per room in one query.
            var roomIds      = userRooms.Select(r => r.Id).ToList();
            var playerCounts = await db.RoomPlayers
                .Where(rp => roomIds.Contains(rp.RoomId))
                .GroupBy(rp => rp.RoomId)
                .Select(g => new { RoomId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoomId, x => x.Count);

            // Build a lookup from gameId → module for gameName resolution.
            var moduleMap = modules.ToDictionary(m => m.GameId, StringComparer.OrdinalIgnoreCase);

            var rooms = userRooms.Select(r => new
            {
                roomId      = r.Id,
                gameId      = r.GameId,
                gameName    = moduleMap.TryGetValue(r.GameId, out var mod) ? mod.Name : r.GameId,
                status      = MapStatus(r.Status),
                playerCount = playerCounts.TryGetValue(r.Id, out var cnt) ? cnt : 0,
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

        group.MapGet("/games", (IEnumerable<IGameModule> modules) =>
            Results.Ok(modules.Select(m => new
            {
                gameId      = m.GameId,
                name        = m.Name,
                description = m.Description,
                minPlayers  = m.MinPlayers,
                maxPlayers  = m.MaxPlayers,
            })));

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
            return Results.Created($"/api/rooms/{room.Id}", new { roomId = room.Id, joinCode = room.JoinCode });
        });

        group.MapGet("/rooms/{roomId}", async (string roomId, PlatformDbContext db) =>
        {
            var room = await db.Rooms.FindAsync(roomId);
            return room is null ? Results.NotFound() : Results.Ok(room);
        });

        group.MapPost("/rooms/join", async (JoinRoomRequest req, HttpContext ctx, PlatformDbContext db, IHubContext<GameHub> hubContext, CancellationToken ct) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!;
            var room   = await db.Rooms.FirstOrDefaultAsync(r => r.JoinCode == req.Code, ct);
            if (room is null) return Results.NotFound();

            // 409 for rooms that are no longer joinable.
            if (room.Status == RoomStatus.InProgress)
                return Results.Conflict(new { message = "Room has already started" });
            if (room.Status == RoomStatus.Finished)
                return Results.Conflict(new { message = "Room has ended" });

            // Idempotent — return 200 if the caller is already in the room.
            var alreadyIn = await db.RoomPlayers.AnyAsync(rp => rp.RoomId == room.Id && rp.UserId == userId, ct);
            if (!alreadyIn)
            {
                var seat = await db.RoomPlayers.CountAsync(rp => rp.RoomId == room.Id, ct);
                db.RoomPlayers.Add(new RoomPlayer { RoomId = room.Id, UserId = userId, SeatIndex = seat });
                await db.SaveChangesAsync(ct);

                // Notify all players already in the room that a new player joined.
                var user = await db.Users.FindAsync(new object[] { userId }, ct);
                if (user is not null)
                {
                    await hubContext.Clients.Group(room.Id).SendAsync("PlayerJoined", new
                    {
                        id          = user.Id,
                        displayName = user.DisplayName,
                        avatarUrl   = AvatarHelper.ResolveAvatarUrl(user.AvatarUrl, user.Email),
                        seatIndex   = seat,
                        connected   = true,
                    }, ct);
                }
            }
            return Results.Ok(new { roomId = room.Id });
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

            var players = (await db.RoomPlayers
                .Where(rp => rp.RoomId == roomId)
                .Join(db.Users, rp => rp.UserId, u => u.Id, (rp, u) => new { u.Id, u.DisplayName, u.AvatarUrl, u.Email, rp.SeatIndex })
                .ToListAsync(ct))
                .Select(p => new PlayerInfo(p.Id, p.DisplayName, AvatarHelper.ResolveAvatarUrl(p.AvatarUrl, p.Email), p.SeatIndex))
                .ToList();

            room.GameState    = module.CreateInitialState(players, room.GameOptions);
            room.Status       = RoomStatus.InProgress;
            room.StateVersion = 1;
            await db.SaveChangesAsync(ct);

            // Notify all players that the game has started.
            await hubContext.Clients.Group(roomId).SendAsync("GameStarted", new { roomId }, ct);

            // Broadcast the initial game state so every client immediately renders
            // the board without waiting for a player action.
            if (module.HasStateProjection)
            {
                // Signal all players to pull their own projected state via GetState.
                await hubContext.Clients.Group(roomId).SendAsync("StateChanged", ct);
            }
            else
            {
                await hubContext.Clients.Group(roomId).SendAsync("StateUpdated", room.GameState, ct);
            }

            return Results.NoContent();
        });

        group.MapGet("/rooms/{roomId}/players", async (string roomId, PlatformDbContext db, CancellationToken ct) =>
        {
            var rawPlayers = await db.RoomPlayers
                .Where(rp => rp.RoomId == roomId)
                .Join(db.Users, rp => rp.UserId, u => u.Id,
                    (rp, u) => new { u.Id, u.DisplayName, u.AvatarUrl, u.Email, rp.SeatIndex })
                .OrderBy(p => p.SeatIndex)
                .ToListAsync(ct);

            var players = rawPlayers
                .Select(p => new { id = p.Id, displayName = p.DisplayName, avatarUrl = AvatarHelper.ResolveAvatarUrl(p.AvatarUrl, p.Email), seatIndex = p.SeatIndex })
                .ToList();
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

        group.MapPost("/rooms/{roomId}/transfer-host", async (string roomId, TransferHostRequest req, HttpContext ctx, PlatformDbContext db, IHubContext<GameHub> hubContext, CancellationToken ct) =>
        {
            var callerId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!;
            var room     = await db.Rooms.FindAsync(new object[] { roomId }, ct);
            if (room is null || room.HostId != callerId) return Results.Forbid();
            if (req.TargetUserId == callerId) return Results.BadRequest(new { message = "Cannot transfer host to yourself." });
            if (room.Status != RoomStatus.Waiting) return Results.Conflict(new { message = "Cannot transfer host while the game is in progress or finished." });

            var isMember = await db.RoomPlayers.AnyAsync(rp => rp.RoomId == roomId && rp.UserId == req.TargetUserId, ct);
            if (!isMember) return Results.NotFound(new { message = "Target user is not a member of this room." });

            var oldHostId  = room.HostId;
            room.HostId    = req.TargetUserId;
            await db.SaveChangesAsync(ct);
            await hubContext.Clients.Group(roomId).SendAsync("HostTransferred", new { newHostId = req.TargetUserId, oldHostId }, ct);
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

        group.MapGet("/rooms/{roomId}/action-log", async (string roomId, HttpContext ctx, PlatformDbContext db, CancellationToken ct) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!;
            var room   = await db.Rooms.FindAsync(new object[] { roomId }, ct);
            if (room is null || room.HostId != userId) return Results.Forbid();

            var entries = await db.ActionLog
                .Where(a => a.RoomId == roomId)
                .Join(db.Users, a => a.PlayerId, u => u.Id,
                    (a, u) => new
                    {
                        id          = a.Id,
                        userId      = a.PlayerId,
                        displayName = u.DisplayName,
                        actionJson  = a.Action,
                        createdAt   = a.CreatedAt,
                    })
                .OrderBy(e => e.createdAt)
                .ToListAsync(ct);

            return Results.Ok(entries);
        });

        app.MapGet("/api/health", () => Results.Ok(new { Status = "healthy" }));
    }

    // Maps RoomStatus enum to the lowercase string values expected by the frontend.
    static string MapStatus(RoomStatus status) => status switch
    {
        RoomStatus.Waiting    => "waiting",
        RoomStatus.InProgress => "playing",
        RoomStatus.Finished   => "finished",
        _                     => status.ToString().ToLowerInvariant(),
    };

    static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // excludes O, I, 0, 1
        return new string(Enumerable.Range(0, 6).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }

    record CreateRoomRequest(string GameId, object? Options = null);
    record JoinRoomRequest(string Code);
    record TransferHostRequest(string TargetUserId);
}
