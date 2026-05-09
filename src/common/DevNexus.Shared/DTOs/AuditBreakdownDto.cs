namespace DevNexus.Shared.DTOs;

/// <summary>
/// 审计聚合拆分 DTO。
/// </summary>
public class AuditBreakdownDto
{
    /// <summary>
    /// 编码。
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 展示名称。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 请求数。
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// 总 Token 数。
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// 总成本。
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// 失败数。
    /// </summary>
    public int FailedCount { get; set; }
}
