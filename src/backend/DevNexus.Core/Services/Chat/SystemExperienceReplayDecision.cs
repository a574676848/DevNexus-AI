using DevNexus.Core.DTOs;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验回放决策。
/// </summary>
public sealed class SystemExperienceReplayDecision
{
    /// <summary>
    /// 是否直接返回经验答案。
    /// </summary>
    public bool ShouldAnswerDirectly { get; init; }

    /// <summary>
    /// 是否注入为动态上下文。
    /// </summary>
    public bool ShouldInjectDynamicContext { get; init; }

    /// <summary>
    /// 匹配结果。
    /// </summary>
    public ExperienceMatchDto? Match { get; init; }

    /// <summary>
    /// 决策原因。
    /// </summary>
    public string Reason { get; init; } = SystemExperienceReplayReasons.NoMatch;
}
