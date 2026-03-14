using Meepliton.Api.Identity;
using Meepliton.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Meepliton.Api.Data;

public class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Room>       Rooms       => Set<Room>();
    public DbSet<RoomPlayer> RoomPlayers => Set<RoomPlayer>();
    public DbSet<ActionLog>  ActionLog   => Set<ActionLog>();
    public DbSet<Game>       Games       => Set<Game>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // MUST be called first — sets up all Identity tables

        // Rename Identity tables to snake_case
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<string>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("role_claims");

        builder.Entity<Room>(e =>
        {
            e.ToTable("rooms");
            e.Property(r => r.GameState).HasColumnType("jsonb");
            e.Property(r => r.GameOptions).HasColumnType("jsonb");
        });
        builder.Entity<RoomPlayer>().ToTable("room_players");
        builder.Entity<ActionLog>(e =>
        {
            e.ToTable("action_log");
            e.Property(a => a.Action).HasColumnType("jsonb");
        });
        builder.Entity<Game>().ToTable("games");
    }
}
