namespace DevNexus.Core.Abstractions.Observability;

/// <summary>
/// 分布式追踪服务 - 提供统一的日志和追踪发送接口
/// </summary>
public interface IDistributedTracingService
{
    /// <summary>
    /// 发送结构化日志
    /// </summary>
    /// <param name="traceEvent">追踪事件</param>
    /// <param name="level">日志级别</param>
    /// <param name="message">消息</param>
    /// <param name="exception">异常信息</param>
    Task LogStructuredEventAsync(
        TraceEvent traceEvent,
        string level = "Information",
        string? message = null,
        Exception? exception = null);

    /// <summary>
    /// 记录性能指标
    /// </summary>
    /// <param name="metricName">指标名称</param>
    /// <param name="value">指标值</param>
    /// <param name="unit">单位</param>
    /// <param name="tags">标签</param>
    Task RecordMetricAsync(
        string metricName,
        double value,
        string unit = "",
        Dictionary<string, string>? tags = null);

    /// <summary>
    /// 记录操作耗时（使用 IDisposable 上下文）
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <returns>自动记录耗时的上下文</returns>
    IDisposable BeginOperationTimer(string operationName);
}

/// <summary>
/// 追踪事件类型 - 定义可观测的事件分类
/// </summary>
public enum TraceEvent
{
    // Agent Loop 相关
    AgentLoopEvaluationStarted,         // Agent Loop 评估开始
    AgentLoopEvaluationCompleted,       // Agent Loop 评估完成
    AgentLoopRepairDecided,             // 决定触发自动修复
    AgentLoopRepairAttemptStarted,      // 自动修复尝试开始
    AgentLoopRepairAttemptFailed,       // 自动修复尝试失败
    AgentLoopMaxAttemptsReached,        // 达到最大重试次数
    
    // 工具执行相关
    ToolExecutionStarted,               // 工具执行开始
    ToolExecutionCompleted,             // 工具执行完成
    ToolExecutionFailed,                // 工具执行失败
    ToolExecutionTimeout,               // 工具执行超时
    
    // 终端输出相关
    TerminalStreamStarted,              // 终端流开始
    TerminalStreamChunkReceived,        // 接收到终端数据块
    TerminalStreamCompleted,            // 终端流完成
    TerminalPersistenceStarted,         // 终端持久化开始
    TerminalPersistenceCompleted,       // 终端持久化完成
    TerminalPersistenceFailed,          // 终端持久化失败
    TerminalReplayStarted,              // 终端回放开始
    
    // 消息生成相关
    MessageGenerationStarted,           // 消息生成开始
    MessageGenerationCompleted,         // 消息生成完成
    MessageGenerationFailed,            // 消息生成失败
    MessageGenerationCancelled,         // 消息生成被取消
    
    // 会话恢复相关
    SessionRecoveryStarted,             // 会话恢复开始
    SessionRecoveryCompleted,           // 会话恢复完成
    SessionRecoveryFailed,              // 会话恢复失败
    
    // 思维链相关
    ThinkingChainStarted,               // 思维链开始
    ThinkingChainEmitted,               // 思维链发出
    
    // 其他
    UnexpectedError,                    // 未预期的错误
    PerformanceThresholdExceeded,       // 性能阈值超出
}
