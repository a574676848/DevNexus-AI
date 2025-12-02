using System.ComponentModel.DataAnnotations;

namespace DevNexus.Web.Client.Models;

public class AIProviderConfig
{
    [Required]
    public string Provider { get; set; } = "openai-compatible"; // Default to OpenAI Compatible

    public string Endpoint { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
