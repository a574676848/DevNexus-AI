namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 自我迭代复盘决策。
/// </summary>
public sealed class SelfIterationReviewDecision
{
    /// <summary>
    /// 是否已形成新的长期经验。
    /// </summary>
    public bool HasNewExperience { get; init; }

    /// <summary>
    /// 是否应仅记录观察。
    /// </summary>
    public bool ShouldObserveOnly { get; init; }

    /// <summary>
    /// 是否需要关注修复。
    /// </summary>
    public bool RequiresRepairAttention { get; init; }

    /// <summary>
    /// 复盘原因。
    /// </summary>
    public string Reason { get; init; } = SelfIterationReviewReasons.SaveResultUnclassified;

    /// <summary>
    /// 保存结果命中的系统经验引用事实。
    /// </summary>
    public SystemExperienceMemoryCitation MemoryCitation { get; init; } = SystemExperienceMemoryCitation.Empty;

    /// <summary>
    /// 本次保存尝试的系统经验引用事实。
    /// </summary>
    public SystemExperienceMemoryCitation AttemptMemoryCitation { get; init; } = SystemExperienceMemoryCitation.Empty;
}
