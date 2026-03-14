using Meepliton.Contracts;
using Meepliton.Games.Skyline.Models;
using Microsoft.EntityFrameworkCore;

namespace Meepliton.Games.Skyline;

public class SkylineDbContext(DbContextOptions<SkylineDbContext> options, IConfiguration configuration)
    : DbContext(options), IGameDbContext
{
    public string GameId => "skyline";

    // Game-owned tables
    public DbSet<SkylineGameResult>  GameResults => Set<SkylineGameResult>();
    public DbSet<SkylinePlayerStats> PlayerStats => Set<SkylinePlayerStats>();

    // Read-only platform views — no migrations generated for these
    public DbSet<RoomView>       Rooms       => Set<RoomView>();
    public DbSet<RoomPlayerView> RoomPlayers => Set<RoomPlayerView>();
    public DbSet<UserView>       Users       => Set<UserView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SkylineGameResult>(e =>
        {
            e.ToTable("skyline_game_results");
            e.Property(r => r.FinalScores).HasColumnType("jsonb");
        });
        modelBuilder.Entity<SkylinePlayerStats>().ToTable("skyline_player_stats");

        // Keyless — EF Core generates no migrations for these
        modelBuilder.Entity<RoomView>().ToTable("rooms").HasNoKey();
        modelBuilder.Entity<RoomPlayerView>().ToTable("room_players").HasNoKey();
        modelBuilder.Entity<UserView>().ToTable("users").HasNoKey();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseNpgsql(
                configuration.GetConnectionString("meepliton"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_skyline"));
        }
    }

    public async Task MigrateAsync(CancellationToken ct = default)
        => await Database.MigrateAsync(ct);
}
