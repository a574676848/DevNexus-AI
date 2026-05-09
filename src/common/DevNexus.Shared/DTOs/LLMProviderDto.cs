using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// LLM供应商创建请求DTO
/// </summary>
public class CreateLLMProviderRequest
{
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ProviderType Type { get; set; }
    public string? LogoUrl { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public int Priority { get; set; } = 100;
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.7;
    public double TopP { get; set; } = 0.9;
    public string? GroupId { get; set; }
    public Dictionary<string, object>? Configuration { get; set; }
}

/// <summary>
/// LLM供应商更新请求DTO
/// </summary>
public class UpdateLLMProviderRequest
{
    public string? DisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? ModelName { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsDefault { get; set; }
    public int? Priority { get; set; }
    public Dictionary<string, object>? Configuration { get; set; }
}

/// <summary>
/// LLM供应商响应DTO
/// </summary>
public class LLMProviderResponse
{
    public Guid Id { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ProviderType Type { get; set; }
    public string? LogoUrl { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
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

/// <summary>
/// 验证供应商请求DTO
/// </summary>
public class ValidateProviderRequest
{
    public string? ProviderId { get; set; }
}

/// <summary>
/// 验证供应商响应DTO
/// </summary>
public class ValidateProviderResponse
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? Details { get; set; }
}
