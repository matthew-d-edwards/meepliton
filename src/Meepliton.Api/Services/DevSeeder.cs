using Meepliton.Api.Identity;
using Microsoft.AspNetCore.Identity;

namespace Meepliton.Api.Services;

/// <summary>
/// Seeds a pre-confirmed dev user on startup so developers can sign in immediately
/// without registering or confirming an email.
/// Only runs in the Development environment.
/// Credentials: dev@meepliton.local / DevPass1
/// </summary>
public class DevSeeder(UserManager<ApplicationUser> userManager, ILogger<DevSeeder> logger)
{
    public const string Email       = "dev@meepliton.local";
    public const string Password    = "DevPass1";
    public const string DisplayName = "Dev User";

    public async Task SeedAsync()
    {
        try
        {
            logger.LogInformation("[DEV] Starting DevSeeder - checking for existing user with email: {Email}", Email);

            var existing = await userManager.FindByEmailAsync(Email);
            if (existing is not null) 
            {
                logger.LogInformation("[DEV] Dev user already exists, skipping seed.");
                return;
            }

            logger.LogInformation("[DEV] Creating new dev user...");

            var user = new ApplicationUser
            {
                UserName       = Email,
                Email          = Email,
                DisplayName    = DisplayName,
                EmailConfirmed = true,
            };

            var result = await userManager.CreateAsync(user, Password);
            if (result.Succeeded)
            {
                logger.LogInformation("[DEV] ✅ Successfully seeded dev user — email: {Email}  password: {Password}", Email, Password);
            }
            else
            {
                logger.LogError("[DEV] ❌ Failed to seed dev user. Errors: {Errors}",
                    string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Description}")));

                // Log each error individually for better visibility
                foreach (var error in result.Errors)
                {
                    logger.LogError("[DEV] Identity Error - Code: {Code}, Description: {Description}", error.Code, error.Description);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DEV] Exception occurred while seeding dev user");
        }
    }
}
