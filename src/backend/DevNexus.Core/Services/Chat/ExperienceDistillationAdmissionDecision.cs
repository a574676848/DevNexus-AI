namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验提纯准入决策。
/// </summary>
public sealed class ExperienceDistillationAdmissionDecision
{
    /// <summary>
    /// 是否允许进入提纯。
    /// </summary>
    public bool ShouldDistill { get; init; }

    /// <summary>
    /// 准入原因。
    /// </summary>
    public string Reason { get; init; } = ExperienceDistillationAdmissionReasons.ContentTooShort;

    /// <summary>
    /// 命中的长期价值信号关键词。
    /// </summary>
    public string MatchedValueSignalKeyword { get; init; } = string.Empty;

    /// <summary>
    /// 命中的跳过条件关键词。
    /// </summary>
    public string MatchedSkipConditionKeyword { get; init; } = string.Empty;
}
