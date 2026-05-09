namespace DevNexus.Core.Models.Cli;

/// <summary>
/// CLI 会话终止原因
/// </summary>
public enum CliSessionTerminationReason
{
    /// <summary>
    /// 未终止
    /// </summary>
    None = 0,

    /// <summary>
    /// 正常完成
    /// </summary>
    Completed = 1,

    /// <summary>
    /// 用户取消
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// 空闲超时
    /// </summary>
    IdleTimeout = 3,

    /// <summary>
    /// 等待输入超时
    /// </summary>
    WaitingForInputTimeout = 4,

    /// <summary>
    /// 超过最大运行时长
    /// </summary>
    MaxRuntimeExceeded = 5,

    /// <summary>
    /// 连接断开
    /// </summary>
    ConnectionDisconnected = 6,

    /// <summary>
    /// 进程异常退出
    /// </summary>
    ProcessExited = 7,

    /// <summary>
    /// 运行时异常
    /// </summary>
    Error = 8,

    /// <summary>
    /// 工作目录锁冲突
    /// </summary>
    LockConflict = 9
}