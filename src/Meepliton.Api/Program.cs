using System.Reflection;
using System.Text;
using Meepliton.Api.Data;
using Meepliton.Api.Hubs;
using Meepliton.Api.Identity;
using Meepliton.Api.Services;
using Meepliton.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Meepliton.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Database — platform context
builder.AddNpgsqlDbContext<PlatformDbContext>("meepliton");

// Game DbContexts are discovered and registered automatically via Scrutor below.
// Each game project must be referenced in Meepliton.Api.csproj so its assembly
// is in the build output and Scrutor can find it.

// Identity
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail               = true;
        options.Password.RequiredLength               = 8;
        options.Password.RequireUppercase             = true;
        options.Password.RequireDigit                 = true;
        options.Password.RequireNonAlphanumeric       = false;
        options.Lockout.MaxFailedAccessAttempts       = 5;
        options.Lockout.DefaultLockoutTimeSpan        = TimeSpan.FromMinutes(15);
        options.SignIn.RequireConfirmedEmail           = !builder.Environment.IsDevelopment();
    })
    .AddEntityFrameworkStores<PlatformDbContext>()
    .AddDefaultTokenProviders();

// Authentication
// NOTE: AddIdentity (above) registers its own DefaultAuthenticateScheme (Identity cookies).
// We must explicitly override DefaultAuthenticateScheme here so requests are authenticated
// via JWT Bearer, not the Identity cookie middleware.
var authenticationBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"]!;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
        options.Events = new JwtBearerEvents
        {
            // Accept token from HttpOnly cookie (REST) or query string (SignalR)
            OnMessageReceived = ctx =>
            {
                // Cookie auth for REST endpoints
                if (ctx.Request.Cookies.TryGetValue("meepliton_session", out var cookieToken)
                    && !string.IsNullOrEmpty(cookieToken))
                {
                    ctx.Token = cookieToken;
                    return Task.CompletedTask;
                }

                // Query-string token for SignalR hub connections
                var queryToken = ctx.Request.Query["access_token"].ToString();
                var path       = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(queryToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = queryToken;

                return Task.CompletedTask;
            }
        };
    });

// Only add Google OAuth if configuration is provided
var googleClientId = builder.Configuration["Auth:Google:ClientId"];
var googleClientSecret = builder.Configuration["Auth:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authenticationBuilder.AddGoogle(options =>
    {
        options.ClientId     = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.ClaimActions.MapJsonKey("picture", "picture");
    });
}

builder.Services.AddAuthorization();

// Configure JSON to serialize enums as strings so the frontend can compare status values by name
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opts =>
    opts.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// SignalR
builder.Services.AddSignalR();

// Game modules via Scrutor auto-discovery.
// FromApplicationDependencies ensures game assemblies are loaded from the dependency
// manifest rather than relying on them already being in the AppDomain at startup.
builder.Services.Scan(scan => scan
    .FromApplicationDependencies(a => a.FullName?.StartsWith("Meepliton.Games.") == true)
    .AddClasses(c => c.AssignableTo<IGameModule>())
    .As<IGameModule>()
    .WithSingletonLifetime()
    .AddClasses(c => c.AssignableTo<IGameHandler>())
    .As<IGameHandler>()
    .WithSingletonLifetime());

// Register game DbContexts via AddDbContext<T> so DbContextOptions<T> is available in DI.
// Scrutor cannot set this up — only AddDbContext<T>() registers the options factory.
// Game assemblies are already loaded by the Scrutor scan above.
{
    var addDbContextMethod = typeof(EntityFrameworkServiceCollectionExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .First(m => m.Name == "AddDbContext"
                 && m.IsGenericMethodDefinition
                 && m.GetGenericArguments().Length == 1
                 && m.GetParameters().Length == 4
                 && m.GetParameters()[1].ParameterType == typeof(Action<DbContextOptionsBuilder>));

    var gameDbContextTypes = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.FullName?.StartsWith("Meepliton.Games.") == true)
        .SelectMany(a => a.GetTypes())
        .Where(t => !t.IsAbstract
                 && typeof(IGameDbContext).IsAssignableFrom(t)
                 && typeof(DbContext).IsAssignableFrom(t))
        .ToList();

    foreach (var dbCtxType in gameDbContextTypes)
    {
        addDbContextMethod.MakeGenericMethod(dbCtxType)
            .Invoke(null, new object?[] { builder.Services, null, ServiceLifetime.Scoped, ServiceLifetime.Singleton });
        // Also register under IGameDbContext so MigrationRunner can inject IEnumerable<IGameDbContext>
        var capturedType = dbCtxType;
        builder.Services.AddScoped<IGameDbContext>(
            sp => (IGameDbContext)sp.GetRequiredService(capturedType));
    }
}

// Email sender — SendGrid when API key is present; console logging for local dev / CI
if (!string.IsNullOrEmpty(builder.Configuration["SENDGRID_API_KEY"]))
    builder.Services.AddTransient<IEmailSender<ApplicationUser>, SendGridEmailSender>();
else
    builder.Services.AddTransient<IEmailSender<ApplicationUser>, LoggingEmailSender>();

// Platform services
builder.Services.AddScoped<MigrationRunner>();
builder.Services.AddScoped<GameDispatcher>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<DevSeeder>();
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
            // Allow any localhost origin so the Vite dev server works regardless of which
            // dynamic port Aspire assigns it on each run.
            policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        else
            policy.WithOrigins("https://meepliton.com")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
    }));

var app = builder.Build();

// Startup migrations
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("=== STARTUP: Beginning migration and seeding process ===");

    var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
    await runner.RunAllAsync();

    if (app.Environment.IsDevelopment())
    {
        logger.LogInformation("=== STARTUP: Environment is Development, running DevSeeder ===");
        try 
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DevSeeder>();
            logger.LogInformation("=== STARTUP: DevSeeder retrieved from DI, calling SeedAsync ===");
            await seeder.SeedAsync();
            logger.LogInformation("=== STARTUP: DevSeeder completed ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "=== STARTUP: DevSeeder failed with exception ===");
            throw;
        }
    }
    else
    {
        logger.LogInformation("=== STARTUP: Environment is {Environment}, skipping DevSeeder ===", app.Environment.EnvironmentName);
    }

    logger.LogInformation("=== STARTUP: Migration and seeding process complete ===");
}

// Middleware
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapDefaultEndpoints();
app.MapAuthEndpoints();
app.MapRoomEndpoints();
app.MapHub<GameHub>("/hubs/game");

app.Run();

// Required for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
