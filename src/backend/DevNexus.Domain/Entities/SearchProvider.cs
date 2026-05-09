using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 搜索供应商配置实体
/// </summary>
public class SearchProvider : AuditableEntity
{
    /// <summary>
    /// 供应商唯一标识 (bing, google, duckduckgo)
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;
    
    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// 供应商类型
    /// </summary>
    public SearchProviderType Type { get; set; }
    
    /// <summary>
    /// 供应商Logo URL
    /// </summary>
    public string? LogoUrl { get; set; }
    
    /// <summary>
    /// API端点
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;
    
    /// <summary>
    /// API密钥 (加密存储)
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 是否为默认供应商
    /// </summary>
    public bool IsDefault { get; set; } = false;
    
    /// <summary>
    /// 优先级 (数字越小优先级越高)
    /// </summary>
    public int Priority { get; set; } = 100;
    
    /// <summary>
    /// 配置元数据 (JSONB)
    /// 示例: { "Market": "zh-CN", "SafeSearch": "Moderate", "DefaultCount": 10 }
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
    /// 搜索引擎ID (仅 Google Custom Search 需要)
    /// </summary>
    public string? SearchEngineId { get; set; }
}
