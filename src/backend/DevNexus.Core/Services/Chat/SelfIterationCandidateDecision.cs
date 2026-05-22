namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 自我迭代候选决策。
/// </summary>
public sealed class SelfIterationCandidateDecision
{
    /// <summary>
    /// 是否应该调度经验提纯。
    /// </summary>
    public bool ShouldDistillExperience { get; init; }

    /// <summary>
    /// 是否应该仅记录观察，不触发后台提纯。
    /// </summary>
    public bool ShouldObserveOnly { get; init; }

    /// <summary>
    /// 决策原因。
    /// </summary>
    public string Reason { get; init; } = SelfIterationCandidateReasons.CompletedWithoutSignal;

    /// <summary>
    /// 上下文压力原因。
    /// </summary>
    public string ContextPressureReason { get; init; } = ChatHistoryPressureReasons.None;

    /// <summary>
    /// 上下文压缩摘要指纹。
    /// </summary>
    public string ContextCompressionSummaryFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// 复用的系统经验是否携带自我迭代调度事实。
    /// </summary>
    public bool ReusedExperienceHasSelfIterationFacts { get; init; }

    /// <summary>
    /// 复用经验的原始候选原因。
    /// </summary>
    public string ReusedExperienceCandidateReason { get; init; } = string.Empty;

    /// <summary>
    /// 复用经验的上下文压力原因。
    /// </summary>
    public string ReusedExperienceContextPressureReason { get; init; } = string.Empty;

    /// <summary>
    /// 复用经验的上下文压缩摘要指纹。
    /// </summary>
    public string ReusedExperienceContextCompressionSummaryFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// 复用经验的记忆引用事实。
    /// </summary>
    public SystemExperienceMemoryCitation ReusedExperienceMemoryCitation { get; init; } =
        SystemExperienceMemoryCitation.Empty;

    /// <summary>
    /// 复用经验的长期价值信号关键词。
    /// </summary>
    public string ReusedExperienceValueSignalKeyword => ReusedExperienceMemoryCitation.ValueSignalKeyword;

    /// <summary>
    /// 复用经验的来源会话标识。
    /// </summary>
    public Guid? ReusedExperienceSourceSessionId => ReusedExperienceMemoryCitation.SourceSessionId;

    /// <summary>
    /// 复用经验的提纯 Prompt 指纹。
    /// </summary>
    public string ReusedExperienceDistillationPromptFingerprint =>
        ReusedExperienceMemoryCitation.DistillationPromptFingerprint;
}
