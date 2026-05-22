using DevNexus.Core.DTOs;
using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验回放策略。
/// </summary>
public static class SystemExperienceReplayPolicy
{
    /// <summary>
    /// 根据系统经验匹配结果决定回放方式。
    /// </summary>
    public static SystemExperienceReplayDecision Decide(ExperienceMatchDto? match)
    {
        if (match == null)
        {
            return new SystemExperienceReplayDecision();
        }

        if (match.Similarity >= MemoryConstants.ChatPerfectHitThreshold)
        {
            return Build(match, direct: true, dynamicContext: false, SystemExperienceReplayReasons.DirectAnswer);
        }

        if (match.Similarity >= MemoryConstants.ChatPartialHitThreshold)
        {
            return Build(match, direct: false, dynamicContext: true, SystemExperienceReplayReasons.DynamicContext);
        }

        return Build(match, direct: false, dynamicContext: false, SystemExperienceReplayReasons.BelowReplayThreshold);
    }

    private static SystemExperienceReplayDecision Build(
        ExperienceMatchDto match,
        bool direct,
        bool dynamicContext,
        string reason)
    {
        return new SystemExperienceReplayDecision
        {
            ShouldAnswerDirectly = direct,
            ShouldInjectDynamicContext = dynamicContext,
            Match = match,
            Reason = reason
        };
    }
}
