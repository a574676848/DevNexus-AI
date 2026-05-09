using DevNexus.Domain.Entities.Base;
using DevNexus.Domain.Enums;
using DevNexus.Shared.Constants;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 排队等待发送的聊天消息实体
/// 与 <see cref="ChatMessage"/> 职责不同：本实体仅表示"尚未进入发送链路"的待发消息，
/// 只有在被调度器派发后才会创建对应的 <see cref="ChatMessage"/>。
/// </summary>
public class QueuedChatMessage : AuditableEntity
{
    /// <summary>
    /// 所属聊天会话ID
    /// </summary>
    public Guid ChatSessionId { get; set; }

    /// <summary>
    /// 所属聊天会话
    /// </summary>
    public ChatSession ChatSession { get; set; } = null!;

    /// <summary>
    /// 发起用户ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 父消息ID（用于关联到当前会话最新一条真实消息）
    /// </summary>
    public Guid? ParentMessageId { get; set; } = null;

    /// <summary>
    /// 消息文本内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 消息类型（如 "text"）
    /// </summary>
    public string MessageType { get; set; } = ChatConstants.MessageTypeText;

    /// <summary>
    /// 选中的 Skill 名称
    /// </summary>
    public string? SelectedSkillName { get; set; }

    /// <summary>
    /// 关联的 Artifact ID 列表（JSON 序列化）
    /// </summary>
    public string? ArtifactIdsJson { get; set; }

    /// <summary>
    /// LLM Provider ID（用户选择的具体模型提供者）
    /// </summary>
    public Guid? LLMProviderId { get; set; }

    /// <summary>
    /// 附加元数据（JSON 序列化）
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// 排队状态
    /// </summary>
    public QueuedMessageStatus Status { get; set; } = QueuedMessageStatus.Pending;

    /// <summary>
    /// 序号（同一会话内按此字段保证 FIFO 顺序）
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// 开始派发时间（从 Pending 进入 Dispatching 时记录）
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 取消时间
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// 失败原因（仅 Status 为 Failed 时有值）
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// 最终生成的真实 ChatMessage ID（派发后回填）
    /// </summary>
    public Guid? ActualMessageId { get; set; }
}
