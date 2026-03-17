using System.Security.Claims;
using System.Text;
using Meepliton.Api.Identity;
using Meepliton.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Meepliton.Api.Endpoints;

public static class AuthEndpoints
{
    private const string CookieName = "meepliton_session";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (
            RegisterRequest req,
            UserManager<ApplicationUser> userManager,
            IEmailSender<ApplicationUser> emailSender,
            IConfiguration configuration) =>
        {
            var user = new ApplicationUser
            {
                UserName    = req.Email,
                Email       = req.Email,
                DisplayName = req.DisplayName,
            };
            var result = await userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
            {
                // Do not reveal whether the email already exists — return a generic message
                // for duplicate-email errors to prevent account enumeration.
                var errors = result.Errors
                    .Select(e => e.Code is "DuplicateUserName" or "DuplicateEmail"
                        ? new { Code = "RegistrationFailed", Description = "Registration failed. Please try again." }
                        : new { Code = e.Code, Description = e.Description })
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).Distinct().ToArray());
                return Results.ValidationProblem(errors);
            }

            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var frontendBase = configuration["Frontend:BaseUrl"] ?? "https://meepliton.com";
            var confirmationLink = $"{frontendBase}/confirm-email?userId={user.Id}&token={encodedToken}";

            await emailSender.SendConfirmationLinkAsync(user, req.Email, confirmationLink);

            return Results.Created($"/api/auth/me", null);
        });

        group.MapPost("/confirm-email", async (
            ConfirmEmailRequest req,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(req.UserId);
            if (user is null) return Results.NotFound();

            var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.Token));
            var result = await userManager.ConfirmEmailAsync(user, token);
            return result.Succeeded ? Results.NoContent() : Results.BadRequest(result.Errors);
        });

        group.MapPost("/resend-confirmation", async (
            ResendConfirmationRequest req,
            UserManager<ApplicationUser> userManager,
            IEmailSender<ApplicationUser> emailSender,
            IConfiguration configuration) =>
        {
            // Always return 204 — no email enumeration
            var user = await userManager.FindByEmailAsync(req.Email);
            if (user is not null && !await userManager.IsEmailConfirmedAsync(user))
            {
                var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var frontendBase = configuration["Frontend:BaseUrl"] ?? "https://meepliton.com";
                var confirmationLink = $"{frontendBase}/confirm-email?userId={user.Id}&token={encodedToken}";
                await emailSender.SendConfirmationLinkAsync(user, req.Email, confirmationLink);
            }
            return Results.NoContent();
        });

        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest req,
            UserManager<ApplicationUser> userManager,
            IEmailSender<ApplicationUser> emailSender,
            IConfiguration configuration) =>
        {
            // Always return 204 — no email enumeration
            var user = await userManager.FindByEmailAsync(req.Email);
            if (user is not null && await userManager.IsEmailConfirmedAsync(user))
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var frontendBase = configuration["Frontend:BaseUrl"] ?? "https://meepliton.com";
                var resetLink = $"{frontendBase}/reset-password?userId={user.Id}&token={encodedToken}";
                await emailSender.SendPasswordResetLinkAsync(user, req.Email, resetLink);
            }
            return Results.NoContent();
        });

        group.MapPost("/reset-password", async (
            ResetPasswordRequest req,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(req.UserId);
            if (user is null) return Results.NoContent(); // silent
            var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.Token));
            var result = await userManager.ResetPasswordAsync(user, token, req.NewPassword);
            return result.Succeeded ? Results.NoContent() : Results.BadRequest(result.Errors);
        });

        // Login
        group.MapPost("/login", async (
            LoginRequest req,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            TokenService tokenService,
            HttpContext ctx,
            IHostEnvironment env) =>
        {
            var user = await userManager.FindByEmailAsync(req.Email);

            if (user is null)
                return Results.Json(new { message = "Incorrect email or password" }, statusCode: 401);

            if (await userManager.IsLockedOutAsync(user))
            {
                var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
                var unlockTime = lockoutEnd?.UtcDateTime.ToString("o") ?? string.Empty;
                return Results.Json(
                    new { message = $"Too many attempts, try again at {unlockTime}", code = "locked", retryAfter = unlockTime },
                    statusCode: 429);
            }

            var result = await signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
                var unlockTime = lockoutEnd?.UtcDateTime.ToString("o") ?? string.Empty;
                return Results.Json(
                    new { message = $"Too many attempts, try again at {unlockTime}", code = "locked", retryAfter = unlockTime },
                    statusCode: 429);
            }

            if (result.IsNotAllowed)
            {
                return Results.Json(
                    new { message = "Please confirm your email. Check your inbox.", code = "unconfirmed" },
                    statusCode: 403);
            }

            if (!result.Succeeded)
                return Results.Json(new { message = "Incorrect email or password" }, statusCode: 401);

            var jwt = tokenService.GenerateToken(user);
            ctx.Response.Cookies.Append(CookieName, jwt, BuildCookieOptions(env));

            user.LastSeenAt = DateTimeOffset.UtcNow;
            await userManager.UpdateAsync(user);

            return Results.Ok(ToUserDto(user));
        });

        // Logout
        group.MapPost("/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
            return Results.NoContent();
        });

        // Me
        group.MapGet("/me", async (
            HttpContext ctx,
            UserManager<ApplicationUser> userManager,
            TokenService tokenService,
            IHostEnvironment env) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? ctx.User.FindFirstValue("sub");

            if (userId is null) return Results.Unauthorized();

            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return Results.Unauthorized();

            var jwt = tokenService.GenerateToken(user);
            ctx.Response.Cookies.Append(CookieName, jwt, BuildCookieOptions(env));

            user.LastSeenAt = DateTimeOffset.UtcNow;
            await userManager.UpdateAsync(user);

            return Results.Ok(ToUserDto(user));
        }).RequireAuthorization();
    }

    private static CookieOptions BuildCookieOptions(IHostEnvironment env) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure   = !env.IsDevelopment(),
        Expires  = DateTimeOffset.UtcNow.AddDays(30),
        Path     = "/",
    };

    private static UserDto ToUserDto(ApplicationUser u) =>
        new(u.Id, u.DisplayName, u.AvatarUrl, u.Email ?? string.Empty, u.Theme);

    record RegisterRequest(string Email, string Password, string DisplayName);
    record ConfirmEmailRequest(string UserId, string Token);
    record ForgotPasswordRequest(string Email);
    record ResetPasswordRequest(string UserId, string Token, string NewPassword);
    record LoginRequest(string Email, string Password);
    record ResendConfirmationRequest(string Email);
    record UserDto(string Id, string DisplayName, string? AvatarUrl, string Email, string Theme);
}
