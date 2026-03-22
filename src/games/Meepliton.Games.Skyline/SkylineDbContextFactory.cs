using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Meepliton.Games.Skyline;

/// <summary>
/// Design-time factory used by the dotnet ef CLI (e.g. "dotnet ef migrations add").
/// Not used at runtime — the app constructs SkylineDbContext via DI using IConfiguration.
/// </summary>
public class SkylineDbContextFactory : IDesignTimeDbContextFactory<SkylineDbContext>
{
    public SkylineDbContext CreateDbContext(string[] args)
    {
        // Read from environment variable so CI / dev machines can override without
        // touching source. Fall back to the standard local Aspire dev connection.
        var connectionString =
            Environment.GetEnvironmentVariable("MEEPLITON_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=meepliton;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<SkylineDbContext>()
            .UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_skyline"))
            .Options;

        return new SkylineDbContext(options);
    }
}
