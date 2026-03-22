using System.Text;
using Meepliton.Api.Data;
using Meepliton.Games.LiarsDice;
using Meepliton.Games.Skyline;
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

// Game DbContexts — add one line per game, keeping the migrations history table isolated.
// Also add a <ProjectReference> in Meepliton.Api.csproj for each new game.
builder.AddNpgsqlDbContext<LiarsDiceDbContext>("meepliton",
    npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_liarsdice"));
builder.AddNpgsqlDbContext<SkylineDbContext>("meepliton",
    npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_skyline"));

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
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme          = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
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
    })
    .AddGoogle(options =>
    {
        options.ClientId     = builder.Configuration["Auth:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Auth:Google:ClientSecret"]!;
        options.ClaimActions.MapJsonKey("picture", "picture");
    });

builder.Services.AddAuthorization();

// SignalR
builder.Services.AddSignalR();

// Game modules via Scrutor auto-discovery
builder.Services.Scan(scan => scan
    .FromAssemblies(AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.FullName?.StartsWith("Meepliton.Games.") == true))
    .AddClasses(c => c.AssignableTo<IGameModule>())
    .AsImplementedInterfaces()
    .WithSingletonLifetime()
    .AddClasses(c => c.AssignableTo<IGameHandler>())
    .AsImplementedInterfaces()
    .WithSingletonLifetime()
    .AddClasses(c => c.AssignableTo<IGameDbContext>())
    .AsImplementedInterfaces()
    .WithScopedLifetime());

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
        policy.WithOrigins("http://localhost:5173", "https://meepliton.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

var app = builder.Build();

// Startup migrations
using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
    await runner.RunAllAsync();

    if (app.Environment.IsDevelopment())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DevSeeder>();
        await seeder.SeedAsync();
    }
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
