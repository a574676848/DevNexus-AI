namespace DevNexus.Client.Shared.Services.Chat;

/// <summary>
/// 块索引器性能指标
/// 用于监控和分析块处理性能
/// </summary>
public record BlockIndexerMetrics
{
    /// <summary>
    /// 总块数
    /// </summary>
    public int TotalBlockCount { get; init; }
    
    /// <summary>
    /// 文本增量块数（TextDelta）
    /// </summary>
    public int TextDeltaCount { get; init; }
    
    /// <summary>
    /// 思维链块数（Thinking）
    /// </summary>
    public int ThinkingCount { get; init; }
    
    /// <summary>
    /// 展示块数（Terminal/Chart/Card/ToolResult）
    /// </summary>
    public int OrderedBlockCount { get; init; }
    
    /// <summary>
    /// 更新操作数（原位更新）
    /// </summary>
    public int UpdateOperationCount { get; init; }
    
    /// <summary>
    /// 文本内容总长度（字符数）
    /// </summary>
    public int TextContentLength { get; init; }
    
    /// <summary>
    /// 思维链内容总长度（字符数）
    /// </summary>
    public int ThinkingContentLength { get; init; }
    
    /// <summary>
    /// 是否需要重建思维链
    /// </summary>
    public bool NeedsThinkingRebuild { get; init; }
    
    /// <summary>
    /// AddBlock 平均耗时（毫秒）
    /// </summary>
    public double AverageAddBlockTimeMs { get; init; }
    
    /// <summary>
    /// AddBlock 调用总次数
    /// </summary>
    public int AddBlockCallCount { get; init; }
}
