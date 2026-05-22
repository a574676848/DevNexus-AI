namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天历史治理策略名称。
/// </summary>
public static class ChatHistoryGovernanceStrategies
{
    /// <summary>
    /// 无可用历史。
    /// </summary>
    public const string Empty = "empty";

    /// <summary>
    /// 直接回放历史。
    /// </summary>
    public const string DirectReplay = "direct-replay";

    /// <summary>
    /// 摘要压缩早期历史并保留最近片段。
    /// </summary>
    public const string SummaryWithRecentSlice = "summary-with-recent-slice";

    /// <summary>
    /// 历史预算耗尽。
    /// </summary>
    public const string BudgetExhausted = "budget-exhausted";
}
