using Meepliton.Api.Data;
using Meepliton.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Meepliton.Api.Services;

public class MigrationRunner(
    PlatformDbContext platformContext,
    IEnumerable<IGameDbContext> gameContexts,
    ILogger<MigrationRunner> logger)
{
    public virtual async Task RunAllAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Applying platform migrations...");
        try
        {
            await platformContext.Database.MigrateAsync(ct);
            logger.LogInformation("Platform migrations complete.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Platform migration failed — startup aborted.");
            throw;
        }

        foreach (var ctx in gameContexts.OrderBy(g => g.GameId))
        {
            logger.LogInformation("Applying migrations for game '{GameId}'...", ctx.GameId);
            try
            {
                await ctx.MigrateAsync(ct);
                logger.LogInformation("Migrations complete for game '{GameId}'.", ctx.GameId);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Migration failed for game '{GameId}' — startup aborted.", ctx.GameId);
                throw;
            }
        }
    }
}
