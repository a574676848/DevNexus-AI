namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 性能监控服务接口
/// </summary>
public interface IPerformanceMonitor
{
    IDisposable StartTrace(string operationName);
    void RecordMetric(string name, double value, string unit = "ms");
    PerformanceStats GetStats();
    void Reset();
}

public class PerformanceStats
{
    public int TotalOperations { get; set; }
    public Dictionary<string, OperationStats> Operations { get; set; } = new();
    public long MemoryUsageMB { get; set; }
    public TimeSpan Uptime { get; set; }
}

public class OperationStats
{
    public int Count { get; set; }
    public double AverageDuration { get; set; }
    public double MinDuration { get; set; }
    public double MaxDuration { get; set; }
    public double TotalDuration { get; set; }
}

/// <summary>
/// 性能优化配置
/// </summary>
public class PerformanceOptions
{
    /// <summary>
    /// 启用虚拟化滚动
    /// </summary>
    public bool EnableVirtualization { get; set; } = true;

    /// <summary>
    /// 启用图片懒加载
    /// </summary>
    public bool EnableImageLazyLoading { get; set; } = true;

    /// <summary>
    /// 启用组件缓存
    /// </summary>
    public bool EnableComponentCaching { get; set; } = true;

    /// <summary>
    /// 启用防抖处理
    /// </summary>
    public bool EnableDebouncing { get; set; } = true;

    /// <summary>
    /// SignalR 消息批处理大小
    /// </summary>
    public int SignalRBatchSize { get; set; } = 10;

    /// <summary>
    /// SignalR 消息批处理延迟（毫秒）
    /// </summary>
    public int SignalRBatchDelayMs { get; set; } = 50;

    /// <summary>
    /// Markdown 渲染防抖延迟（毫秒）
    /// </summary>
    public int MarkdownDebounceMs { get; set; } = 100;

    /// <summary>
    /// 消息列表虚拟化阈值
    /// </summary>
    public int MessageVirtualizationThreshold { get; set; } = 50;

    /// <summary>
    /// 最大内存缓存大小（MB）
    /// </summary>
    public int MaxMemoryCacheMB { get; set; } = 100;

    /// <summary>
    /// 启用性能监控
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;
}

