using Meepliton.Contracts;
using Meepliton.Games.LiarsDice.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Meepliton.Games.LiarsDice;

public class LiarsDiceDbContext(DbContextOptions<LiarsDiceDbContext> options, IConfiguration configuration)
    : DbContext(options), IGameDbContext
{
    public string GameId => "liarsdice";

    // Read-only platform views — no migrations generated for these
    public DbSet<RoomView>       Rooms       => Set<RoomView>();
    public DbSet<RoomPlayerView> RoomPlayers => Set<RoomPlayerView>();
    public DbSet<UserView>       Users       => Set<UserView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_liarsdice"));
        }
    }

    public async Task MigrateAsync(CancellationToken ct = default)
        => await Database.MigrateAsync(ct);
}
