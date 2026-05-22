namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天历史上下文压力原因。
/// </summary>
public static class ChatHistoryPressureReasons
{
    /// <summary>
    /// 未检测到上下文压力。
    /// </summary>
    public const string None = "none";

    /// <summary>
    /// 历史已进入摘要压缩。
    /// </summary>
    public const string SummaryCompression = "summary-compression";

    /// <summary>
    /// 历史因预算不足被截断。
    /// </summary>
    public const string BudgetTruncated = "budget-truncated";

    /// <summary>
    /// 存在被跳过的未完成助手消息。
    /// </summary>
    public const string IncompleteAssistantSkipped = "incomplete-assistant-skipped";
}
