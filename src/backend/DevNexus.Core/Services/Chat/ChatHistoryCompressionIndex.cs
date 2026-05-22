namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天历史压缩索引。
/// </summary>
public sealed class ChatHistoryCompressionIndex
{
    /// <summary>
    /// 空压缩索引。
    /// </summary>
    public static ChatHistoryCompressionIndex Empty { get; } = new();

    /// <summary>
    /// 是否存在压缩摘要索引。
    /// </summary>
    public bool HasIndex { get; init; }

    /// <summary>
    /// 压缩覆盖的历史消息数。
    /// </summary>
    public int CoveredMessageCount { get; init; }

    /// <summary>
    /// 摘要字符数。
    /// </summary>
    public int SummaryCharacterCount { get; init; }

    /// <summary>
    /// 摘要稳定指纹。
    /// </summary>
    public string SummaryFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// 主题提示。
    /// </summary>
    public IReadOnlyList<string> TopicHints { get; init; } = Array.Empty<string>();
}
