using Meepliton.Api.Data;
using Meepliton.Api.Hubs;
using Meepliton.Api.Identity;
using Meepliton.Api.Services;
using Meepliton.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Meepliton.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// ── Database ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("meepliton")));

// ── Identity ────────────────────────────────────────────────────────────────
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
        options.SignIn.RequireConfirmedEmail           = true;
    })
    .AddEntityFrameworkStores<PlatformDbContext>()
    .AddDefaultTokenProviders();

// ── Authentication ───────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme          = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            // Allow SignalR to authenticate via query string token
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                var path  = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
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

// ── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Game modules — auto-discovered via Scrutor ───────────────────────────────
builder.Services.Scan(scan => scan
    .FromApplicationDependencies()
    .AddClasses(c => c.AssignableTo<IGameModule>())
    .AsImplementedInterfaces()
    .WithSingletonLifetime()
    .AddClasses(c => c.AssignableTo<IGameHandler>())
    .AsImplementedInterfaces()
    .WithSingletonLifetime()
    .AddClasses(c => c.AssignableTo<IGameDbContext>())
    .AsImplementedInterfaces()
    .WithScopedLifetime());

// ── Platform services ────────────────────────────────────────────────────────
builder.Services.AddScoped<MigrationRunner>();
builder.Services.AddScoped<GameDispatcher>();
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173", "https://meepliton.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

var app = builder.Build();

// ── Startup migrations ────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
    await runner.RunAllAsync();
}

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ────────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapRoomEndpoints();
app.MapHub<GameHub>("/hubs/game");

app.Run();
