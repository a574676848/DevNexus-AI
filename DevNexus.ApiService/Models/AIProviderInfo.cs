namespace DevNexus.ApiService.Models;

/// <summary>
/// Metadata for an AI provider
/// </summary>
public class AIProviderInfo
{
    /// <summary>
    /// Unique identifier for the provider (e.g., "openai-compatible")
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name for the provider (e.g., "OpenAI Compatible")
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// URL where users can obtain an API key
    /// </summary>
    public string? ApiKeyUrl { get; init; }

    /// <summary>
    /// Whether this provider requires an API key
    /// </summary>
    public bool RequiresApiKey { get; init; } = true;

    /// <summary>
    /// Default endpoint URL for this provider (optional)
    /// </summary>
    public string? DefaultEndpoint { get; init; }

    /// <summary>
    /// Brief description of the provider
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Icon or emoji to represent the provider
    /// </summary>
    public string? Icon { get; init; }
}
