namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 记忆沉淀触发决策。
/// </summary>
public sealed class MemoryConsolidationTriggerDecision
{
    /// <summary>
    /// 是否立即入队。
    /// </summary>
    public bool ShouldEnqueueImmediately { get; init; }

    /// <summary>
    /// 是否调度延迟任务。
    /// </summary>
    public bool ShouldScheduleDelayed { get; init; }

    /// <summary>
    /// 是否需要取消已有任务。
    /// </summary>
    public bool ShouldCancelExistingJob { get; init; }

    /// <summary>
    /// 当前消息数。
    /// </summary>
    public int CurrentMessageCount { get; init; }

    /// <summary>
    /// 距上次沉淀的消息增量。
    /// </summary>
    public int MessageDelta { get; init; }

    /// <summary>
    /// 决策原因。
    /// </summary>
    public string Reason { get; init; } = MemoryConsolidationTriggerReasons.TooFewMessages;

    /// <summary>
    /// 上下文压力原因。
    /// </summary>
    public string ContextPressureReason { get; init; } = ChatHistoryPressureReasons.None;
}
