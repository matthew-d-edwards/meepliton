var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("meepliton");

var api = builder.AddProject<Projects.Meepliton_Api>("api")
    .WithReference(postgres);

builder.AddNpmApp("frontend", "../apps/frontend")
    .WithReference(api)
    .WithHttpEndpoint(port: 5173);

builder.Build().Run();
