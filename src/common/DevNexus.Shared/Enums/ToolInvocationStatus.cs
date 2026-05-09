namespace DevNexus.Shared.Enums;

/// <summary>
/// 工具调用生命周期状态。
/// </summary>
public enum ToolInvocationStatus
{
    Unknown = 0,
    Queued = 1,
    Pending = 2,
    Running = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6,
    Timeout = 7
}

/// <summary>
/// ToolInvocationStatus 的字符串协议转换。
/// </summary>
public static class ToolInvocationStatusExtensions
{
    /// <summary>
    /// 转换为前后端传输使用的字符串值。
    /// </summary>
    public static string ToWireValue(this ToolInvocationStatus status)
    {
        return status switch
        {
            ToolInvocationStatus.Queued => "queued",
            ToolInvocationStatus.Pending => "pending",
            ToolInvocationStatus.Running => "running",
            ToolInvocationStatus.Completed => "completed",
            ToolInvocationStatus.Failed => "failed",
            ToolInvocationStatus.Cancelled => "cancelled",
            ToolInvocationStatus.Timeout => "timeout",
            _ => "unknown"
        };
    }

    /// <summary>
    /// 从字符串协议值解析为枚举。
    /// 兼容历史状态别名（success/error/start 等）。
    /// </summary>
    public static ToolInvocationStatus Parse(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant();

        return normalized switch
        {
            "queued" => ToolInvocationStatus.Queued,
            "pending" => ToolInvocationStatus.Pending,
            "start" => ToolInvocationStatus.Running,
            "started" => ToolInvocationStatus.Running,
            "running" => ToolInvocationStatus.Running,
            "success" => ToolInvocationStatus.Completed,
            "completed" => ToolInvocationStatus.Completed,
            "error" => ToolInvocationStatus.Failed,
            "failed" => ToolInvocationStatus.Failed,
            "exception" => ToolInvocationStatus.Failed,
            "cancel" => ToolInvocationStatus.Cancelled,
            "canceled" => ToolInvocationStatus.Cancelled,
            "cancelled" => ToolInvocationStatus.Cancelled,
            "timeout" => ToolInvocationStatus.Timeout,
            _ => ToolInvocationStatus.Unknown
        };
    }

    /// <summary>
    /// 是否为结束态。
    /// </summary>
    public static bool IsTerminal(this ToolInvocationStatus status)
    {
        return status == ToolInvocationStatus.Completed
            || status == ToolInvocationStatus.Failed
            || status == ToolInvocationStatus.Cancelled
            || status == ToolInvocationStatus.Timeout;
    }
}
