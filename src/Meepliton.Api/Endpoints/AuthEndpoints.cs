using Meepliton.Api.Identity;
using Microsoft.AspNetCore.Identity;

namespace Meepliton.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (
            RegisterRequest req,
            UserManager<ApplicationUser> userManager,
            IEmailSender<ApplicationUser> emailSender) =>
        {
            var user = new ApplicationUser
            {
                UserName    = req.Email,
                Email       = req.Email,
                DisplayName = req.DisplayName,
            };
            var result = await userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await emailSender.SendConfirmationLinkAsync(user, req.Email, token);

            return Results.Created($"/api/auth/me", null);
        });

        group.MapPost("/confirm-email", async (
            ConfirmEmailRequest req,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(req.UserId);
            if (user is null) return Results.NotFound();
            var result = await userManager.ConfirmEmailAsync(user, req.Token);
            return result.Succeeded ? Results.NoContent() : Results.BadRequest(result.Errors);
        });

        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest req,
            UserManager<ApplicationUser> userManager,
            IEmailSender<ApplicationUser> emailSender) =>
        {
            // Always return 204 — no email enumeration
            var user = await userManager.FindByEmailAsync(req.Email);
            if (user is not null && await userManager.IsEmailConfirmedAsync(user))
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                await emailSender.SendPasswordResetLinkAsync(user, req.Email, token);
            }
            return Results.NoContent();
        });

        group.MapPost("/reset-password", async (
            ResetPasswordRequest req,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(req.UserId);
            if (user is null) return Results.NoContent(); // silent
            var result = await userManager.ResetPasswordAsync(user, req.Token, req.NewPassword);
            return result.Succeeded ? Results.NoContent() : Results.BadRequest(result.Errors);
        });

        group.MapGet("/me", (HttpContext ctx) =>
        {
            // TODO: return UserDto from claims
            return Results.Ok(new { ctx.User.Identity?.Name });
        }).RequireAuthorization();
    }

    record RegisterRequest(string Email, string Password, string DisplayName);
    record ConfirmEmailRequest(string UserId, string Token);
    record ForgotPasswordRequest(string Email);
    record ResetPasswordRequest(string UserId, string Token, string NewPassword);
}
