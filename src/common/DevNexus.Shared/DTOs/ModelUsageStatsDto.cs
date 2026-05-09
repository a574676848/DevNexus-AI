namespace DevNexus.Shared.DTOs;

/// <summary>
/// 模型使用统计 DTO
/// </summary>
public class ModelUsageStatsDto
{
    /// <summary>
    /// 模型ID
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// 提供商类型。
    /// </summary>
    public string ProviderType { get; set; } = ModelInvocationProviderTypes.Llm;

    /// <summary>
    /// 提供商名称
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// 提供商 ID（LLMProviders 表的数据库主键 GUID）
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// 请求数
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// 总 Token 数
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// 总成本
    /// </summary>
    public decimal TotalCost { get; set; }
}
