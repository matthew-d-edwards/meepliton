using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Meepliton.Games.LiarsDice;

/// <summary>
/// Design-time factory used by the dotnet ef CLI (e.g. "dotnet ef migrations add").
/// Not used at runtime — the app constructs LiarsDiceDbContext via DI using IConfiguration.
///
/// Connection string is read from the startup project's appsettings.json /
/// appsettings.Development.json, then environment variables. No credentials
/// are hardcoded here.
/// </summary>
public class LiarsDiceDbContextFactory : IDesignTimeDbContextFactory<LiarsDiceDbContext>
{
    public LiarsDiceDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("meepliton")
            ?? throw new InvalidOperationException(
                "Connection string 'meepliton' not found. " +
                "Add it to appsettings.Development.json or set the " +
                "CONNECTIONSTRINGS__MEEPLITON environment variable.");

        var options = new DbContextOptionsBuilder<LiarsDiceDbContext>()
            .UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_liarsdice"))
            .Options;

        return new LiarsDiceDbContext(options);
    }
}
