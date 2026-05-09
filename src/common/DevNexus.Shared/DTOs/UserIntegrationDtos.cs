using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 用户集成响应
/// </summary>
public class UserIntegrationResponse
{
    public Guid Id { get; set; }
    public IntegrationType IntegrationType { get; set; }
    public string IntegrationTypeName { get; set; } = string.Empty;
    public Guid? ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public IntegrationAuthType AuthType { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public ValidationStatus ValidationStatus { get; set; }
    public CredentialRuntimeStatus CredentialRuntimeStatus { get; set; }
    public DateTime? LastValidatedAt { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public DateTime? LastCredentialRefreshAt { get; set; }
    public string? ValidationError { get; set; }
    public int ConsecutiveAuthFailureCount { get; set; }
    public DateTime? LastAuthFailureAt { get; set; }
    public DateTime? CooldownUntil { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 用户集成详细响应（管理员视图，包含用户信息）
/// </summary>
public class UserIntegrationDetailedResponse : UserIntegrationResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
}

/// <summary>
/// 创建用户集成请求
/// </summary>
public class CreateUserIntegrationRequest
{
    /// <summary>
    /// 用户 ID（可选，管理员可以为其他用户创建集成）
    /// </summary>
    public Guid? UserId { get; set; }
    
    public IntegrationType IntegrationType { get; set; }
    public Guid? ProviderId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public IntegrationAuthType AuthType { get; set; } = IntegrationAuthType.ApiToken;
    public string Credential { get; set; } = string.Empty;
    public string? SecondaryCredential { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public Dictionary<string, object>? Configuration { get; set; }
}

/// <summary>
/// 更新用户集成请求
/// </summary>
public class UpdateUserIntegrationRequest
{
    public string? DisplayName { get; set; }
    public string? Endpoint { get; set; }
    public string? Credential { get; set; }
    public string? SecondaryCredential { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsDefault { get; set; }
    public Dictionary<string, object>? Configuration { get; set; }
}

/// <summary>
/// 验证用户集成请求
/// </summary>
public class ValidateUserIntegrationRequest
{
    public IntegrationType IntegrationType { get; set; }
    public string? Endpoint { get; set; }
    public IntegrationAuthType AuthType { get; set; }
    public string Credential { get; set; } = string.Empty;
    public string? SecondaryCredential { get; set; }
}

/// <summary>
/// 验证用户集成响应
/// </summary>
public class ValidateUserIntegrationResponse
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ValidatedAt { get; set; }
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// 用户集成统计
/// </summary>
public class UserIntegrationStatsResponse
{
    public int TotalIntegrations { get; set; }
    public int ActiveIntegrations { get; set; }
    public Dictionary<IntegrationType, int> IntegrationsByType { get; set; } = new();
    public DateTime? LastUsedAt { get; set; }
}
