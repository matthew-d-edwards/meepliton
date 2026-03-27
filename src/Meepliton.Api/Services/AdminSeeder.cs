using Meepliton.Api.Identity;
using Microsoft.AspNetCore.Identity;

namespace Meepliton.Api.Services;

/// <summary>
/// Ensures the Admin role exists and optionally grants it to a seed user.
/// Runs in ALL environments on startup.
///
/// Configuration:
///   AdminSeedEmail (env var: ADMIN_SEED_EMAIL) — if set, the user with this email
///   is promoted to the Admin role. If not set, the role is still created but no
///   user is promoted.
/// </summary>
public class AdminSeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<AdminSeeder> logger)
{
    public async Task SeedAsync()
    {
        // 1. Ensure the Admin role exists.
        const string adminRole = "Admin";
        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(adminRole));
            if (result.Succeeded)
                logger.LogInformation("[AdminSeeder] Created '{Role}' role.", adminRole);
            else
                logger.LogError("[AdminSeeder] Failed to create '{Role}' role: {Errors}", adminRole,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // 2. Promote seed user if configured.
        var seedEmail = configuration["AdminSeedEmail"];
        if (string.IsNullOrWhiteSpace(seedEmail))
        {
            logger.LogInformation("[AdminSeeder] No AdminSeedEmail configured — skipping user promotion.");
            return;
        }

        var user = await userManager.FindByEmailAsync(seedEmail);
        if (user is null)
        {
            logger.LogWarning("[AdminSeeder] User with email '{Email}' not found — skipping promotion.", seedEmail);
            return;
        }

        if (await userManager.IsInRoleAsync(user, adminRole))
        {
            logger.LogInformation("[AdminSeeder] User '{Email}' is already in the '{Role}' role.", seedEmail, adminRole);
            return;
        }

        var addResult = await userManager.AddToRoleAsync(user, adminRole);
        if (addResult.Succeeded)
            logger.LogInformation("[AdminSeeder] Granted '{Role}' role to '{Email}'.", adminRole, seedEmail);
        else
            logger.LogError("[AdminSeeder] Failed to grant '{Role}' to '{Email}': {Errors}", adminRole, seedEmail,
                string.Join(", ", addResult.Errors.Select(e => e.Description)));
    }
}
