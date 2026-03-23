using Meepliton.Contracts;
using Meepliton.Games.Skyline.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Meepliton.Games.Skyline;

public class SkylineDbContext : DbContext, IGameDbContext
{
    private readonly IConfiguration? _configuration;

    // Used by DI (Scrutor) — IConfiguration is always available in the container.
    public SkylineDbContext(IConfiguration configuration) => _configuration = configuration;

    // Used by IDesignTimeDbContextFactory for dotnet ef CLI tooling.
    internal SkylineDbContext(DbContextOptions<SkylineDbContext> options) : base(options) { }

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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = _configuration?.GetConnectionString("meepliton")
                ?? throw new InvalidOperationException(
                    "Connection string 'meepliton' not found. " +
                    "Use IDesignTimeDbContextFactory for dotnet ef tooling.");

            optionsBuilder.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_skyline"));
        }
    }

    public async Task MigrateAsync(CancellationToken ct = default)
        => await Database.MigrateAsync(ct);
}
