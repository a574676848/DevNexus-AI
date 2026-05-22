namespace DevNexus.Shared.DTOs;

/// <summary>
/// 挂起交互摘要 DTO。
/// </summary>
public sealed class PendingInteractionSummaryDto
{
    /// <summary>
    /// 视觉语义。
    /// </summary>
    public string Tone { get; set; } = "warning";

    /// <summary>
    /// 主状态标签。
    /// </summary>
    public string Label { get; set; } = "等待用户处理";

    /// <summary>
    /// 低噪说明文案。
    /// </summary>
    public string Description { get; set; } = "当前会话仍有待处理交互。";

    /// <summary>
    /// 输入框占位文案。
    /// </summary>
    public string InputPlaceholder { get; set; } = "请先完成待处理交互";

    /// <summary>
    /// 建议下一步动作。
    /// </summary>
    public string NextAction { get; set; } = "ResolveInteraction";

    /// <summary>
    /// 是否阻塞继续发送普通消息。
    /// </summary>
    public bool BlocksMessageSend { get; set; } = true;
}
