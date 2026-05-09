namespace DevNexus.Shared.DTOs;

/// <summary>
/// 审计看板 DTO。
/// </summary>
public class AuditDashboardDto
{
    /// <summary>
    /// 总览信息。
    /// </summary>
    public AuditOverviewDto Overview { get; set; } = new();

    /// <summary>
    /// 场景分布。
    /// </summary>
    public List<AuditBreakdownDto> SceneBreakdown { get; set; } = new();

    /// <summary>
    /// 主体分布。
    /// </summary>
    public List<AuditBreakdownDto> OwnerBreakdown { get; set; } = new();

    /// <summary>
    /// 调用类型分布。
    /// </summary>
    public List<AuditBreakdownDto> InvocationBreakdown { get; set; } = new();

    /// <summary>
    /// 异常场景列表。
    /// </summary>
    public List<AuditExceptionSpotDto> ExceptionSpots { get; set; } = new();
}

/// <summary>
/// 审计总览 DTO。
/// </summary>
public class AuditOverviewDto
{
    public int TotalRequests { get; set; }
    public long TotalTokens { get; set; }
    public decimal TotalCost { get; set; }
    public double SuccessRate { get; set; }
    public double AverageDurationMs { get; set; }
    public double SystemCallRatio { get; set; }
    public double EstimatedUsageRatio { get; set; }
    public double TimeoutRate { get; set; }
}

/// <summary>
/// 审计异常场景 DTO。
/// </summary>
public class AuditExceptionSpotDto
{
    public string SceneCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int FailedCount { get; set; }
    public int TimeoutCount { get; set; }
    public int EstimatedCount { get; set; }
    public int TotalCount { get; set; }
    public double FailureRate { get; set; }
    public double TimeoutRate { get; set; }
    public double EstimatedRate { get; set; }
}
