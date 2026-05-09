namespace DevNexus.Shared.Enums;

/// <summary>
/// 终端流执行状态。
/// </summary>
public enum TerminalStreamStatus
{
    /// <summary>
    /// 未知状态。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 运行中。
    /// </summary>
    Running = 1,

    /// <summary>
    /// 已完成。
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 执行失败。
    /// </summary>
    Failed = 3,

    /// <summary>
    /// 已取消。
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// 已超时。
    /// </summary>
    Timeout = 5
}

/// <summary>
/// 终端流执行状态字符串协议扩展。
/// </summary>
public static class TerminalStreamStatusExtensions
{
    /// <summary>
    /// 转换为前后端传输使用的字符串值。
    /// </summary>
    public static string ToWireValue(this TerminalStreamStatus status)
    {
        return status switch
        {
            TerminalStreamStatus.Running => nameof(TerminalStreamStatus.Running),
            TerminalStreamStatus.Completed => nameof(TerminalStreamStatus.Completed),
            TerminalStreamStatus.Failed => nameof(TerminalStreamStatus.Failed),
            TerminalStreamStatus.Cancelled => nameof(TerminalStreamStatus.Cancelled),
            TerminalStreamStatus.Timeout => nameof(TerminalStreamStatus.Timeout),
            _ => nameof(TerminalStreamStatus.Unknown)
        };
    }

    /// <summary>
    /// 从字符串协议值解析为枚举。
    /// </summary>
    public static TerminalStreamStatus Parse(string? value)
    {
        var normalized = value?.Trim();

        return normalized switch
        {
            nameof(TerminalStreamStatus.Running) => TerminalStreamStatus.Running,
            nameof(TerminalStreamStatus.Completed) => TerminalStreamStatus.Completed,
            nameof(TerminalStreamStatus.Failed) => TerminalStreamStatus.Failed,
            nameof(TerminalStreamStatus.Cancelled) => TerminalStreamStatus.Cancelled,
            nameof(TerminalStreamStatus.Timeout) => TerminalStreamStatus.Timeout,
            nameof(CliSessionState.TimedOut) => TerminalStreamStatus.Timeout,
            _ => TerminalStreamStatus.Unknown
        };
    }

    /// <summary>
    /// 是否为结束态。
    /// </summary>
    public static bool IsTerminal(this TerminalStreamStatus status)
    {
        return status is TerminalStreamStatus.Completed
            or TerminalStreamStatus.Failed
            or TerminalStreamStatus.Cancelled
            or TerminalStreamStatus.Timeout;
    }
}