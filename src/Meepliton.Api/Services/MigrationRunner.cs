using Meepliton.Api.Data;
using Meepliton.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meepliton.Api.Services;

public class MigrationRunner(
    PlatformDbContext platformContext,
    IEnumerable<IGameDbContext> gameContexts,
    ILogger<MigrationRunner> logger)
{
    public async Task RunAllAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Applying platform migrations...");
        await platformContext.Database.MigrateAsync(ct);
        logger.LogInformation("Platform migrations complete.");

        foreach (var ctx in gameContexts.OrderBy(g => g.GameId))
        {
            logger.LogInformation("Applying migrations for {GameId}...", ctx.GameId);
            await ctx.MigrateAsync(ct);
            logger.LogInformation("Migrations complete for {GameId}.", ctx.GameId);
        }
    }
}
