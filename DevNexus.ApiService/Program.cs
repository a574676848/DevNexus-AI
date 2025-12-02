using DevNexus.ApiService.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

builder.AddNpgsqlDbContext<DevNexus.ApiService.Data.DevNexusDbContext>("postgres");

builder.AddServiceDefaults();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapAIEndpoints();

app.UseHttpsRedirection();

app.Run();
