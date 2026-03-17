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
    .WaitFor(postgres);

builder.AddViteApp("frontend", "../apps/frontend")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: 5173, name: "frontend");

builder.Build().Run();
