using Meepliton.Api.Identity;
using Microsoft.AspNetCore.Identity;

namespace Meepliton.Api.Services;


/// <summary>
/// Seeds pre-confirmed dev accounts on startup so developers can sign in and
/// play-test multiplayer games immediately without registering or confirming email.
/// Only runs in the Development environment.
///
/// Accounts:
///   bob@dev.local   / BobPass1   (Bob)
///   jan@dev.local   / JanPass1   (Jan)
///   rick@dev.local  / RickPass1  (Rick)
///   matt@dev.local  / MattPass1  (Matt)
/// </summary>
// Emails that receive the Admin role in Development.
// The Admin role itself is created by AdminSeeder (which runs before DevSeeder).
file static class AdminDevAccounts
{
    public static readonly string[] Emails = ["bob@dev.local", "matt@dev.local"];
}

public class DevSeeder(UserManager<ApplicationUser> userManager, ILogger<DevSeeder> logger)
{
    private static readonly (string Email, string Password, string DisplayName)[] Accounts =
    [
        ("bob@dev.local",  "BobPass1",  "Bob"),
        ("jan@dev.local",  "JanPass1",  "Jan"),
        ("rick@dev.local", "RickPass1", "Rick"),
        ("matt@dev.local", "MattPass1", "Matt"),
    ];

    // Keep the old single-user constants so any existing code that references them still compiles.
    public const string Email       = "bob@dev.local";
    public const string Password    = "BobPass1";
    public const string DisplayName = "Bob";

    public async Task SeedAsync()
    {
        foreach (var (email, password, displayName) in Accounts)
        {
            try
            {
                var existing = await userManager.FindByEmailAsync(email);
                if (existing is not null)
                {
                    logger.LogInformation("[DEV] {DisplayName} already exists, skipping.", displayName);
                    continue;
                }

                var user = new ApplicationUser
                {
                    UserName       = email,
                    Email          = email,
                    DisplayName    = displayName,
                    EmailConfirmed = true,
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                    logger.LogInformation("[DEV] ✅ Seeded {DisplayName} — {Email} / {Password}", displayName, email, password);
                else
                    logger.LogError("[DEV] ❌ Failed to seed {DisplayName}: {Errors}", displayName,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[DEV] Exception seeding {DisplayName}", displayName);
            }
        }

        // Grant Admin role to designated dev accounts.
        // AdminSeeder has already ensured the role exists.
        const string adminRole = "Admin";
        foreach (var email in AdminDevAccounts.Emails)
        {
            try
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user is null)
                {
                    logger.LogWarning("[DEV] Skipping Admin grant — user '{Email}' not found.", email);
                    continue;
                }

                if (!await userManager.IsInRoleAsync(user, adminRole))
                {
                    var result = await userManager.AddToRoleAsync(user, adminRole);
                    if (result.Succeeded)
                        logger.LogInformation("[DEV] Granted '{Role}' role to '{Email}'.", adminRole, email);
                    else
                        logger.LogError("[DEV] Failed to grant '{Role}' to '{Email}': {Errors}", adminRole, email,
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[DEV] Exception granting Admin to '{Email}'", email);
            }
        }
    }
}
