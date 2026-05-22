using DevNexus.Core.DTOs;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验回放快照。
/// </summary>
public sealed class SystemExperienceReplaySnapshot
{
    /// <summary>
    /// 空快照。
    /// </summary>
    public static readonly SystemExperienceReplaySnapshot Empty = new();

    /// <summary>
    /// 是否命中系统经验。
    /// </summary>
    public bool HasMatch { get; init; }

    /// <summary>
    /// 是否已实际复用系统经验。
    /// </summary>
    public bool WasReplayed => AnsweredDirectly || InjectedDynamicContext;

    /// <summary>
    /// 是否直接返回经验答案。
    /// </summary>
    public bool AnsweredDirectly { get; init; }

    /// <summary>
    /// 是否注入为动态上下文。
    /// </summary>
    public bool InjectedDynamicContext { get; init; }

    /// <summary>
    /// 回放决策原因。
    /// </summary>
    public string Reason { get; init; } = SystemExperienceReplayReasons.NoMatch;

    /// <summary>
    /// 经验标识。
    /// </summary>
    public Guid? ExperienceId { get; init; }

    /// <summary>
    /// 匹配相似度。
    /// </summary>
    public float? Similarity { get; init; }

    /// <summary>
    /// 经验上下文标签。
    /// </summary>
    public string ContextTags { get; init; } = string.Empty;

    /// <summary>
    /// 结构化上下文标签快照。
    /// </summary>
    public SystemExperienceContextTagSnapshot ContextTagSnapshot { get; init; } =
        SystemExperienceContextTagSnapshot.Empty;

    /// <summary>
    /// 结构化记忆引用事实。
    /// </summary>
    public SystemExperienceMemoryCitation MemoryCitation =>
        SystemExperienceMemoryCitation.FromTagSnapshot(ExperienceId, ContextTagSnapshot);

    /// <summary>
    /// 触发该系统经验沉淀的长期价值信号关键词。
    /// </summary>
    public string ValueSignalKeyword => ContextTagSnapshot.ValueSignalKeyword;

    /// <summary>
    /// 经验来源会话标识。
    /// </summary>
    public Guid? SourceSessionId => ContextTagSnapshot.SourceSessionId;

    /// <summary>
    /// 从回放决策创建快照。
    /// </summary>
    public static SystemExperienceReplaySnapshot FromDecision(SystemExperienceReplayDecision? decision)
    {
        if (decision?.Match == null)
        {
            return Empty;
        }

        var experience = decision.Match.Experience;
        return new SystemExperienceReplaySnapshot
        {
            HasMatch = true,
            AnsweredDirectly = decision.ShouldAnswerDirectly,
            InjectedDynamicContext = decision.ShouldInjectDynamicContext,
            Reason = decision.Reason,
            ExperienceId = experience.Id,
            Similarity = decision.Match.Similarity,
            ContextTags = experience.ContextTags,
            ContextTagSnapshot = SystemExperienceContextTagSnapshot.Parse(experience.ContextTags)
        };
    }
}
