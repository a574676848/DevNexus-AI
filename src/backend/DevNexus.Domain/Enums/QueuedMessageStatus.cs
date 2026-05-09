namespace DevNexus.Domain.Enums;

/// <summary>
/// 排队消息状态枚举
/// </summary>
public enum QueuedMessageStatus
{
    /// <summary>
    /// 等待中（已入队，尚未派发）
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 派发中（已从队列取出，正在执行发送链路）
    /// </summary>
    Dispatching = 1,

    /// <summary>
    /// 已完成（消息已成功发送）
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 已取消（用户主动取消或会话关闭）
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// 失败（发送过程中发生异常）
    /// </summary>
    Failed = 4
}
