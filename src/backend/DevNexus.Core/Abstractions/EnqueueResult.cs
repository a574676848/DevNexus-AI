namespace DevNexus.Core.Abstractions;

/// <summary>
/// 排队消息处理结果
/// </summary>
public record EnqueueResult(
    /// <summary>
    /// 执行决策（立即发送 / 已入队 / 转发给运行时 / 拒绝）
    /// </summary>
    Domain.Enums.ChatExecutionDecision Decision,

    /// <summary>
    /// 排队消息 ID（仅在 Decision == Queued 时有值）
    /// </summary>
    Guid? QueuedMessageId = null,

    /// <summary>
    /// 当前队列中的 Pending 消息数量
    /// </summary>
    int PendingCount = 0,

    /// <summary>
    /// 人类友好的提示信息
    /// </summary>
    string? Message = null);
