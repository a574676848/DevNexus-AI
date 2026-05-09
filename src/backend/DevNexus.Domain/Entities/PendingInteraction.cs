using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 挂起交互实体。
/// 用于保存等待用户补参、等待审批等会中断自动执行的运行时状态。
/// </summary>
public class PendingInteraction : AuditableEntity
{
    /// <summary>
    /// 所属聊天会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 所属聊天会话。
    /// </summary>
    public ChatSession ChatSession { get; set; } = null!;

    /// <summary>
    /// 关联的聊天消息标识。
    /// </summary>
    public Guid? MessageId { get; set; }

    /// <summary>
    /// 关联的聊天消息。
    /// </summary>
    public ChatMessage? Message { get; set; }

    /// <summary>
    /// 挂起交互类型。
    /// </summary>
    public PendingInteractionKind Kind { get; set; } = PendingInteractionKind.Unknown;

    /// <summary>
    /// 当前挂起交互状态。
    /// </summary>
    public PendingInteractionStatus Status { get; set; } = PendingInteractionStatus.Pending;

    /// <summary>
    /// 标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 说明文案。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 来源工具名称。
    /// </summary>
    public string? SourceTool { get; set; }

    /// <summary>
    /// 建议动作。
    /// </summary>
    public ToolSuggestedAction? SuggestedAction { get; set; }

    /// <summary>
    /// 请求数据。
    /// </summary>
    public Dictionary<string, object>? RequestedData { get; set; }

    /// <summary>
    /// 解决数据。
    /// </summary>
    public Dictionary<string, object>? ResolutionData { get; set; }

    /// <summary>
    /// 过期时间。
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 恢复令牌。
    /// </summary>
    public string? RetryToken { get; set; }
}
