namespace DevNexus.Web.Client.Models;

/// <summary>
/// Client-side model for AI Provider information
/// </summary>
public class AIProviderInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ApiKeyUrl { get; set; }
    public bool RequiresApiKey { get; set; } = true;
    public string? DefaultEndpoint { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
}
