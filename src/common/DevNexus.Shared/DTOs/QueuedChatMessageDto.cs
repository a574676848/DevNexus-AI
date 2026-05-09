using DevNexus.Shared.Constants;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 排队消息 DTO。
/// </summary>
public class QueuedChatMessageDto
{
    /// <summary>
    /// 排队消息 ID。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 会话 ID。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 消息内容。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 消息类型。
    /// </summary>
    public string MessageType { get; set; } = ChatConstants.MessageTypeText;

    /// <summary>
    /// 选中的 Skill 名称。
    /// </summary>
    public string? SelectedSkillName { get; set; }

    /// <summary>
    /// 排队状态。
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// 序号。
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 开始派发时间。
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 完成时间。
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 取消时间。
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// 失败原因。
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// 最终生成的真实消息 ID。
    /// </summary>
    public Guid? ActualMessageId { get; set; }
}
