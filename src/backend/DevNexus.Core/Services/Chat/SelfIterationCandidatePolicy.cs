using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 自我迭代候选策略。
/// </summary>
public static class SelfIterationCandidatePolicy
{
    /// <summary>
    /// 可进入经验提纯的最低回复长度。
    /// </summary>
    public const int MinimumResponseLengthForDistillation = 300;

    /// <summary>
    /// 根据任务编排快照判断是否值得进入经验提纯。
    /// </summary>
    public static SelfIterationCandidateDecision Decide(AgentTaskOrchestrationSnapshot snapshot)
    {
        if (snapshot.AgentLoopAction == AgentLoopAction.Retry)
        {
            return ObserveOnly(SelfIterationCandidateReasons.AgentLoopRetrying, snapshot);
        }

        if (snapshot.AgentLoopAction == AgentLoopAction.Stop)
        {
            return ObserveOnly(SelfIterationCandidateReasons.AgentLoopStopped, snapshot);
        }

        if (snapshot.FailedToolEventCount > 0)
        {
            return ObserveOnly(SelfIterationCandidateReasons.ToolRecoveryPending, snapshot);
        }

        if (snapshot.SystemExperienceReplay.InjectedDynamicContext
            || snapshot.SystemExperienceReplay.AnsweredDirectly)
        {
            return ObserveReusedExperience(snapshot);
        }

        if (snapshot.HasContextPressure)
        {
            return Distill(ResolveContextPressureReason(snapshot.ContextPressureReason), snapshot);
        }

        if (snapshot.MemoryEnqueuedImmediately)
        {
            return Distill(SelfIterationCandidateReasons.MemoryConsolidationImmediate, snapshot);
        }

        if (snapshot.ToolEventCount > 0 && snapshot.PrimarySuggestedAction == ToolSuggestedAction.None)
        {
            return Distill(SelfIterationCandidateReasons.ToolWorkflowCompleted, snapshot);
        }

        if (snapshot.ResponseLength >= MinimumResponseLengthForDistillation)
        {
            return Distill(SelfIterationCandidateReasons.LongFormAnswerCompleted, snapshot);
        }

        return ObserveOnly(SelfIterationCandidateReasons.CompletedWithoutSignal, snapshot);
    }

    private static SelfIterationCandidateDecision Distill(string reason, AgentTaskOrchestrationSnapshot snapshot)
    {
        return new SelfIterationCandidateDecision
        {
            ShouldDistillExperience = true,
            Reason = reason,
            ContextPressureReason = snapshot.ContextPressureReason,
            ContextCompressionSummaryFingerprint = snapshot.ContextCompressionIndex.SummaryFingerprint
        };
    }

    private static SelfIterationCandidateDecision ObserveOnly(string reason, AgentTaskOrchestrationSnapshot snapshot)
    {
        return new SelfIterationCandidateDecision
        {
            ShouldObserveOnly = true,
            Reason = reason,
            ContextPressureReason = snapshot.ContextPressureReason,
            ContextCompressionSummaryFingerprint = snapshot.ContextCompressionIndex.SummaryFingerprint
        };
    }

    private static SelfIterationCandidateDecision ObserveReusedExperience(AgentTaskOrchestrationSnapshot snapshot)
    {
        var contextTags = snapshot.SystemExperienceReplay.ContextTagSnapshot;
        return new SelfIterationCandidateDecision
        {
            ShouldObserveOnly = true,
            Reason = SelfIterationCandidateReasons.SystemExperienceReused,
            ContextPressureReason = snapshot.ContextPressureReason,
            ContextCompressionSummaryFingerprint = snapshot.ContextCompressionIndex.SummaryFingerprint,
            ReusedExperienceHasSelfIterationFacts = contextTags.HasSelfIterationFacts,
            ReusedExperienceCandidateReason = contextTags.CandidateReason,
            ReusedExperienceContextPressureReason = contextTags.ContextPressureReason,
            ReusedExperienceContextCompressionSummaryFingerprint = contextTags.ContextCompressionSummaryFingerprint,
            ReusedExperienceMemoryCitation = snapshot.SystemExperienceReplay.MemoryCitation
        };
    }

    private static string ResolveContextPressureReason(string pressureReason)
    {
        return pressureReason switch
        {
            ChatHistoryPressureReasons.SummaryCompression => SelfIterationCandidateReasons.SummaryCompressionResolved,
            ChatHistoryPressureReasons.BudgetTruncated => SelfIterationCandidateReasons.BudgetTruncationResolved,
            ChatHistoryPressureReasons.IncompleteAssistantSkipped => SelfIterationCandidateReasons.IncompleteAssistantSkippedResolved,
            _ => SelfIterationCandidateReasons.ContextPressureResolved
        };
    }
}
