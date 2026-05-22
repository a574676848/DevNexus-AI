namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天历史上下文压力摘要。
/// </summary>
public sealed class ChatHistoryPressureSummary
{
    /// <summary>
    /// 是否存在上下文压力。
    /// </summary>
    public bool HasPressure { get; init; }

    /// <summary>
    /// 主要压力原因。
    /// </summary>
    public string PrimaryReason { get; init; } = ChatHistoryPressureReasons.None;
}
