namespace DevNexus.Shared.DTOs;

/// <summary>
/// Token 使用统计 DTO
/// </summary>
public class TokenUsageStatsDto
{
    /// <summary>
    /// 总请求数
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// 成功请求数
    /// </summary>
    public int SuccessfulRequests { get; set; }

    /// <summary>
    /// 失败请求数
    /// </summary>
    public int FailedRequests { get; set; }

    /// <summary>
    /// 总输入 Token 数
    /// </summary>
    public long TotalInputTokens { get; set; }

    /// <summary>
    /// 总输出 Token 数
    /// </summary>
    public long TotalOutputTokens { get; set; }

    /// <summary>
    /// 总 Token 数
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// 总成本（美元）
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// 平均处理时间（毫秒）
    /// </summary>
    public double AverageDurationMs { get; set; }

    /// <summary>
    /// 按模型分组的统计
    /// </summary>
    public List<ModelUsageStatsDto> ModelStats { get; set; } = new();

    /// <summary>
    /// 按日期分组的统计
    /// </summary>
    public List<DailyUsageStatsDto> DailyStats { get; set; } = new();

    /// <summary>
    /// 按场景分组的统计
    /// </summary>
    public List<AuditBreakdownDto> SceneStats { get; set; } = new();

    /// <summary>
    /// 按主体分组的统计
    /// </summary>
    public List<AuditBreakdownDto> OwnerStats { get; set; } = new();

    /// <summary>
    /// 按调用类型分组的统计
    /// </summary>
    public List<AuditBreakdownDto> InvocationKindStats { get; set; } = new();
}
