namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 经验提纯候选消息。
/// </summary>
public sealed class ExperienceDistillationQaMessage
{
    /// <summary>
    /// 发送者类型。
    /// </summary>
    public required string SenderType { get; init; }

    /// <summary>
    /// 文本内容。
    /// </summary>
    public required string Text { get; init; }
}
