// Story-005: Integration tests for account-linking endpoints
//
// Covers:
//   GET  /api/auth/me/login-methods
//   POST /api/auth/add-password
//   GET  /api/auth/link-google
//   GET  /api/auth/link-google/callback

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using Meepliton.Api.Data;
using Meepliton.Api.Identity;
using Meepliton.Api.Services;
using Meepliton.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Meepliton.Tests.Integration;

// ---------------------------------------------------------------------------
// Fake Google external-auth handler
// ---------------------------------------------------------------------------

/// <summary>
/// A fake authentication handler that acts as the "Google" external scheme so
/// that /api/auth/link-google/callback can be exercised in-process without a
/// real OAuth round-trip.
///
/// Tests configure what result the handler returns by storing a
/// <see cref="FakeGoogleAuthResult"/> in the factory's <see cref="FakeGoogleState"/>.
/// </summary>
public class FakeGoogleState
{
    public ClaimsPrincipal? Principal { get; set; }
    public string?          UserId    { get; set; }
    public bool             Fail      { get; set; }
}

public class FakeGoogleAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory                               logger,
    UrlEncoder                                   encoder,
    FakeGoogleState                              state)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (state.Fail || state.Principal is null)
            return Task.FromResult(AuthenticateResult.Fail("Fake Google failure"));

        var properties = new AuthenticationProperties();
        if (state.UserId is not null)
            properties.Items["userId"] = state.UserId;

        var ticket = new AuthenticationTicket(state.Principal, properties, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode  = 302;
        Response.Headers.Location = "/fake-google-login";
        return Task.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// WebApplicationFactory — extends AuthApiFactory with a fake Google scheme
// ---------------------------------------------------------------------------

/// <summary>
/// Extends the existing <see cref="AuthApiFactory"/> by registering a fake
/// Google (and IdentityConstants.ExternalScheme) handler that can be
/// controlled per-test through <see cref="FakeGoogleState"/>.
/// </summary>
public class AccountLinkingApiFactory : WebApplicationFactory<Program>
{
    public FakeGoogleState GoogleState { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Replace Postgres with in-memory DB.
            // Remove ALL registrations added by Aspire's AddNpgsqlDbContext (which calls
            // AddDbContextPool). AddDbContextPool registers several internal EF Core singletons
            // (IDbContextPool, IScopedDbContextLease, …) that must all be removed together
            // before re-registering with AddDbContext for the InMemory provider.
            var efDescriptors = services
                .Where(d => d.ServiceType == typeof(PlatformDbContext) ||
                            d.ServiceType == typeof(DbContextOptions<PlatformDbContext>) ||
                            (d.ServiceType.IsGenericType &&
                             d.ServiceType.GenericTypeArguments.Any(t => t == typeof(PlatformDbContext))))
                .ToList();
            foreach (var d in efDescriptors) services.Remove(d);
            // IMPORTANT: capture the name before the lambda so every DI scope (test scope
            // and each request scope) resolves the same in-memory database.
            var dbName = "account-linking-" + Guid.NewGuid();
            services.AddDbContext<PlatformDbContext>(opts =>
                opts.UseInMemoryDatabase(dbName));

            // Suppress EF migrations on startup
            services.RemoveAll<MigrationRunner>();
            services.AddScoped<MigrationRunner>(sp =>
                new NoOpMigrationRunner(
                    sp.GetRequiredService<PlatformDbContext>(),
                    sp.GetRequiredService<IEnumerable<IGameDbContext>>(),
                    sp.GetRequiredService<ILogger<MigrationRunner>>()));

            // Register the shared fake state
            services.AddSingleton(GoogleState);

            // Register the fake Google handler in DI so the scheme provider can resolve it.
            services.AddTransient<FakeGoogleAuthHandler>();

            // "Google" is not registered by Program.cs (config keys are empty in tests),
            // so we can add it as a new scheme.
            services
                .AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, FakeGoogleAuthHandler>("Google", _ => { });

            // IdentityConstants.ExternalScheme ("Identity.External") is already registered by
            // AddIdentity in Program.cs. We cannot call AddScheme again (it throws), so we
            // replace its handler type by mutating the existing AuthenticationSchemeBuilder.
            services.Configure<AuthenticationOptions>(opts =>
            {
                var existing = opts.Schemes.FirstOrDefault(s => s.Name == IdentityConstants.ExternalScheme);
                if (existing != null)
                    existing.HandlerType = typeof(FakeGoogleAuthHandler);
                else
                    opts.AddScheme(IdentityConstants.ExternalScheme,
                        s => s.HandlerType = typeof(FakeGoogleAuthHandler));
            });
        });
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Story-005 — Tests for the account-linking endpoints.
/// </summary>
public class AccountLinkingEndpointTests : IClassFixture<AccountLinkingApiFactory>
{
    private readonly AccountLinkingApiFactory _factory;

    public AccountLinkingEndpointTests(AccountLinkingApiFactory factory)
    {
        _factory = factory;
    }

    // ==========================================================================
    // GET /api/auth/me/login-methods
    // ==========================================================================

    /// <summary>Unauthenticated request returns 401.</summary>
    [Fact]
    public async Task GetLoginMethods_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/auth/me/login-methods");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Email/password user sees only "password" in login methods.</summary>
    [Fact]
    public async Task GetLoginMethods_PasswordUser_ReturnsPasswordOnly()
    {
        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "methods-pw@test.com", "PwUser");
        var token = AuthTestHelpers.MintToken(scope, user);

        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        var response = await client.GetAsync("/api/auth/me/login-methods");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var methods = body.GetProperty("loginMethods")
                          .EnumerateArray()
                          .Select(e => e.GetString())
                          .ToList();

        methods.Should().ContainSingle().Which.Should().Be("password");
    }

    /// <summary>Google-only user (no password hash) sees only "google".</summary>
    [Fact]
    public async Task GetLoginMethods_GoogleOnlyUser_ReturnsGoogleOnly()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Create user without a password — simulates a Google-only account.
        var user = new ApplicationUser
        {
            UserName       = "methods-google@test.com",
            Email          = "methods-google@test.com",
            DisplayName    = "GoogleUser",
            EmailConfirmed = true,
        };
        var createResult = await userManager.CreateAsync(user); // no password
        createResult.Succeeded.Should().BeTrue();

        // Add a Google external login to this user
        var loginInfo = new UserLoginInfo("Google", "google-sub-001", "GoogleUser");
        await userManager.AddLoginAsync(user, loginInfo);

        var token = AuthTestHelpers.MintToken(scope, user);
        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        var response = await client.GetAsync("/api/auth/me/login-methods");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var methods = body.GetProperty("loginMethods")
                          .EnumerateArray()
                          .Select(e => e.GetString())
                          .ToList();

        methods.Should().ContainSingle().Which.Should().Be("google");
    }

    /// <summary>Linked account (password + Google) sees both methods.</summary>
    [Fact]
    public async Task GetLoginMethods_LinkedAccount_ReturnsBothMethods()
    {
        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "methods-both@test.com", "BothUser");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var loginInfo   = new UserLoginInfo("Google", "google-sub-002", "BothUser");
        await userManager.AddLoginAsync(user, loginInfo);

        var token = AuthTestHelpers.MintToken(scope, user);
        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        var response = await client.GetAsync("/api/auth/me/login-methods");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var methods = body.GetProperty("loginMethods")
                          .EnumerateArray()
                          .Select(e => e.GetString())
                          .ToList();

        methods.Should().Contain("password").And.Contain("google");
        methods.Should().HaveCount(2);
    }

    // ==========================================================================
    // POST /api/auth/add-password
    // ==========================================================================

    /// <summary>Unauthenticated request returns 401.</summary>
    [Fact]
    public async Task AddPassword_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.PostAsJsonAsync("/api/auth/add-password",
            new { newPassword = "NewPass1!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Google-only user can add a password — returns 204.</summary>
    [Fact]
    public async Task AddPassword_GoogleOnlyUser_Returns204()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName       = "add-pw-success@test.com",
            Email          = "add-pw-success@test.com",
            DisplayName    = "NoPwUser",
            EmailConfirmed = true,
        };
        var createResult = await userManager.CreateAsync(user); // no password
        createResult.Succeeded.Should().BeTrue();

        var token = AuthTestHelpers.MintToken(scope, user);
        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        var response = await client.PostAsJsonAsync("/api/auth/add-password",
            new { newPassword = "NewPass1!" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>User who already has a password gets 400 with a message field.</summary>
    [Fact]
    public async Task AddPassword_AlreadyHasPassword_Returns400WithMessage()
    {
        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "add-pw-duplicate@test.com", "DupUser");
        var token = AuthTestHelpers.MintToken(scope, user);

        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        var response = await client.PostAsJsonAsync("/api/auth/add-password",
            new { newPassword = "AnotherPass1!" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("message",
            because: "response must include a 'message' field when password is already set");
        body.Should().Contain("already",
            because: "the message should indicate the password is already set");
    }

    /// <summary>Weak password fails Identity validation — returns 400 (ValidationProblem).</summary>
    [Fact]
    public async Task AddPassword_WeakPassword_Returns400ValidationErrors()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName       = "add-pw-weak@test.com",
            Email          = "add-pw-weak@test.com",
            DisplayName    = "WeakPwUser",
            EmailConfirmed = true,
        };
        var createResult = await userManager.CreateAsync(user); // no password
        createResult.Succeeded.Should().BeTrue();

        var token = AuthTestHelpers.MintToken(scope, user);
        var client = _factory.CreateClient();
        AuthTestHelpers.SetBearer(client, token);

        // "abc" is too short and missing uppercase/digit — violates Identity password rules
        var response = await client.PostAsJsonAsync("/api/auth/add-password",
            new { newPassword = "abc" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ==========================================================================
    // GET /api/auth/link-google
    // ==========================================================================

    /// <summary>Unauthenticated request returns 401.</summary>
    [Fact]
    public async Task LinkGoogle_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/auth/link-google");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Authenticated user receives a redirect (302) toward Google OAuth.</summary>
    [Fact]
    public async Task LinkGoogle_Authenticated_IssuesChallenge302()
    {
        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "link-google-initiate@test.com", "LinkUser");
        var token = AuthTestHelpers.MintToken(scope, user);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        AuthTestHelpers.SetBearer(client, token);

        var response = await client.GetAsync("/api/auth/link-google");

        // A challenge result issues a redirect to the OAuth provider
        response.StatusCode.Should().Be(HttpStatusCode.Redirect,
            because: "a successful challenge should redirect the browser to Google");
    }

    // ==========================================================================
    // GET /api/auth/link-google/callback
    // ==========================================================================

    /// <summary>
    /// Happy path: the external auth succeeds, Google account not yet linked to anyone —
    /// redirects to /account?linked=google.
    /// </summary>
    [Fact]
    public async Task LinkGoogleCallback_Success_RedirectsToLinkedGoogle()
    {
        using var scope = _factory.Services.CreateScope();
        var user  = await AuthTestHelpers.CreateUserAsync(scope, "callback-ok@test.com", "CallbackUser");

        // Configure the fake Google handler to return a successful result
        _factory.GoogleState.Fail      = false;
        _factory.GoogleState.UserId    = user.Id;
        _factory.GoogleState.Principal = BuildGooglePrincipal("google-sub-happy", "Happy User");

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/auth/link-google/callback");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.Should().Contain("linked=google",
            because: "a successful link must redirect to /account?linked=google");
    }

    /// <summary>
    /// Conflict path: Google account already linked to a different user —
    /// redirects to /account?error=google_already_linked.
    /// </summary>
    [Fact]
    public async Task LinkGoogleCallback_GoogleAccountAlreadyLinkedToAnotherUser_RedirectsWithError()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // User A owns the Google sub; user B tries to link the same sub
        var userA = await AuthTestHelpers.CreateUserAsync(scope, "callback-owner@test.com",  "OwnerUser");
        var userB = await AuthTestHelpers.CreateUserAsync(scope, "callback-linker@test.com", "LinkerUser");

        const string sharedGoogleSub = "google-sub-already-taken";
        await userManager.AddLoginAsync(userA, new UserLoginInfo("Google", sharedGoogleSub, "OwnerUser"));

        // Now user B attempts to link the same Google sub
        _factory.GoogleState.Fail      = false;
        _factory.GoogleState.UserId    = userB.Id;
        _factory.GoogleState.Principal = BuildGooglePrincipal(sharedGoogleSub, "Owner User (stolen)");

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/auth/link-google/callback");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.Should().Contain("error=google_already_linked",
            because: "linking a Google account that belongs to another user must redirect with error=google_already_linked");
    }

    /// <summary>
    /// Failure path: external auth fails (e.g. user denied Google OAuth) —
    /// redirects to /account?error=google_failed.
    /// </summary>
    [Fact]
    public async Task LinkGoogleCallback_ExternalAuthFails_RedirectsWithGoogleFailedError()
    {
        _factory.GoogleState.Fail      = true;
        _factory.GoogleState.Principal = null;
        _factory.GoogleState.UserId    = null;

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/auth/link-google/callback");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString();
        location.Should().Contain("error=google_failed",
            because: "when external authentication fails the callback must redirect with error=google_failed");
    }

    // ==========================================================================
    // Helpers
    // ==========================================================================

    /// <summary>Builds a minimal ClaimsPrincipal that looks like a Google identity.</summary>
    private static ClaimsPrincipal BuildGooglePrincipal(string sub, string name)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, sub),
            new Claim(ClaimTypes.Name,           name),
            new Claim(ClaimTypes.Email,          $"{sub}@gmail.com"),
        ],
        authenticationType: "Google");

        return new ClaimsPrincipal(identity);
    }
}
