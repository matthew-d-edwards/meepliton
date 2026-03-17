// Story-007: Integration tests for GET/PUT /api/auth/me
//
// These tests spin up the full API in-process using WebApplicationFactory<Program>
// with an EF Core InMemory database substituted for PostgreSQL.
// Authentication uses JWT bearer tokens minted directly by the same TokenService
// the production app uses.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Meepliton.Api.Data;
using Meepliton.Api.Identity;
using Meepliton.Api.Services;
using Meepliton.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Meepliton.Tests.Integration;

/// <summary>
/// A MigrationRunner that does nothing — used in tests to avoid calling
/// MigrateAsync() on the EF InMemory provider, which doesn't support migrations.
/// EF InMemory creates the schema automatically on first use.
/// </summary>
internal sealed class NoOpMigrationRunner(
    PlatformDbContext platformContext,
    IEnumerable<IGameDbContext> gameContexts,
    ILogger<MigrationRunner> logger)
    : MigrationRunner(platformContext, gameContexts, logger)
{
    public override Task RunAllAsync(CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// WebApplicationFactory that replaces Postgres with EF Core InMemory so tests
/// can run without a live database. The appsettings.json JWT settings are reused
/// as-is — TokenService and JwtBearer both read from the same IConfiguration, so
/// tokens minted in-process are always valid for the in-process validator.
/// </summary>
public class AuthApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove any PlatformDbContext registrations registered by the app
            // (Aspire's AddNpgsqlDbContext or standard AddDbContext).
            services.RemoveAll<DbContextOptions<PlatformDbContext>>();
            services.RemoveAll<PlatformDbContext>();

            // Register an in-memory database — unique per factory instance so
            // parallel test classes don't share state.
            services.AddDbContext<PlatformDbContext>(opts =>
                opts.UseInMemoryDatabase("auth-tests-" + Guid.NewGuid()));

            // Replace MigrationRunner with a no-op so the EF InMemory provider
            // doesn't throw "Migrations are not supported by the in-memory store"
            // during the startup RunAllAsync call in Program.cs.
            services.RemoveAll<MigrationRunner>();
            services.AddScoped<MigrationRunner>(sp =>
                new NoOpMigrationRunner(
                    sp.GetRequiredService<PlatformDbContext>(),
                    sp.GetRequiredService<IEnumerable<IGameDbContext>>(),
                    sp.GetRequiredService<ILogger<MigrationRunner>>()));
        });
    }
}

/// <summary>
/// Helpers for creating users, minting JWT tokens, and building HTTP clients
/// that authenticate as a specific user.
/// </summary>
public static class AuthTestHelpers
{
    /// <summary>Creates a confirmed ApplicationUser in the in-process UserManager.</summary>
    public static async Task<ApplicationUser> CreateUserAsync(
        IServiceScope scope,
        string email,
        string displayName,
        string? avatarUrl = null,
        string password = "TestPass1!")
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName       = email,
            Email          = email,
            DisplayName    = displayName,
            AvatarUrl      = avatarUrl,
            EmailConfirmed = true, // skip email-confirmation gate in tests
        };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue(because: string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    /// <summary>Mints a bearer token that the in-process app will accept.</summary>
    public static string MintToken(IServiceScope scope, ApplicationUser user)
    {
        var tokenService = scope.ServiceProvider.GetRequiredService<TokenService>();
        return tokenService.GenerateToken(user);
    }

    /// <summary>
    /// Attaches the given bearer token as an Authorization header.
    /// The app reads the cookie OR the Authorization header — we use the header
    /// so tests don't need to track cookies.
    /// </summary>
    public static void SetBearer(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}

/// <summary>
/// Derives the expected Gravatar URL for an email address the same way the
/// production <c>ResolveAvatarUrl</c> method does.
/// </summary>
public static class GravatarHelper
{
    public static string For(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(normalized)))
                          .ToLowerInvariant();
        return $"https://www.gravatar.com/avatar/{hash}?d=identicon&s=80";
    }
}

/// <summary>
/// Story-007 — Tests for GET /api/auth/me and PUT /api/auth/me.
/// </summary>
public class AuthEndpointTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public AuthEndpointTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/auth/me ──────────────────────────────────────────────────────

    /// <summary>AC-1: Unauthenticated GET returns 401.</summary>
    [Fact]
    public async Task GetMe_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// AC-1 shape: GET returns { id, displayName, avatarUrl, email, theme } when authenticated.
    /// </summary>
    [Fact]
    public async Task GetMe_Authenticated_ReturnsCorrectShape()
    {
        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "shape@test.com", "Shape User");
        var token = AuthTestHelpers.MintToken(scope, user);

        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("id",          out _).Should().BeTrue(because: "response must include 'id'");
        body.TryGetProperty("displayName", out _).Should().BeTrue(because: "response must include 'displayName'");
        body.TryGetProperty("avatarUrl",   out _).Should().BeTrue(because: "response must include 'avatarUrl'");
        body.TryGetProperty("email",       out _).Should().BeTrue(because: "response must include 'email'");
        body.TryGetProperty("theme",       out _).Should().BeTrue(because: "response must include 'theme'");

        body.GetProperty("id").GetString().Should().Be(user.Id);
        body.GetProperty("displayName").GetString().Should().Be("Shape User");
        body.GetProperty("email").GetString().Should().Be("shape@test.com");
        body.GetProperty("theme").GetString().Should().Be("system");
    }

    /// <summary>
    /// AC-10: When user has no custom avatarUrl, GET returns a Gravatar URL
    /// derived from the user's lowercased, trimmed email MD5.
    /// </summary>
    [Fact]
    public async Task GetMe_NoCustomAvatar_ReturnsGravatarUrl()
    {
        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "gravatar@test.com", "Gravatar User", avatarUrl: null);
        var token = AuthTestHelpers.MintToken(scope, user);

        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var avatarUrl = body.GetProperty("avatarUrl").GetString();

        var expected = GravatarHelper.For("gravatar@test.com");
        avatarUrl.Should().Be(expected,
            because: "user without a custom avatar should receive a Gravatar URL derived from their email hash");
    }

    /// <summary>
    /// AC-10/AC-4: When user has a custom avatarUrl stored, GET returns that stored URL.
    /// </summary>
    [Fact]
    public async Task GetMe_WithCustomAvatar_ReturnsStoredAvatarUrl()
    {
        const string customUrl = "https://example.com/my-avatar.png";

        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "custom-avatar@test.com", "Custom Avatar", avatarUrl: customUrl);
        var token = AuthTestHelpers.MintToken(scope, user);

        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("avatarUrl").GetString().Should().Be(customUrl,
            because: "a stored custom avatarUrl must override the Gravatar default");
    }

    // ── PUT /api/auth/me ──────────────────────────────────────────────────────

    /// <summary>AC-2 unauthenticated: PUT returns 401 when not signed in.</summary>
    [Fact]
    public async Task PutMe_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.PutAsJsonAsync("/api/auth/me", new { displayName = "Test" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>AC-2: PUT with valid displayName returns 204.</summary>
    [Fact]
    public async Task PutMe_ValidDisplayName_Returns204AndPersists()
    {
        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "update-name@test.com", "Old Name");
        var token = AuthTestHelpers.MintToken(scope, user);

        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        var response = await client.PutAsJsonAsync("/api/auth/me", new { displayName = "New Name" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Confirm the change is reflected in GET /api/auth/me.
        var getResponse = await client.GetAsync("/api/auth/me");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("displayName").GetString().Should().Be("New Name");
    }

    /// <summary>AC-3: displayName shorter than 1 character returns 400 validation error.</summary>
    [Fact]
    public async Task PutMe_DisplayNameEmpty_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "empty-name@test.com", "Valid Name");
        var token = AuthTestHelpers.MintToken(scope, user);

        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        var response = await client.PutAsJsonAsync("/api/auth/me", new { displayName = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("displayName",
            because: "validation error response must name the offending field");
    }

    /// <summary>AC-3: displayName longer than 32 characters returns 400 validation error.</summary>
    [Fact]
    public async Task PutMe_DisplayNameTooLong_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "long-name@test.com", "Valid Name");
        var token = AuthTestHelpers.MintToken(scope, user);

        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        var tooLong = new string('x', 33);
        var response = await client.PutAsJsonAsync("/api/auth/me", new { displayName = tooLong });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("displayName",
            because: "validation error response must name the offending field");
    }

    /// <summary>AC-4: Sending avatarUrl: null clears the stored override (falls back to Gravatar).</summary>
    [Fact]
    public async Task PutMe_AvatarUrlNull_ClearsOverride()
    {
        const string customUrl = "https://example.com/custom.png";

        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "clear-avatar@test.com", "Clear Avatar", avatarUrl: customUrl);
        var token = AuthTestHelpers.MintToken(scope, user);

        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        // Send explicit null to clear the override.
        var response = await client.PutAsJsonAsync("/api/auth/me", new { avatarUrl = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // After clearing, GET should return the Gravatar URL, not the old custom URL.
        var getResponse = await client.GetAsync("/api/auth/me");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var returnedAvatar = body.GetProperty("avatarUrl").GetString();
        returnedAvatar.Should().NotBe(customUrl,
            because: "sending null for avatarUrl must clear the custom override");
        returnedAvatar.Should().StartWith("https://www.gravatar.com/",
            because: "after clearing the custom override the Gravatar default should be returned");
    }

    /// <summary>AC-4: Non-HTTPS avatarUrl returns 400.</summary>
    [Fact]
    public async Task PutMe_HttpAvatarUrl_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "http-avatar@test.com", "Http Avatar");
        var token = AuthTestHelpers.MintToken(scope, user);

        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        var response = await client.PutAsJsonAsync("/api/auth/me",
            new { avatarUrl = "http://insecure.example.com/pic.png" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("avatarUrl",
            because: "validation error response must name the offending field");
    }

    /// <summary>
    /// Absent fields leave those fields unchanged (only displayName provided — avatarUrl unaffected).
    /// </summary>
    [Fact]
    public async Task PutMe_AbsentField_LeavesOtherFieldsUnchanged()
    {
        const string originalAvatar = "https://cdn.example.com/avatar.png";

        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "absent-field@test.com", "Original Name", avatarUrl: originalAvatar);
        var token = AuthTestHelpers.MintToken(scope, user);

        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        // Only send displayName — avatarUrl is not present in the body at all.
        var response = await client.PutAsJsonAsync("/api/auth/me", new { displayName = "Updated Name" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync("/api/auth/me");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("displayName").GetString().Should().Be("Updated Name");
        body.GetProperty("avatarUrl").GetString().Should().Be(originalAvatar,
            because: "absent avatarUrl field must not clear the stored value");
    }

    // ── POST /api/auth/register — displayName validation ─────────────────────

    /// <summary>
    /// AC-3: POST /api/auth/register enforces the same 1–32 char displayName rule
    /// as PUT /api/auth/me.  Empty displayName must return 400.
    /// </summary>
    [Fact]
    public async Task Register_EmptyDisplayName_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email       = "register-empty@test.com",
            password    = "TestPass1!",
            displayName = "",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("displayName");
    }

    /// <summary>AC-3: POST /api/auth/register rejects displayName > 32 chars.</summary>
    [Fact]
    public async Task Register_TooLongDisplayName_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email       = "register-long@test.com",
            password    = "TestPass1!",
            displayName = new string('y', 33),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("displayName");
    }

    /// <summary>
    /// AC-3: POST /api/auth/register accepts a displayName that is exactly 1 character.
    /// </summary>
    [Fact]
    public async Task Register_SingleCharDisplayName_Succeeds()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email       = "register-one@test.com",
            password    = "TestPass1!",
            displayName = "A",
        });

        // The endpoint returns 201 Created when registration succeeds (even in test env
        // where email confirmation is not required for sign-in, the account is created).
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// AC-3: POST /api/auth/register accepts a displayName that is exactly 32 characters.
    /// </summary>
    [Fact]
    public async Task Register_ThirtyTwoCharDisplayName_Succeeds()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email       = "register-32@test.com",
            password    = "TestPass1!",
            displayName = new string('z', 32),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
