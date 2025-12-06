var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.DevNexus_ApiService>("apiservice")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
