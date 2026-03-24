using Meepliton.Api.Data;
using Meepliton.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Meepliton.Api.Services;

public class MigrationRunner(
    PlatformDbContext platformContext,
    IEnumerable<IGameDbContext> gameContexts,
    ILogger<MigrationRunner> logger)
{
    public virtual async Task RunAllAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Applying platform migrations...");
        await SafeMigrateAsync(() => platformContext.Database.MigrateAsync(ct), "Platform");
        logger.LogInformation("Platform migrations complete.");

        foreach (var ctx in gameContexts.OrderBy(g => g.GameId))
        {
            logger.LogInformation("Applying migrations for {GameId}...", ctx.GameId);
            await SafeMigrateAsync(() => ctx.MigrateAsync(ct), ctx.GameId);
            logger.LogInformation("Migrations complete for {GameId}.", ctx.GameId);
        }
    }

    private async Task SafeMigrateAsync(Func<Task> migrateAction, string contextName)
    {
        try
        {
            await migrateAction();
        }
        catch (Exception ex) when (IsTableAlreadyExistsError(ex))
        {
            logger.LogWarning("Migration attempted to create table that already exists for {ContextName}. " +
                             "This typically happens when the database is partially migrated. " +
                             "Continuing with application startup - you may need to manually clean up the migration state if issues persist. " +
                             "Exception type: {ExceptionType}, Message: {Message}", 
                             contextName, ex.GetType().Name, GetRootExceptionMessage(ex));

            // Don't rethrow - continue with the application startup
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration failed for {ContextName}. Exception type: {ExceptionType}", contextName, ex.GetType().Name);
            throw; // Rethrow other exceptions as they indicate real problems
        }
    }

    private static bool IsTableAlreadyExistsError(Exception ex)
    {
        // Check the entire exception chain for various table/relation exists errors
        var current = ex;
        while (current != null)
        {
            // Check for PostgresException with "relation already exists" error
            if (current is PostgresException pgEx && pgEx.SqlState == "42P07")
            {
                return true;
            }

            // Check for common error messages that indicate table already exists
            var message = current.Message;
            if (message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("42P07", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("relation \"action_log\" already exists", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("CREATE TABLE action_log", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check for EF Core specific migration errors related to existing objects
            if (current.GetType().Name.Contains("Migration", StringComparison.OrdinalIgnoreCase) ||
                current.GetType().Name.Contains("Database", StringComparison.OrdinalIgnoreCase))
            {
                if (message.Contains("action_log", StringComparison.OrdinalIgnoreCase) &&
                    (message.Contains("CREATE", StringComparison.OrdinalIgnoreCase) || 
                     message.Contains("exists", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            current = current.InnerException;
        }

        return false;
    }

    private static string GetRootExceptionMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException != null)
        {
            current = current.InnerException;
        }
        return current.Message;
    }
}
