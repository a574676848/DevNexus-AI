using System.Collections.Generic;

namespace DevNexus.Core.Models.Observability;

/// <summary>
/// 结构化日志入口 - 统一的日志记录结构体
/// 所有发送到 Seq 的日志都应遵循此结构，便于查询和聚合
/// </summary>
public class StructuredLogEntry
{
    // ===== 时间戳 =====
    /// <summary>
    /// 事件发生时间 (UTC ISO 8601 格式)
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // ===== 追踪信息 =====
    /// <summary>
    /// 分布式追踪 ID (W3C TraceId 标准)
    /// </summary>
    public string TraceId { get; set; } = "";

    /// <summary>
    /// Span ID (W3C SpanId 标准)
    /// </summary>
    public string SpanId { get; set; } = "";

    /// <summary>
    /// 父 Span ID
    /// </summary>
    public string ParentSpanId { get; set; } = "";

    // ===== 租户和用户信息 =====
    /// <summary>
    /// 租户 ID
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// 用户 ID
    /// </summary>
    public Guid UserId { get; set; }

    // ===== 业务信息 =====
    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 消息 ID
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// 工具调用 ID
    /// </summary>
    public Guid? ToolCallId { get; set; }

    /// <summary>
    /// 当前重试/尝试次数 (0 表示第一次)
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// 终端流 ID (用于聚合同一命令的多个输出块)
    /// </summary>
    public string? TerminalStreamId { get; set; }

    // ===== 事件信息 =====
    /// <summary>
    /// 日志级别 (Trace, Debug, Information, Warning, Error, Critical, Fatal)
    /// </summary>
    public string Level { get; set; } = "Information";

    /// <summary>
    /// 事件类型 (如 AgentLoopEvaluationStarted, ToolExecutionCompleted 等)
    /// </summary>
    public string EventType { get; set; } = "";

    /// <summary>
    /// 日志类别/主题 (如 AgentLoop, ToolExecution, Terminal, MessageGeneration 等)
    /// </summary>
    public string Topic { get; set; } = "";

    /// <summary>
    /// 日志消息
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// 异常堆栈跟踪
    /// </summary>
    public string? ExceptionStackTrace { get; set; }

    // ===== 性能指标 =====
    /// <summary>
    /// 操作耗时 (毫秒)
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// 内存使用量 (MB)
    /// </summary>
    public double? MemoryUsageMb { get; set; }

    /// <summary>
    /// CPU 使用率 (%)
    /// </summary>
    public double? CpuUsagePercent { get; set; }

    // ===== 工具执行指标 =====
    /// <summary>
    /// 工具名称
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// 工具执行状态 (Success, Failed, Timeout)
    /// </summary>
    public string? ToolExecutionStatus { get; set; }

    /// <summary>
    /// 工具失败原因（如果失败）
    /// </summary>
    public string? ToolFailureReason { get; set; }

    // ===== 终端相关指标 =====
    /// <summary>
    /// 终端命令
    /// </summary>
    public string? TerminalCommand { get; set; }

    /// <summary>
    /// 终端工作目录
    /// </summary>
    public string? TerminalWorkingDirectory { get; set; }

    /// <summary>
    /// 终端输出字节数
    /// </summary>
    public long? TerminalOutputBytes { get; set; }

    /// <summary>
    /// 终端输出行数
    /// </summary>
    public int? TerminalOutputLines { get; set; }

    /// <summary>
    /// 终端输出块数
    /// </summary>
    public int? TerminalChunkCount { get; set; }

    /// <summary>
    /// 终端状态 (Running, Completed, Failed, Timeout)
    /// </summary>
    public string? TerminalStatus { get; set; }

    /// <summary>
    /// 终端退出码
    /// </summary>
    public int? TerminalExitCode { get; set; }

    // ===== Agent Loop 相关指标 =====
    /// <summary>
    /// Agent Loop 评估结果 (Pass, NeedRepair, Fail)
    /// </summary>
    public string? EvaluationResult { get; set; }

    /// <summary>
    /// 自动修复是否成功
    /// </summary>
    public bool? RepairSuccess { get; set; }

    /// <summary>
    /// 停止自动修复的原因
    /// </summary>
    public string? StopRepairReason { get; set; }

    // ===== 会话恢复相关 =====
    /// <summary>
    /// 恢复操作类型 (Online, Offline, FullRecovery)
    /// </summary>
    public string? RecoveryType { get; set; }

    /// <summary>
    /// 恢复的消息数量
    /// </summary>
    public int? RecoveredMessageCount { get; set; }

    // ===== 自定义字段 =====
    /// <summary>
    /// 自定义标签 (用于聚合和过滤)
    /// </summary>
    public Dictionary<string, object> Tags { get; set; } = new();

    /// <summary>
    /// 自定义数据（不限制类型）
    /// </summary>
    public Dictionary<string, object> CustomData { get; set; } = new();

    /// <summary>
    /// 用于调试的额外信息
    /// </summary>
    public string? DebugInfo { get; set; }

    /// <summary>
    /// 验证必填字段是否完整
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(TraceId) &&
               !string.IsNullOrEmpty(Level) &&
               !string.IsNullOrEmpty(EventType);
    }

    /// <summary>
    /// 转换为 Serilog 友好的字典（用于结构化日志输出）
    /// </summary>
    public Dictionary<string, object> ToLoggingDictionary()
    {
        var dict = new Dictionary<string, object>
        {
            { "Timestamp", Timestamp },
            { "TraceId", TraceId },
            { "SpanId", SpanId },
            { "TenantId", TenantId },
            { "UserId", UserId },
            { "SessionId", SessionId },
            { "MessageId", MessageId },
            { "Level", Level },
            { "EventType", EventType },
            { "Topic", Topic },
            { "Message", Message },
        };

        if (ToolCallId != null && ToolCallId != Guid.Empty)
            dict.Add("ToolCallId", ToolCallId);

        if (AttemptNumber > 0)
            dict.Add("AttemptNumber", AttemptNumber);

        if (!string.IsNullOrEmpty(ParentSpanId))
            dict.Add("ParentSpanId", ParentSpanId);

        if (!string.IsNullOrEmpty(TerminalStreamId))
            dict.Add("TerminalStreamId", TerminalStreamId);

        if (DurationMs.HasValue)
            dict.Add("DurationMs", DurationMs);

        if (MemoryUsageMb.HasValue)
            dict.Add("MemoryUsageMb", MemoryUsageMb);

        if (CpuUsagePercent.HasValue)
            dict.Add("CpuUsagePercent", CpuUsagePercent);

        if (!string.IsNullOrEmpty(ToolName))
            dict.Add("ToolName", ToolName);

        if (!string.IsNullOrEmpty(ToolExecutionStatus))
            dict.Add("ToolExecutionStatus", ToolExecutionStatus);

        if (!string.IsNullOrEmpty(ExceptionStackTrace))
            dict.Add("ExceptionStackTrace", ExceptionStackTrace);

        // 添加可选字段
        foreach (var kvp in Tags)
            dict.Add($"Tag_{kvp.Key}", kvp.Value);

        foreach (var kvp in CustomData)
            dict.Add($"Custom_{kvp.Key}", kvp.Value);

        return dict;
    }
}
