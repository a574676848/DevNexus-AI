using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 自我迭代候选决策 metadata 协议。
/// </summary>
public static class SelfIterationCandidateMetadata
{
    /// <summary>
    /// 将自我迭代候选决策写入消息 metadata。
    /// </summary>
    public static void Apply(
        IDictionary<string, object> metadata,
        SelfIterationCandidateDecision decision)
    {
        metadata[ChatMessageMetadataKeys.SelfIterationCandidateReason] = decision.Reason;
        metadata[ChatMessageMetadataKeys.SelfIterationShouldDistill] = decision.ShouldDistillExperience;
        metadata[ChatMessageMetadataKeys.SelfIterationObserveOnly] = decision.ShouldObserveOnly;
        metadata[ChatMessageMetadataKeys.SelfIterationContextPressureReason] = decision.ContextPressureReason;
        metadata[ChatMessageMetadataKeys.SelfIterationContextCompressionSummaryFingerprint] =
            decision.ContextCompressionSummaryFingerprint;

        if (decision.ReusedExperienceMemoryCitation == SystemExperienceMemoryCitation.Empty)
        {
            return;
        }

        metadata[ChatMessageMetadataKeys.ReusedExperienceCitationFingerprint] =
            decision.ReusedExperienceMemoryCitation.CitationFingerprint;
    }
}
