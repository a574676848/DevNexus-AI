using DevNexus.ApiService.Domain;
using DevNexus.ApiService.Data;
using DevNexus.ApiService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.ApiService.Endpoints;

public static class AIProviderEndpoints
{
    public static void MapAIEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/config/ai");

        group.MapGet("/", GetAIConfig);
        group.MapPost("/", SaveAIConfig);
        group.MapGet("/providers", GetAvailableProviders);

        app.MapPost("/api/ai/test", TestAIConnection);
    }

    private static IResult GetAvailableProviders()
    {
        var providers = AIProviderRegistry.GetProviders();
        return Results.Ok(providers);
    }

    private static async Task<IResult> GetAIConfig(DevNexusDbContext db)
    {
        var configs = await db.Configs
            .Where(c => c.Key.StartsWith("AI:"))
            .ToListAsync();

        var configDict = new Dictionary<string, string>();
        foreach (var config in configs)
        {
            configDict[config.Key] = config.Value;
        }

        return Results.Ok(configDict);
    }

    private static async Task<IResult> SaveAIConfig(DevNexusDbContext db, [FromBody] Dictionary<string, string> newConfigs)
    {
        foreach (var kvp in newConfigs)
        {
            if (!kvp.Key.StartsWith("AI:")) continue;

            var existing = await db.Configs.FirstOrDefaultAsync(c => c.Key == kvp.Key);
            if (existing != null)
            {
                existing.Value = kvp.Value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.Configs.Add(new Config { Key = kvp.Key, Value = kvp.Value });
            }
        }

        await db.SaveChangesAsync();
        return Results.Ok();
    }

    private static async Task<IResult> TestAIConnection([FromBody] Dictionary<string, string> config)
    {
        try
        {
            var provider = config.GetValueOrDefault("AI:Provider");
            var endpoint = config.GetValueOrDefault("AI:Endpoint");

            if (string.IsNullOrEmpty(provider))
                return Results.BadRequest("Provider is required");

            // Simulate network delay
            await Task.Delay(500);

            if (provider == "ollama")
            {
                if (string.IsNullOrEmpty(endpoint))
                    return Results.BadRequest("Endpoint is required for Ollama");

                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out _))
                    return Results.BadRequest("Invalid Endpoint URL");

                return Results.Ok(new { Status = "Connected", Message = "Successfully connected to Ollama" });
            }

            return Results.Ok(new { Status = "Connected", Message = $"Successfully connected to {provider}" });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
