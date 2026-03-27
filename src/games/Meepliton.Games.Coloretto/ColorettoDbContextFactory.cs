using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Meepliton.Games.Coloretto;

/// <summary>
/// Design-time factory used by the dotnet ef CLI (e.g. "dotnet ef migrations add").
/// Not used at runtime — the app constructs ColorettoDbContext via DI using IConfiguration.
///
/// Connection string is read from the startup project's appsettings.json /
/// appsettings.Development.json, then environment variables. No credentials
/// are hardcoded here.
/// </summary>
public class ColorettoDbContextFactory : IDesignTimeDbContextFactory<ColorettoDbContext>
{
    public ColorettoDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<ColorettoDbContext>()
            .UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_coloretto"))
            .Options;

        return new ColorettoDbContext(options);
    }
}
