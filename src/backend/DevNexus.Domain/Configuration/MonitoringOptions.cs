namespace DevNexus.Domain.Configuration;

/// <summary>
/// 监控配置选项
/// </summary>
public class MonitoringOptions
{
    /// <summary>
    /// 是否启用详细监控
    /// </summary>
    public bool EnableDetailedMonitoring { get; set; } = true;

    /// <summary>
    /// 是否记录提供商使用情况
    /// </summary>
    public bool TrackProviderUsage { get; set; } = true;

    /// <summary>
    /// 是否记录 Token 消耗
    /// </summary>
    public bool TrackTokenConsumption { get; set; } = true;

    /// <summary>
    /// 是否记录向量数据库大小
    /// </summary>
    public bool TrackVectorDbSize { get; set; } = true;

    /// <summary>
    /// 指标收集间隔（秒）
    /// </summary>
    public int MetricsCollectionIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// 是否启用性能分析
    /// </summary>
    public bool EnablePerformanceProfiling { get; set; } = false;

    /// <summary>
    /// 慢查询阈值（毫秒）
    /// </summary>
    public int SlowQueryThresholdMs { get; set; } = 1000;
}
