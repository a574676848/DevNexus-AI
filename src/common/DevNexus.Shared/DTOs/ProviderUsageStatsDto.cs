namespace DevNexus.Shared.DTOs;

/// <summary>
/// 供应商使用统计 DTO
/// </summary>
public class ProviderUsageStatsDto
{
    /// <summary>
    /// 提供商类型。
    /// </summary>
    public string ProviderType { get; set; } = ModelInvocationProviderTypes.Llm;

    /// <summary>
    /// 供应商名称
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// 供应商ID（LLMProviders 表的数据库主键 GUID）
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// 总请求数
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// 总 Token 数
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// 总成本
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// 模型明细
    /// </summary>
    public List<ModelUsageStatsDto> ModelBreakdown { get; set; } = new();
}
