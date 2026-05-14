namespace DevNexus.Shared.DTOs;

/// <summary>
/// AI Agent 优化看板数据。
/// </summary>
public class AiOptimizationDashboardDto
{
    /// <summary>
    /// 输入 Token 总数。
    /// </summary>
    public long TotalInputTokens { get; set; }

    /// <summary>
    /// 缓存命中输入 Token 总数。
    /// </summary>
    public long CachedPromptTokens { get; set; }

    /// <summary>
    /// 缓存命中率。
    /// </summary>
    public double CacheHitRatio { get; set; }

    /// <summary>
    /// 记录了稳定前缀指纹的请求数。
    /// </summary>
    public int StablePrefixTrackedRequests { get; set; }

    /// <summary>
    /// 记录了工具 Schema 指纹的请求数。
    /// </summary>
    public int ToolSchemaTrackedRequests { get; set; }

    /// <summary>
    /// 工具调用总数。
    /// </summary>
    public int ToolCallCount { get; set; }

    /// <summary>
    /// 工具调用成功数。
    /// </summary>
    public int ToolSuccessCount { get; set; }

    /// <summary>
    /// 工具调用失败数。
    /// </summary>
    public int ToolFailureCount { get; set; }

    /// <summary>
    /// 工具调用成功率。
    /// </summary>
    public double ToolSuccessRate { get; set; }

    /// <summary>
    /// 工具参数通过预验证的调用数。
    /// </summary>
    public int ToolArgumentValidCount { get; set; }

    /// <summary>
    /// 可重试工具失败数。
    /// </summary>
    public int ToolRetryableFailureCount { get; set; }

    /// <summary>
    /// 需要人工介入的工具失败数。
    /// </summary>
    public int ToolHumanInterventionCount { get; set; }

    /// <summary>
    /// 按工具名称分组的调用统计。
    /// </summary>
    public List<ToolInvocationStatsDto> ToolStats { get; set; } = new();

    /// <summary>
    /// 按工具失败原因分组的统计。
    /// </summary>
    public List<AuditBreakdownDto> ToolFailureReasonStats { get; set; } = new();
}

/// <summary>
/// 工具调用统计。
/// </summary>
public class ToolInvocationStatsDto
{
    /// <summary>
    /// 工具名称。
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// 调用次数。
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// 成功次数。
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失败次数。
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// 成功率。
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// 平均耗时（毫秒）。
    /// </summary>
    public double AverageDurationMs { get; set; }
}
