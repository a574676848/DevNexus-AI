namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 记忆沉淀触发原因。
/// </summary>
public static class MemoryConsolidationTriggerReasons
{
    /// <summary>
    /// 无新增消息。
    /// </summary>
    public const string NoNewMessages = "no-new-messages";

    /// <summary>
    /// 消息阈值已达到。
    /// </summary>
    public const string MessageThresholdReached = "message-threshold-reached";

    /// <summary>
    /// 上下文压力已出现。
    /// </summary>
    public const string ContextPressureDetected = "context-pressure-detected";

    /// <summary>
    /// 消息数不足。
    /// </summary>
    public const string TooFewMessages = "too-few-messages";

    /// <summary>
    /// 已调度空闲延迟沉淀。
    /// </summary>
    public const string IdleDelayScheduled = "idle-delay-scheduled";
}
