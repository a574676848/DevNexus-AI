namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天历史上下文压力策略。
/// </summary>
public static class ChatHistoryPressurePolicy
{
    /// <summary>
    /// 根据历史治理快照判断上下文压力。
    /// </summary>
    public static ChatHistoryPressureSummary Summarize(ChatHistoryGovernanceSnapshot? historyGovernance)
    {
        if (historyGovernance is null)
        {
            return NoPressure();
        }

        if (historyGovernance.SummaryMessageCount > 0)
        {
            return Pressure(ChatHistoryPressureReasons.SummaryCompression);
        }

        if (historyGovernance.TruncatedByBudget)
        {
            return Pressure(ChatHistoryPressureReasons.BudgetTruncated);
        }

        if (historyGovernance.SkippedIncompleteAssistantMessageCount > 0)
        {
            return Pressure(ChatHistoryPressureReasons.IncompleteAssistantSkipped);
        }

        return NoPressure();
    }

    private static ChatHistoryPressureSummary Pressure(string reason)
    {
        return new ChatHistoryPressureSummary
        {
            HasPressure = true,
            PrimaryReason = reason
        };
    }

    private static ChatHistoryPressureSummary NoPressure()
    {
        return new ChatHistoryPressureSummary();
    }
}
