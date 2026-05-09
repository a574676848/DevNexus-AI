namespace DevNexus.Shared.DTOs;

/// <summary>
/// 更新观测详情 DTO。
/// </summary>
public class UpdateObservabilityDetailDto
{
    /// <summary>
    /// 最近失败事件。
    /// </summary>
    public IList<UpdateClientEventDto> RecentFailures { get; set; } = new List<UpdateClientEventDto>();

    /// <summary>
    /// 失败原因统计。
    /// </summary>
    public IList<UpdateFailureReasonDto> FailureReasons { get; set; } = new List<UpdateFailureReasonDto>();

    /// <summary>
    /// 事件类型统计。
    /// </summary>
    public IList<UpdateEventMetricDto> EventMetrics { get; set; } = new List<UpdateEventMetricDto>();

    /// <summary>
    /// 每日趋势。
    /// </summary>
    public IList<UpdateDailyTrendDto> DailyTrends { get; set; } = new List<UpdateDailyTrendDto>();
}

/// <summary>
/// 客户端更新事件 DTO。
/// </summary>
public class UpdateClientEventDto
{
    public string InstallationId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string TargetVersion { get; set; } = string.Empty;
    public Guid? RolloutId { get; set; }
    public Guid? ReleaseId { get; set; }
    public Guid? ArtifactId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 失败原因统计 DTO。
/// </summary>
public class UpdateFailureReasonDto
{
    public string ErrorCode { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// 事件指标 DTO。
/// </summary>
public class UpdateEventMetricDto
{
    public string EventType { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// 每日趋势 DTO。
/// </summary>
public class UpdateDailyTrendDto
{
    public DateTime Date { get; set; }
    public int CheckCount { get; set; }
    public int UpdateAvailableCount { get; set; }
    public int InstallCompletedCount { get; set; }
    public int FailedCount { get; set; }
}
