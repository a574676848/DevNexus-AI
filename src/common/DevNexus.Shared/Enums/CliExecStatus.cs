using System.Text.Json.Serialization;

namespace DevNexus.Shared.Enums;

/// <summary>
/// CLI 执行会话的统一执行状态。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CliExecStatus
{
    /// <summary>
    /// 未知。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 已请求。
    /// </summary>
    Requested = 1,

    /// <summary>
    /// 等待批准。
    /// </summary>
    PendingApproval = 2,

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
    /// 已排队，等待真正进入执行。
    /// </summary>
    Queued = 10,

    /// <summary>
    /// 已回滚。
    /// </summary>
    RolledBack = 11
}
