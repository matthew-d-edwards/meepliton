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
        var existing = await userManager.FindByEmailAsync(Email);
        if (existing is not null) return;

        var user = new ApplicationUser
        {
            UserName       = Email,
            Email          = Email,
            DisplayName    = DisplayName,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, Password);
        if (result.Succeeded)
            logger.LogInformation("[DEV] Seeded dev user — email: {Email}  password: {Password}", Email, Password);
        else
            logger.LogWarning("[DEV] Failed to seed dev user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}
