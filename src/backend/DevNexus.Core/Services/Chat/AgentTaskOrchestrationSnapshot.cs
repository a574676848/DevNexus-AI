using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent 单轮任务编排快照。
/// </summary>
public sealed class AgentTaskOrchestrationSnapshot
{
    /// <summary>
    /// 轮次标识。
    /// </summary>
    public Guid TurnId { get; init; }

    /// <summary>
    /// 自动修复尝试次数。
    /// </summary>
    public int AgentLoopAttempt { get; init; }

    /// <summary>
    /// Agent Loop 决策动作。
    /// </summary>
    public AgentLoopAction AgentLoopAction { get; init; }

    /// <summary>
    /// 历史上下文治理策略。
    /// </summary>
    public string ContextStrategy { get; init; } = ChatHistoryGovernanceStrategies.Empty;

    /// <summary>
    /// 是否出现上下文压力。
    /// </summary>
    public bool HasContextPressure { get; init; }

    /// <summary>
    /// 上下文压力原因。
    /// </summary>
    public string ContextPressureReason { get; init; } = ChatHistoryPressureReasons.None;

    /// <summary>
    /// 历史压缩索引。
    /// </summary>
    public ChatHistoryCompressionIndex ContextCompressionIndex { get; init; } =
        ChatHistoryCompressionIndex.Empty;

    /// <summary>
    /// 系统经验回放快照。
    /// </summary>
    public SystemExperienceReplaySnapshot SystemExperienceReplay { get; init; } =
        SystemExperienceReplaySnapshot.Empty;

    /// <summary>
    /// 已复用系统经验的记忆引用事实。
    /// </summary>
    public SystemExperienceMemoryCitation ExperienceMemoryCitation =>
        SystemExperienceReplay.MemoryCitation;

    /// <summary>
    /// 已复用系统经验的长期价值信号关键词。
    /// </summary>
    public string ExperienceValueSignalKeyword => ExperienceMemoryCitation.ValueSignalKeyword;

    /// <summary>
    /// 已复用系统经验的来源会话标识。
    /// </summary>
    public Guid? ExperienceSourceSessionId => ExperienceMemoryCitation.SourceSessionId;

    /// <summary>
    /// 已复用系统经验的提纯 Prompt 指纹。
    /// </summary>
    public string ExperienceDistillationPromptFingerprint =>
        ExperienceMemoryCitation.DistillationPromptFingerprint;

    /// <summary>
    /// 记忆沉淀触发原因。
    /// </summary>
    public string MemoryTriggerReason { get; init; } = MemoryConsolidationTriggerReasons.TooFewMessages;

    /// <summary>
    /// 是否立即触发记忆沉淀。
    /// </summary>
    public bool MemoryEnqueuedImmediately { get; init; }

    /// <summary>
    /// 是否已调度延迟记忆沉淀。
    /// </summary>
    public bool MemoryScheduledDelayed { get; init; }

    /// <summary>
    /// 工具事件数量。
    /// </summary>
    public int ToolEventCount { get; init; }

    /// <summary>
    /// 回复正文长度。
    /// </summary>
    public int ResponseLength { get; init; }

    /// <summary>
    /// 失败工具事件数量。
    /// </summary>
    public int FailedToolEventCount { get; init; }

    /// <summary>
    /// 首要工具恢复动作。
    /// </summary>
    public ToolSuggestedAction PrimarySuggestedAction { get; init; }

    /// <summary>
    /// 编排下一步动作。
    /// </summary>
    public string NextStep { get; init; } = AgentTaskOrchestrationSteps.Complete;
}
