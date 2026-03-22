using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Meepliton.Games.LiarsDice;

/// <summary>
/// Design-time factory used by the dotnet ef CLI (e.g. "dotnet ef migrations add").
/// Not used at runtime — the app constructs LiarsDiceDbContext via DI using IConfiguration.
/// </summary>
public class LiarsDiceDbContextFactory : IDesignTimeDbContextFactory<LiarsDiceDbContext>
{
    public LiarsDiceDbContext CreateDbContext(string[] args)
    {
        // Read from environment variable so CI / dev machines can override without
        // touching source. Fall back to the standard local Aspire dev connection.
        var connectionString =
            Environment.GetEnvironmentVariable("MEEPLITON_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=meepliton;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<LiarsDiceDbContext>()
            .UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_liarsdice"))
            .Options;

        return new LiarsDiceDbContext(options);
    }
}
