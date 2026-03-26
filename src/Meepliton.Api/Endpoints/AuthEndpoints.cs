using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Meepliton.Api.Helpers;
using Meepliton.Api.Identity;
using Meepliton.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Meepliton.Api.Endpoints;

public static class AuthEndpoints
{
    private const string CookieName = "meepliton_session";
    private static readonly string[] AllowedThemes = ["light", "dark", "system"];

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (
            RegisterRequest req,
            UserManager<ApplicationUser> userManager,
            IEmailSender<ApplicationUser> emailSender,
            IConfiguration configuration) =>
        {
            var nameError = ValidateDisplayName(req.DisplayName);
            if (nameError is not null)
                return Results.ValidationProblem(
                    new Dictionary<string, string[]> { ["displayName"] = [nameError] });

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

        // Me — GET
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

        // Me — PUT
        group.MapPut("/me", async (
            HttpContext ctx,
            UserManager<ApplicationUser> userManager) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? ctx.User.FindFirstValue("sub");

            if (userId is null) return Results.Unauthorized();

            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return Results.Unauthorized();

            // Read body as a JsonDocument so we can distinguish absent fields from explicit null.
            UpdateMeRequest req;
            try
            {
                using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
                req = UpdateMeRequest.Parse(doc.RootElement);
            }
            catch (Exception)
            {
                return Results.BadRequest(new { message = "Invalid JSON body." });
            }

            var errors = new Dictionary<string, string[]>();

            if (req.DisplayName is not null)
            {
                var nameError = ValidateDisplayName(req.DisplayName);
                if (nameError is not null)
                    errors["displayName"] = [nameError];
            }

            // avatarUrl: absent → no change.
            //            null   → clear the override (fall back to Gravatar/Google default).
            //            string → must be an absolute HTTPS URL.
            if (req.AvatarUrlPresent && req.AvatarUrl is not null)
            {
                if (!Uri.TryCreate(req.AvatarUrl, UriKind.Absolute, out var uri) ||
                    uri.Scheme != Uri.UriSchemeHttps)
                {
                    errors["avatarUrl"] = ["avatarUrl must be an absolute HTTPS URL or null."];
                }
            }

            if (req.Theme is not null && !AllowedThemes.Contains(req.Theme))
                errors["theme"] = [$"theme must be one of: {string.Join(", ", AllowedThemes)}."];

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            if (req.DisplayName is not null)
                user.DisplayName = req.DisplayName;

            if (req.AvatarUrlPresent)
                user.AvatarUrl = req.AvatarUrl; // null clears override; HTTPS URL sets it

            if (req.Theme is not null)
                user.Theme = req.Theme;

            await userManager.UpdateAsync(user);
            return Results.NoContent();
        }).RequireAuthorization();

        // Login-methods — GET /api/auth/me/login-methods
        group.MapGet("/me/login-methods", async (
            HttpContext ctx,
            UserManager<ApplicationUser> userManager) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? ctx.User.FindFirstValue("sub");
            if (userId is null) return Results.Unauthorized();

            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return Results.Unauthorized();

            var loginMethods = new List<string>();

            if (!string.IsNullOrEmpty(user.PasswordHash))
                loginMethods.Add("password");

            var logins = await userManager.GetLoginsAsync(user);
            if (logins.Any(l => l.LoginProvider == "Google"))
                loginMethods.Add("google");

            return Results.Ok(new { loginMethods });
        }).RequireAuthorization();

        // Add password — POST /api/auth/add-password
        group.MapPost("/add-password", async (
            AddPasswordRequest req,
            HttpContext ctx,
            UserManager<ApplicationUser> userManager) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? ctx.User.FindFirstValue("sub");
            if (userId is null) return Results.Unauthorized();

            var user = await userManager.FindByIdAsync(userId);
            if (user is null) return Results.Unauthorized();

            if (!string.IsNullOrEmpty(user.PasswordHash))
                return Results.BadRequest(new { message = "An account password is already set." });

            var result = await userManager.AddPasswordAsync(user, req.NewPassword);
            if (!result.Succeeded)
            {
                var errors = result.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
                return Results.ValidationProblem(errors);
            }

            return Results.NoContent();
        }).RequireAuthorization();

        // Link Google — initiate — GET /api/auth/link-google
        group.MapGet("/link-google", (HttpContext ctx) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? ctx.User.FindFirstValue("sub");
            if (userId is null) return Results.Unauthorized();

            var properties = new AuthenticationProperties
            {
                RedirectUri = "/api/auth/link-google/callback",
                Items       = { ["userId"] = userId },
            };
            return Results.Challenge(properties, ["Google"]);
        }).RequireAuthorization();

        // Link Google — callback — GET /api/auth/link-google/callback
        // No .RequireAuthorization() here: this is an OAuth redirect from Google, so there
        // is no JWT cookie on the request. The userId is carried in the encrypted external
        // auth state cookie (AuthenticationProperties.Items["userId"]) set during the
        // /link-google initiation — it cannot be forged without the server's data-protection keys.
        group.MapGet("/link-google/callback", async (
            HttpContext ctx,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration) =>
        {
            var frontendBase = configuration["Frontend:BaseUrl"] ?? "https://meepliton.com";

            var authResult = await ctx.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (!authResult.Succeeded || authResult.Principal is null)
                return Results.Redirect($"{frontendBase}/account?error=google_failed");

            var userId = authResult.Properties?.Items["userId"];
            if (userId is null)
                return Results.Redirect($"{frontendBase}/account?error=google_failed");

            var user = await userManager.FindByIdAsync(userId);
            if (user is null)
                return Results.Redirect($"{frontendBase}/account?error=google_failed");

            var loginProvider       = authResult.Principal.Identity?.AuthenticationType ?? "Google";
            var providerKey         = authResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
                                   ?? string.Empty;
            var providerDisplayName = authResult.Principal.FindFirstValue(ClaimTypes.Name) ?? "Google";

            // Check if this Google account is already linked to a different user
            var existingUser = await userManager.FindByLoginAsync("Google", providerKey);
            if (existingUser is not null && existingUser.Id != userId)
                return Results.Redirect($"{frontendBase}/account?error=google_already_linked");

            var info = new UserLoginInfo("Google", providerKey, providerDisplayName);
            var result = await userManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
                return Results.Redirect($"{frontendBase}/account?error=google_failed");

            return Results.Redirect($"{frontendBase}/account?linked=google");
        });

        // Google OAuth — initiate
        group.MapGet("/google", (HttpContext ctx) =>
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = "/api/auth/google/callback",
            };
            return Results.Challenge(properties, ["Google"]);
        });

        // Google OAuth — callback
        group.MapGet("/google/callback", async (
            HttpContext ctx,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            TokenService tokenService,
            IHostEnvironment env,
            IConfiguration configuration) =>
        {
            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info is null)
            {
                var frontendBase = configuration["Frontend:BaseUrl"] ?? "https://meepliton.com";
                return Results.Redirect($"{frontendBase}/login?error=google_failed");
            }

            // Try to sign in with the external login
            var result = await signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            ApplicationUser user;

            if (!result.Succeeded)
            {
                // First-time Google sign-in — create a new account
                var email       = info.Principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
                var displayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;
                var pictureUrl  = info.Principal.FindFirstValue("picture");

                user = new ApplicationUser
                {
                    UserName       = email,
                    Email          = email,
                    DisplayName    = displayName,
                    AvatarUrl      = pictureUrl,
                    EmailConfirmed = true, // Google has already verified it
                };

                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    var frontendBase = configuration["Frontend:BaseUrl"] ?? "https://meepliton.com";
                    return Results.Redirect($"{frontendBase}/login?error=google_failed");
                }

                await userManager.AddLoginAsync(user, info);
            }
            else
            {
                user = (await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey))!;
                // AC-12: Do not overwrite AvatarUrl on subsequent sign-ins.
                // The Google picture was captured on first login; after that the user controls it.
            }

            user.LastSeenAt = DateTimeOffset.UtcNow;
            await userManager.UpdateAsync(user);

            var jwt = tokenService.GenerateToken(user);
            ctx.Response.Cookies.Append(CookieName, jwt, BuildCookieOptions(env));

            var frontendUrl = configuration["Frontend:BaseUrl"] ?? "https://meepliton.com";
            return Results.Redirect($"{frontendUrl}/lobby");
        });
    }

    private static CookieOptions BuildCookieOptions(IHostEnvironment env) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure   = !env.IsDevelopment(),
        Expires  = DateTimeOffset.UtcNow.AddDays(30),
        Path     = "/",
    };

    /// <summary>
    /// Returns the effective avatar URL: user-set override, then derived Gravatar URL for
    /// email/password users, then null (Google-only accounts without a stored picture).
    /// </summary>
    private static string? ResolveAvatarUrl(ApplicationUser u) =>
        AvatarHelper.ResolveAvatarUrl(u.AvatarUrl, u.Email);

    private static UserDto ToUserDto(ApplicationUser u) =>
        new(u.Id, u.DisplayName, ResolveAvatarUrl(u), u.Email ?? string.Empty, u.Theme);

    /// <summary>Returns null if valid, or an error message if invalid.</summary>
    private static string? ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName) || displayName.Length < 1)
            return "Display name must be at least 1 character.";
        if (displayName.Length > 32)
            return "Display name must be at most 32 characters.";
        return null;
    }

    record RegisterRequest(string Email, string Password, string DisplayName);
    record ConfirmEmailRequest(string UserId, string Token);
    record ForgotPasswordRequest(string Email);
    record ResetPasswordRequest(string UserId, string Token, string NewPassword);
    record LoginRequest(string Email, string Password);
    record ResendConfirmationRequest(string Email);
    record AddPasswordRequest(string NewPassword);
    record UserDto(string Id, string DisplayName, string? AvatarUrl, string Email, string Theme);

    /// <summary>
    /// PUT /api/auth/me request body.  All fields are optional — omitted fields are not changed.
    /// AvatarUrl distinguishes "absent" (no change) from "null" (clear override).
    /// </summary>
    private sealed class UpdateMeRequest
    {
        public string?  DisplayName    { get; private set; }
        public string?  AvatarUrl      { get; private set; }
        public bool     AvatarUrlPresent { get; private set; }
        public string?  Theme          { get; private set; }

        public static UpdateMeRequest Parse(JsonElement root)
        {
            var req = new UpdateMeRequest();

            if (root.TryGetProperty("displayName", out var dn) &&
                dn.ValueKind == JsonValueKind.String)
                req.DisplayName = dn.GetString();

            if (root.TryGetProperty("avatarUrl", out var av))
            {
                req.AvatarUrlPresent = true;
                req.AvatarUrl = av.ValueKind == JsonValueKind.String ? av.GetString() : null;
            }

            if (root.TryGetProperty("theme", out var th) &&
                th.ValueKind == JsonValueKind.String)
                req.Theme = th.GetString();

            return req;
        }
    }
}
