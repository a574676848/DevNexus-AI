namespace DevNexus.Core.Models.Cli;

/// <summary>
/// CLI 会话执行状态
/// </summary>
public enum CliSessionExecutionState
{
    /// <summary>
    /// 已创建
    /// </summary>
    Created = 0,

    /// <summary>
    /// 运行中
    /// </summary>
    Running = 1,

    /// <summary>
    /// 等待输入
    /// </summary>
    WaitingForInput = 2,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 3,

    /// <summary>
    /// 执行失败
    /// </summary>
    Failed = 4,

    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled = 5,

    /// <summary>
    /// 已超时
    /// </summary>
    TimedOut = 6,

    /// <summary>
    /// 已回收
    /// </summary>
    Reaped = 7
}