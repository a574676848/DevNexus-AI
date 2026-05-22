namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 自我迭代候选原因。
/// </summary>
public static class SelfIterationCandidateReasons
{
    /// <summary>
    /// Agent Loop 仍在重试。
    /// </summary>
    public const string AgentLoopRetrying = "agent-loop-retrying";

    /// <summary>
    /// Agent Loop 已停止。
    /// </summary>
    public const string AgentLoopStopped = "agent-loop-stopped";

    /// <summary>
    /// 工具恢复仍需处理。
    /// </summary>
    public const string ToolRecoveryPending = "tool-recovery-pending";

    /// <summary>
    /// 本轮已复用既有系统经验。
    /// </summary>
    public const string SystemExperienceReused = "system-experience-reused";

    /// <summary>
    /// 上下文压力已被处理。
    /// </summary>
    public const string ContextPressureResolved = "context-pressure-resolved";

    /// <summary>
    /// 摘要压缩后的上下文压力已被处理。
    /// </summary>
    public const string SummaryCompressionResolved = "summary-compression-resolved";

    /// <summary>
    /// 预算截断后的上下文压力已被处理。
    /// </summary>
    public const string BudgetTruncationResolved = "budget-truncation-resolved";

    /// <summary>
    /// 未完成助手消息跳过后的上下文压力已被处理。
    /// </summary>
    public const string IncompleteAssistantSkippedResolved = "incomplete-assistant-skipped-resolved";

    /// <summary>
    /// 记忆沉淀已立即触发。
    /// </summary>
    public const string MemoryConsolidationImmediate = "memory-consolidation-immediate";

    /// <summary>
    /// 工具工作流已完成。
    /// </summary>
    public const string ToolWorkflowCompleted = "tool-workflow-completed";

    /// <summary>
    /// 长回复已完成。
    /// </summary>
    public const string LongFormAnswerCompleted = "long-form-answer-completed";

    /// <summary>
    /// 已完成但缺少可提纯信号。
    /// </summary>
    public const string CompletedWithoutSignal = "completed-without-signal";
}
