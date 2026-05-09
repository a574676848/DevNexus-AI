using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 搜索供应商响应DTO
/// </summary>
public class SearchProviderResponse
{
    public Guid Id { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public SearchProviderType Type { get; set; }
    public string? LogoUrl { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public int Priority { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public DateTime? LastValidatedAt { get; set; }
    public ValidationStatus ValidationStatus { get; set; }
    public string? ValidationError { get; set; }
    public string? SearchEngineId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 创建搜索供应商请求DTO
/// </summary>
public class CreateSearchProviderRequest
{
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public SearchProviderType Type { get; set; }
    public string? LogoUrl { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public int Priority { get; set; } = 100;
    public Dictionary<string, object> Configuration { get; set; } = new();
    public string? SearchEngineId { get; set; }
}

/// <summary>
/// 更新搜索供应商请求DTO
/// </summary>
public class UpdateSearchProviderRequest
{
    public string? DisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public bool? IsEnabled { get; set; }
    public int? Priority { get; set; }
    public Dictionary<string, object>? Configuration { get; set; }
    public string? SearchEngineId { get; set; }
}

/// <summary>
/// 搜索供应商验证响应DTO
/// </summary>
public class SearchProviderValidationResponse
{
    public Guid Id { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public ValidationStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ValidatedAt { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
