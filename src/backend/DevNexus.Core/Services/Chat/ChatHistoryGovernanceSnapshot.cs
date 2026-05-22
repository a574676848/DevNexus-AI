namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天历史上下文治理快照。
/// </summary>
public sealed class ChatHistoryGovernanceSnapshot
{
    /// <summary>
    /// 空治理快照。
    /// </summary>
    public static ChatHistoryGovernanceSnapshot Empty { get; } = new();

    /// <summary>
    /// 历史 Token 预算。
    /// </summary>
    public int BudgetTokens { get; init; }

    /// <summary>
    /// 已写入模型历史的估算 Token 数。
    /// </summary>
    public int ConsumedTokens { get; init; }

    /// <summary>
    /// 仓储读取到的消息数。
    /// </summary>
    public int FetchedMessageCount { get; init; }

    /// <summary>
    /// 可回放消息数。
    /// </summary>
    public int ReplayableMessageCount { get; init; }

    /// <summary>
    /// 直接写入的对话消息数。
    /// </summary>
    public int DirectMessageCount { get; init; }

    /// <summary>
    /// 摘要压缩覆盖的早期消息数。
    /// </summary>
    public int CompressedMessageCount { get; init; }

    /// <summary>
    /// 摘要消息数。
    /// </summary>
    public int SummaryMessageCount { get; init; }

    /// <summary>
    /// 摘要后追加的最近消息数。
    /// </summary>
    public int RecentMessageCount { get; init; }

    /// <summary>
    /// 历史压缩索引。
    /// </summary>
    public ChatHistoryCompressionIndex CompressionIndex { get; init; } =
        ChatHistoryCompressionIndex.Empty;

    /// <summary>
    /// 跳过的内部修复提示数。
    /// </summary>
    public int SkippedInternalRepairPromptCount { get; init; }

    /// <summary>
    /// 跳过的未完成助手消息数。
    /// </summary>
    public int SkippedIncompleteAssistantMessageCount { get; init; }

    /// <summary>
    /// 是否因为预算不足发生截断。
    /// </summary>
    public bool TruncatedByBudget { get; init; }

    /// <summary>
    /// 历史治理策略。
    /// </summary>
    public string Strategy { get; init; } = ChatHistoryGovernanceStrategies.Empty;
}
