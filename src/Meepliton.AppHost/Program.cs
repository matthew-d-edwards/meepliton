var builder = DistributedApplication.CreateBuilder(args);

// Debug: Log the configuration values being used
var postgresUser = builder.Configuration["PostgreSQL:Username"] ?? "postgres";
var postgresPass = builder.Configuration["PostgreSQL:Password"] ?? "postgres";
Console.WriteLine($"PostgreSQL Config - Username: {postgresUser}, Password: {postgresPass}");

var postgresUsername = builder.AddParameter("postgres-username", value: postgresUser);
var postgresPassword = builder.AddParameter("postgres-password", value: postgresPass);
var postgres = builder.AddPostgres("postgres")
    .WithUserName(postgresUsername)
    .WithPassword(postgresPassword)
    .WithDataVolume()
    .AddDatabase("meepliton");

var api = builder.AddProject<Projects.Meepliton_Api>("api")
    .WithReference(postgres)
    .WaitFor(postgres)
    // Disable the DCP reverse proxy so Vite connects directly to the API process.
    // This avoids intermittent 503s from the proxy and lets the fixed port in
    // launchSettings (http://localhost:5000) be used reliably.
    .WithEndpoint("http", e => e.IsProxied = false);

builder.AddViteApp("frontend", "../../apps/frontend")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 5173, name: "frontend")
    // Disable the DCP reverse proxy so the browser connects to Vite directly on 5173.
    // Without this, Aspire picks a random proxy port on each restart.
    .WithEndpoint("frontend", e => e.IsProxied = false);

builder.Build().Run();
