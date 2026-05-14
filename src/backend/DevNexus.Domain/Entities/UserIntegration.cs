using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 用户外部系统集成实体
/// 统一管理用户与各种外部系统的连接（笔记、代码仓库、云服务等）
/// </summary>
public class UserIntegration : AuditableEntity
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 集成类型
    /// </summary>
    public IntegrationType IntegrationType { get; set; }

    /// <summary>
    /// 关联的供应商 ID。
    /// 可为空，对于不需要全局配置的集成
    /// </summary>
    public Guid? ProviderId { get; set; }

    /// <summary>
    /// 供应商名称（冗余字段，方便查询）
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// 连接显示名称（用户自定义）
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 服务端点 URL
    /// 如果 ProviderId 存在，可从 Provider 获取；否则用户自定义
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// 认证类型
    /// </summary>
    public IntegrationAuthType AuthType { get; set; } = IntegrationAuthType.ApiToken;

    /// <summary>
    /// 访问凭证 (加密存储)
    /// 可能是 Token、API Key、密码等
    /// </summary>
    public string Credential { get; set; } = string.Empty;

    /// <summary>
    /// 额外凭证（如 OAuth Refresh Token、Secret Key）
    /// 加密存储
    /// </summary>
    public string? SecondaryCredential { get; set; }

    /// <summary>
    /// OAuth 访问令牌过期时间
    /// </summary>
    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>
    /// 最近一次凭证刷新时间。
    /// </summary>
    public DateTime? LastCredentialRefreshAt { get; set; }

    /// <summary>
    /// 是否激活
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 是否为该类型的默认集成
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// 配置元数据 (JSONB)
    /// 可存储特定集成的额外配置
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// 最后验证时间
    /// </summary>
    public DateTime? LastValidatedAt { get; set; }

    /// <summary>
    /// 验证状态
    /// </summary>
    public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.Unknown;

    /// <summary>
    /// 验证错误消息
    /// </summary>
    public string? ValidationError { get; set; }

    /// <summary>
    /// 连续认证失败次数。
    /// </summary>
    public int ConsecutiveAuthFailureCount { get; set; }

    /// <summary>
    /// 最近一次认证失败时间。
    /// </summary>
    public DateTime? LastAuthFailureAt { get; set; }

    /// <summary>
    /// 凭证冷却截止时间。
    /// 在冷却结束前不应继续自动尝试该凭证。
    /// </summary>
    public DateTime? CooldownUntil { get; set; }

    /// <summary>
    /// 最后使用时间
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// 使用次数统计
    /// </summary>
    public int UsageCount { get; set; } = 0;
}
