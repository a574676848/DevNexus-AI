namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 记忆沉淀触发策略。
/// </summary>
public static class MemoryConsolidationTriggerPolicy
{
    /// <summary>
    /// 根据消息增量和上下文治理快照生成记忆沉淀触发决策。
    /// </summary>
    public static MemoryConsolidationTriggerDecision Decide(
        int currentMessageCount,
        int lastConsolidatedMessageCount,
        int messageThreshold,
        int minimumDelayedMessageCount,
        ChatHistoryGovernanceSnapshot? historyGovernance,
        bool hasExistingJob)
    {
        var messageDelta = Math.Max(0, currentMessageCount - lastConsolidatedMessageCount);
        if (messageDelta <= 0)
        {
            return BuildDecision(currentMessageCount, messageDelta, MemoryConsolidationTriggerReasons.NoNewMessages);
        }

        if (messageDelta >= messageThreshold)
        {
            return BuildImmediateDecision(
                currentMessageCount,
                messageDelta,
                MemoryConsolidationTriggerReasons.MessageThresholdReached,
                hasExistingJob);
        }

        var pressureSummary = ChatHistoryPressurePolicy.Summarize(historyGovernance);
        if (ShouldConsolidateForContextPressure(
            pressureSummary,
            historyGovernance,
            currentMessageCount,
            lastConsolidatedMessageCount,
            minimumDelayedMessageCount))
        {
            return BuildImmediateDecision(
                currentMessageCount,
                messageDelta,
                MemoryConsolidationTriggerReasons.ContextPressureDetected,
                hasExistingJob,
                pressureSummary.PrimaryReason);
        }

        if (currentMessageCount < minimumDelayedMessageCount)
        {
            return BuildDecision(currentMessageCount, messageDelta, MemoryConsolidationTriggerReasons.TooFewMessages);
        }

        return new MemoryConsolidationTriggerDecision
        {
            ShouldScheduleDelayed = true,
            ShouldCancelExistingJob = hasExistingJob,
            CurrentMessageCount = currentMessageCount,
            MessageDelta = messageDelta,
            Reason = MemoryConsolidationTriggerReasons.IdleDelayScheduled
        };
    }

    private static bool ShouldConsolidateForContextPressure(
        ChatHistoryPressureSummary pressureSummary,
        ChatHistoryGovernanceSnapshot? historyGovernance,
        int currentMessageCount,
        int lastConsolidatedMessageCount,
        int minimumDelayedMessageCount)
    {
        if (historyGovernance is null || currentMessageCount < minimumDelayedMessageCount)
        {
            return false;
        }

        if (!pressureSummary.HasPressure)
        {
            return false;
        }

        return pressureSummary.PrimaryReason != ChatHistoryPressureReasons.SummaryCompression
            || lastConsolidatedMessageCount < historyGovernance.CompressedMessageCount;
    }

    private static MemoryConsolidationTriggerDecision BuildImmediateDecision(
        int currentMessageCount,
        int messageDelta,
        string reason,
        bool hasExistingJob,
        string contextPressureReason = ChatHistoryPressureReasons.None)
    {
        return new MemoryConsolidationTriggerDecision
        {
            ShouldEnqueueImmediately = true,
            ShouldCancelExistingJob = hasExistingJob,
            CurrentMessageCount = currentMessageCount,
            MessageDelta = messageDelta,
            Reason = reason,
            ContextPressureReason = contextPressureReason
        };
    }

    private static MemoryConsolidationTriggerDecision BuildDecision(
        int currentMessageCount,
        int messageDelta,
        string reason)
    {
        return new MemoryConsolidationTriggerDecision
        {
            CurrentMessageCount = currentMessageCount,
            MessageDelta = messageDelta,
            Reason = reason
        };
    }
}
