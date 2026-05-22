using DevNexus.Core.DTOs;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 自我迭代复盘策略。
/// </summary>
public static class SelfIterationReviewPolicy
{
    /// <summary>
    /// 记录提纯被跳过的观察型复盘。
    /// </summary>
    public static SelfIterationReviewDecision ObserveSkippedDistillation(string reason)
    {
        return new SelfIterationReviewDecision
        {
            ShouldObserveOnly = true,
            Reason = reason
        };
    }

    /// <summary>
    /// 记录带引用事实的提纯跳过观察型复盘。
    /// </summary>
    public static SelfIterationReviewDecision ObserveSkippedDistillation(
        string reason,
        SystemExperienceMemoryCitation memoryCitation)
    {
        return new SelfIterationReviewDecision
        {
            ShouldObserveOnly = true,
            Reason = reason,
            MemoryCitation = memoryCitation
        };
    }

    /// <summary>
    /// 根据提纯跳过原因归类观察型复盘。
    /// </summary>
    public static SelfIterationReviewDecision ObserveSkippedDistillationBySkipReason(string skipReason)
    {
        var reviewReason = skipReason switch
        {
            SelfIterationSkipReasons.TooFewMessages
                or SelfIterationSkipReasons.SwarmSession
                or SelfIterationSkipReasons.MissingQaPair
                or SelfIterationSkipReasons.ProviderMissing => SelfIterationReviewReasons.PreconditionSkipped,
            SelfIterationSkipReasons.ModelTimeout
                or SelfIterationSkipReasons.ModelCancelled
                or SelfIterationSkipReasons.ModelInterrupted => SelfIterationReviewReasons.ModelInvocationSkipped,
            ExperienceDistillationAdmissionReasons.MissingQaPair
                or ExperienceDistillationAdmissionReasons.ContentTooShort
                or ExperienceDistillationAdmissionReasons.SkipConditionMatched
                or ExperienceDistillationAdmissionReasons.MissingValueSignal => SelfIterationReviewReasons.AdmissionSkipped,
            ExperienceDistillationParseReasons.Empty
                or ExperienceDistillationParseReasons.NoValue
                or ExperienceDistillationParseReasons.MissingIntent
                or ExperienceDistillationParseReasons.MissingIntentMarker
                or ExperienceDistillationParseReasons.MissingSop
                or ExperienceDistillationParseReasons.MarkdownCodeBlock
                or ExperienceDistillationParseReasons.NoValueWithContent
                or ExperienceDistillationParseReasons.SopTooLong
                or ExperienceDistillationParseReasons.RawTranscriptLeak => SelfIterationReviewReasons.ParseSkipped,
            _ => SelfIterationReviewReasons.SaveResultUnclassified
        };

        return ObserveSkippedDistillation(reviewReason);
    }

    /// <summary>
    /// 根据提纯跳过原因归类带引用事实的观察型复盘。
    /// </summary>
    public static SelfIterationReviewDecision ObserveSkippedDistillationBySkipReason(
        string skipReason,
        SystemExperienceMemoryCitation memoryCitation)
    {
        var decision = ObserveSkippedDistillationBySkipReason(skipReason);
        return ObserveSkippedDistillation(decision.Reason, memoryCitation);
    }

    /// <summary>
    /// 根据系统经验保存结果判断自我迭代复盘动作。
    /// </summary>
    public static SelfIterationReviewDecision Decide(ExperienceSaveResultDto saveResult)
    {
        if (saveResult.IsDuplicate)
        {
            return new SelfIterationReviewDecision
            {
                ShouldObserveOnly = true,
                Reason = SelfIterationReviewReasons.ExperienceDuplicateSkipped,
                MemoryCitation = saveResult.MemoryCitation,
                AttemptMemoryCitation = saveResult.AttemptMemoryCitation
            };
        }

        if (saveResult.IsNew && saveResult.VectorIndexed)
        {
            return new SelfIterationReviewDecision
            {
                HasNewExperience = true,
                Reason = SelfIterationReviewReasons.ExperienceCreatedAndIndexed,
                MemoryCitation = saveResult.MemoryCitation,
                AttemptMemoryCitation = saveResult.AttemptMemoryCitation
            };
        }

        if (saveResult.IsNew)
        {
            return new SelfIterationReviewDecision
            {
                HasNewExperience = true,
                RequiresRepairAttention = true,
                Reason = SelfIterationReviewReasons.ExperienceCreatedButIndexFailed,
                MemoryCitation = saveResult.MemoryCitation,
                AttemptMemoryCitation = saveResult.AttemptMemoryCitation
            };
        }

        return new SelfIterationReviewDecision
        {
            ShouldObserveOnly = true
        };
    }
}
