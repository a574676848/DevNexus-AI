using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// Embedding供应商创建请求DTO
/// </summary>
public class CreateEmbeddingProviderRequest
{
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public EmbeddingProviderType Type { get; set; }
    public string? LogoUrl { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int VectorSize { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public int Priority { get; set; } = 100;
    public Dictionary<string, object>? Configuration { get; set; }
}

/// <summary>
/// Embedding供应商更新请求DTO
/// </summary>
public class UpdateEmbeddingProviderRequest
{
    public string? DisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? ModelName { get; set; }
    public int? VectorSize { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsDefault { get; set; }
    public int? Priority { get; set; }
    public Dictionary<string, object>? Configuration { get; set; }
}

/// <summary>
/// Embedding供应商响应DTO
/// </summary>
public class EmbeddingProviderResponse
{
    public Guid Id { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public EmbeddingProviderType Type { get; set; }
    public string? LogoUrl { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int VectorSize { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public int Priority { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public ValidationStatus ValidationStatus { get; set; }
    public string? ValidationError { get; set; }
    public DateTime? LastValidatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
