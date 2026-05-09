namespace DevNexus.Shared.Enums;

/// <summary>
/// CLI 会话状态。
/// </summary>
public enum CliSessionState
{
    /// <summary>
    /// 未知状态。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 已创建。
    /// </summary>
    Created = 1,

    /// <summary>
    /// 已排队。
    /// </summary>
    Queued = 2,

    /// <summary>
    /// 运行中。
    /// </summary>
    Running = 3,

    /// <summary>
    /// 等待输入。
    /// </summary>
    WaitingForInput = 4,

    /// <summary>
    /// 已完成。
    /// </summary>
    Completed = 5,

    /// <summary>
    /// 执行失败。
    /// </summary>
    Failed = 6,

    /// <summary>
    /// 已取消。
    /// </summary>
    Cancelled = 7,

    /// <summary>
    /// 已超时。
    /// </summary>
    TimedOut = 8,

    /// <summary>
    /// 已回收。
    /// </summary>
    Reaped = 9,

    /// <summary>
    /// 已回滚。
    /// </summary>
    RolledBack = 10
}

/// <summary>
/// CLI 会话状态字符串协议扩展。
/// </summary>
public static class CliSessionStateExtensions
{
    /// <summary>
    /// 转换为前后端传输使用的字符串值。
    /// </summary>
    public static string ToWireValue(this CliSessionState state)
    {
        return state switch
        {
            CliSessionState.Created => nameof(CliSessionState.Created),
            CliSessionState.Queued => nameof(CliSessionState.Queued),
            CliSessionState.Running => nameof(CliSessionState.Running),
            CliSessionState.WaitingForInput => nameof(CliSessionState.WaitingForInput),
            CliSessionState.Completed => nameof(CliSessionState.Completed),
            CliSessionState.Failed => nameof(CliSessionState.Failed),
            CliSessionState.Cancelled => nameof(CliSessionState.Cancelled),
            CliSessionState.TimedOut => nameof(CliSessionState.TimedOut),
            CliSessionState.Reaped => nameof(CliSessionState.Reaped),
            CliSessionState.RolledBack => nameof(CliSessionState.RolledBack),
            _ => nameof(CliSessionState.Unknown)
        };
    }

    /// <summary>
    /// 从字符串协议值解析为枚举。
    /// </summary>
    public static CliSessionState Parse(string? value)
    {
        var normalized = value?.Trim();

        return normalized switch
        {
            nameof(CliSessionState.Created) => CliSessionState.Created,
            nameof(CliSessionState.Queued) => CliSessionState.Queued,
            nameof(CliSessionState.Running) => CliSessionState.Running,
            nameof(CliSessionState.WaitingForInput) => CliSessionState.WaitingForInput,
            nameof(CliSessionState.Completed) => CliSessionState.Completed,
            nameof(CliSessionState.Failed) => CliSessionState.Failed,
            nameof(CliSessionState.Cancelled) => CliSessionState.Cancelled,
            nameof(CliSessionState.TimedOut) => CliSessionState.TimedOut,
            "Timeout" => CliSessionState.TimedOut,
            nameof(CliSessionState.Reaped) => CliSessionState.Reaped,
            nameof(CliSessionState.RolledBack) => CliSessionState.RolledBack,
            _ => CliSessionState.Unknown
        };
    }

    /// <summary>
    /// 是否为活动态。
    /// </summary>
    public static bool IsActive(this CliSessionState state)
    {
        return state is CliSessionState.Created
            or CliSessionState.Queued
            or CliSessionState.Running
            or CliSessionState.WaitingForInput;
    }
}
