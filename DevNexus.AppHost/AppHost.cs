var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddConnectionString("postgres");

var redis = builder.AddConnectionString("redis");

var seq = builder.AddConnectionString("seq");

var qdrant = builder.AddConnectionString("qdrant");

var api = builder.AddProject<Projects.DevNexus_ApiService>("apiservice")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(seq)
    .WithReference(qdrant);

builder.AddProject<Projects.DevNexus_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithReference(seq);

builder.Build().Run();
