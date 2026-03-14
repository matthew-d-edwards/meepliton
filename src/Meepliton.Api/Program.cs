using System.Text;
using Meepliton.Api.Data;
using Meepliton.Api.Hubs;
using Meepliton.Api.Identity;
using Meepliton.Api.Services;
using Meepliton.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Meepliton.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("meepliton")));

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
        options.SignIn.RequireConfirmedEmail           = true;
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

// Platform services
builder.Services.AddScoped<MigrationRunner>();
builder.Services.AddScoped<GameDispatcher>();
builder.Services.AddScoped<TokenService>();
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
}

// Middleware
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapAuthEndpoints();
app.MapRoomEndpoints();
app.MapHub<GameHub>("/hubs/game");

app.Run();
