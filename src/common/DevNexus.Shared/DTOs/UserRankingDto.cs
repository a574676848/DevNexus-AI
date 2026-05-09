namespace DevNexus.Shared.DTOs;

/// <summary>
/// 用户排行 DTO
/// </summary>
public class UserRankingDto
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 总 Token 数
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// 总成本
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// 会话数
    /// </summary>
    public int SessionCount { get; set; }

    /// <summary>
    /// 平均每会话成本
    /// </summary>
    public decimal AverageCostPerSession { get; set; }

    /// <summary>
    /// 趋势（"up", "down", "stable"）
    /// </summary>
    public string Trend { get; set; } = "stable";
}
