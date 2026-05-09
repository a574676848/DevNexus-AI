namespace DevNexus.Shared.Constants;

/// <summary>
/// CLI 会话终止原因字符串协议定义。
/// </summary>
public static class CliSessionTerminationReasons
{
    /// <summary>
    /// 未终止。
    /// </summary>
    public const string None = "None";

    /// <summary>
    /// 正常完成。
    /// </summary>
    public const string Completed = "Completed";

    /// <summary>
    /// 用户取消。
    /// </summary>
    public const string Cancelled = "Cancelled";

    /// <summary>
    /// 空闲超时。
    /// </summary>
    public const string IdleTimeout = "IdleTimeout";

    /// <summary>
    /// 等待输入超时。
    /// </summary>
    public const string WaitingForInputTimeout = "WaitingForInputTimeout";

    /// <summary>
    /// 超过最大运行时长。
    /// </summary>
    public const string MaxRuntimeExceeded = "MaxRuntimeExceeded";

    /// <summary>
    /// 连接断开。
    /// </summary>
    public const string ConnectionDisconnected = "ConnectionDisconnected";

    /// <summary>
    /// 进程异常退出。
    /// </summary>
    public const string ProcessExited = "ProcessExited";

    /// <summary>
    /// 运行时错误。
    /// </summary>
    public const string Error = "Error";

    /// <summary>
    /// 工作目录锁冲突。
    /// </summary>
    public const string LockConflict = "LockConflict";

    private const string LegacyFailed = "Failed";
    private const string LegacyTimeout = "Timeout";
    private const string LegacyReaped = "Reaped";

    /// <summary>
    /// 规范化终止原因字符串，兼容历史别名。
    /// </summary>
    public static string Normalize(string? terminationReason, string fallback = None)
    {
        var normalized = terminationReason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        return normalized.ToLowerInvariant() switch
        {
            "none" => None,
            "completed" => Completed,
            "cancelled" => Cancelled,
            "idletimeout" => IdleTimeout,
            "waitingforinputtimeout" => WaitingForInputTimeout,
            "maxruntimeexceeded" => MaxRuntimeExceeded,
            "connectiondisconnected" => ConnectionDisconnected,
            "processexited" => ProcessExited,
            "error" => Error,
            "lockconflict" => LockConflict,
            "failed" => Error,
            "timeout" => MaxRuntimeExceeded,
            "reaped" => LegacyReaped,
            _ => fallback
        };
    }

    /// <summary>
    /// 获取终止原因的中文展示文本。
    /// </summary>
    public static string GetDisplayText(string? terminationReason)
    {
        return Normalize(terminationReason, string.Empty) switch
        {
            Completed => "正常结束",
            Cancelled => "已停止",
            IdleTimeout => "空闲超时",
            WaitingForInputTimeout => "等待输入超时",
            MaxRuntimeExceeded => "执行超时",
            ConnectionDisconnected => "连接已断开",
            ProcessExited => "进程已退出",
            Error => "执行失败",
            LockConflict => "工作目录锁冲突",
            LegacyReaped => "已结束",
            None or "" => string.Empty,
            _ => terminationReason ?? string.Empty
        };
    }
}