using System.Security.Claims;
using System.Web;
using Meepliton.Api.Data;
using Meepliton.Api.Identity;
using Meepliton.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Meepliton.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization("AdminOnly");

        // ── Users ────────────────────────────────────────────────────────────

        group.MapGet("/users", async (
            HttpContext ctx,
            PlatformDbContext db,
            UserManager<ApplicationUser> userManager,
            string? search,
            int page = 1,
            int pageSize = 25) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page     = Math.Max(1, page);

            // Resolve the Admin role ID once.
            var adminRoleId = await db.Roles
                .Where(r => r.Name == "Admin")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            // Collect the set of admin user IDs via a single join — no N+1 per row.
            HashSet<string> adminUserIds = adminRoleId is null
                ? []
                : (await db.UserRoles
                    .Where(ur => ur.RoleId == adminRoleId)
                    .Select(ur => ur.UserId)
                    .ToListAsync()).ToHashSet();

            // Base query.
            IQueryable<ApplicationUser> query = db.Users.OrderBy(u => u.DisplayName);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u =>
                    u.DisplayName.ToLower().Contains(term) ||
                    (u.Email != null && u.Email.ToLower().StartsWith(term)));
            }

            var totalCount = await query.CountAsync();
            var users      = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var userIds = users.Select(u => u.Id).ToList();

            // Collect login methods in one query.
            var logins = await db.UserLogins
                .Where(l => userIds.Contains(l.UserId))
                .Select(l => new { l.UserId, l.LoginProvider })
                .ToListAsync();

            var loginsByUser = logins
                .GroupBy(l => l.UserId)
                .ToDictionary(g => g.Key, g => g.Select(l => l.LoginProvider).ToHashSet());

            var now = DateTimeOffset.UtcNow;

            var items = users.Select(u =>
            {
                var methods = new List<string>();
                if (u.PasswordHash is not null) methods.Add("password");
                if (loginsByUser.TryGetValue(u.Id, out var providers) && providers.Contains("Google"))
                    methods.Add("google");

                return new
                {
                    id           = u.Id,
                    displayName  = u.DisplayName,
                    email        = u.Email,
                    emailConfirmed = u.EmailConfirmed,
                    createdAt    = u.CreatedAt,
                    lastSeenAt   = u.LastSeenAt,
                    loginMethods = methods,
                    isLockedOut  = u.LockoutEnd.HasValue && u.LockoutEnd > now,
                    lockoutEnd   = u.LockoutEnd,
                    isAdmin      = adminUserIds.Contains(u.Id),
                };
            }).ToList();

            return Results.Ok(new { items, totalCount, page, pageSize });
        });

        group.MapPost("/users/{userId}/send-password-reset", async (
            string userId,
            HttpContext ctx,
            UserManager<ApplicationUser> userManager,
            IEmailSender<ApplicationUser> emailSender,
            IConfiguration configuration) =>
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return Results.NotFound();

            if (user.PasswordHash is null)
                return Results.BadRequest(new { message = "User has no password login method." });

            var token        = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = HttpUtility.UrlEncode(token);
            var frontendBase = configuration["Frontend:BaseUrl"] ?? "https://meepliton.com";
            var resetLink    = $"{frontendBase}/reset-password?userId={user.Id}&token={encodedToken}";

            await emailSender.SendPasswordResetLinkAsync(user, user.Email!, resetLink);

            return Results.NoContent();
        });

        group.MapPost("/users/{userId}/unlock", async (
            string userId,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return Results.NotFound();

            await userManager.SetLockoutEndDateAsync(user, null);
            await userManager.ResetAccessFailedCountAsync(user);

            return Results.NoContent();
        });

        group.MapPost("/users/{userId}/grant-admin", async (
            string userId,
            HttpContext ctx,
            UserManager<ApplicationUser> userManager) =>
        {
            var currentUserId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == currentUserId)
                return Results.BadRequest(new { message = "You cannot modify your own admin role." });

            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return Results.NotFound();

            if (await userManager.IsInRoleAsync(user, "Admin"))
                return Results.BadRequest(new { message = "User is already an Admin." });

            await userManager.AddToRoleAsync(user, "Admin");
            return Results.NoContent();
        });

        group.MapPost("/users/{userId}/revoke-admin", async (
            string userId,
            HttpContext ctx,
            UserManager<ApplicationUser> userManager) =>
        {
            var currentUserId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == currentUserId)
                return Results.BadRequest(new { message = "You cannot modify your own admin role." });

            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return Results.NotFound();

            await userManager.RemoveFromRoleAsync(user, "Admin");
            return Results.NoContent();
        });

        // ── Rooms ─────────────────────────────────────────────────────────────

        group.MapGet("/rooms", async (
            PlatformDbContext db,
            IEnumerable<Meepliton.Contracts.IGameModule> modules,
            string? status,
            string? gameId,
            int page = 1,
            int pageSize = 25) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page     = Math.Max(1, page);

            IQueryable<Room> query = db.Rooms.OrderByDescending(r => r.CreatedAt);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var statuses = status.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Enum.TryParse<RoomStatus>(s, ignoreCase: true, out var v) ? (RoomStatus?)v : null)
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();
                if (statuses.Count > 0)
                    query = query.Where(r => statuses.Contains(r.Status));
            }

            if (!string.IsNullOrWhiteSpace(gameId))
                query = query.Where(r => r.GameId == gameId);

            var totalCount = await query.CountAsync();
            var rooms      = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var roomIds = rooms.Select(r => r.Id).ToList();
            var hostIds = rooms.Select(r => r.HostId).Distinct().ToList();

            // Host display names in one query.
            var hostNames = await db.Users
                .Where(u => hostIds.Contains(u.Id))
                .Select(u => new { u.Id, u.DisplayName })
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

            // Player counts in one query. ConnectedCount derives from users.LastSeenAt
            // (same 5-minute window used in the lobby).
            var onlineThreshold = DateTimeOffset.UtcNow.AddMinutes(-5);
            var playerStats = await db.RoomPlayers
                .Where(rp => roomIds.Contains(rp.RoomId))
                .Join(db.Users, rp => rp.UserId, u => u.Id, (rp, u) => new
                {
                    rp.RoomId,
                    IsConnected = u.LastSeenAt >= onlineThreshold,
                })
                .GroupBy(x => x.RoomId)
                .Select(g => new
                {
                    RoomId         = g.Key,
                    PlayerCount    = g.Count(),
                    ConnectedCount = g.Count(x => x.IsConnected),
                })
                .ToDictionaryAsync(g => g.RoomId);

            // Game name lookup.
            var moduleMap = modules.ToDictionary(m => m.GameId, StringComparer.OrdinalIgnoreCase);

            var items = rooms.Select(r =>
            {
                playerStats.TryGetValue(r.Id, out var stats);
                moduleMap.TryGetValue(r.GameId, out var module);
                hostNames.TryGetValue(r.HostId, out var hostDisplayName);

                return new
                {
                    id               = r.Id,
                    joinCode         = r.JoinCode,
                    gameId           = r.GameId,
                    gameName         = module?.Name ?? r.GameId,
                    hostId           = r.HostId,
                    hostDisplayName  = hostDisplayName ?? r.HostId,
                    status           = r.Status.ToString(),
                    playerCount      = stats?.PlayerCount ?? 0,
                    connectedCount   = stats?.ConnectedCount ?? 0,
                    createdAt        = r.CreatedAt,
                    updatedAt        = r.UpdatedAt,
                    expiresAt        = r.ExpiresAt,
                };
            }).ToList();

            return Results.Ok(new { items, totalCount, page, pageSize });
        });

        group.MapDelete("/rooms/{roomId}", async (
            string roomId,
            PlatformDbContext db) =>
        {
            var room = await db.Rooms.FindAsync(roomId);
            if (room is null) return Results.NotFound();

            // Remove child rows explicitly (EF cascade is not configured on these relationships).
            var players    = db.RoomPlayers.Where(rp => rp.RoomId == roomId);
            var actionLogs = db.ActionLog.Where(al => al.RoomId == roomId);
            db.RoomPlayers.RemoveRange(players);
            db.ActionLog.RemoveRange(actionLogs);
            db.Rooms.Remove(room);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
