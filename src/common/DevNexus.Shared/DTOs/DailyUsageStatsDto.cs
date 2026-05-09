namespace DevNexus.Shared.DTOs;

/// <summary>
/// 每日使用统计 DTO
/// </summary>
public class DailyUsageStatsDto
{
    /// <summary>
    /// 日期
    /// </summary>
    public DateTime Date { get; set; }

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
