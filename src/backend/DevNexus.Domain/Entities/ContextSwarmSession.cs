using DevNexus.Shared.Enums;
using DevNexus.Domain.Entities.Base;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 上下文驱动 Swarm 会话实体
/// </summary>
public class ContextSwarmSession : AuditableEntity, ISoftDelete
{
    /// <summary>
    /// 外部显示的会话唯一标识 (例如 SW-XXXXXX)
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 会话标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 会话描述或用户原始请求
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 全局状态
    /// </summary>
    public SwarmStatus Status { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 最终汇总结果 (Markdown)
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 所属用户 ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 领域类型 (int 存储枚举值，恢复时强转为 DomainType)
    /// </summary>
    public int DomainType { get; set; }

    /// <summary>
    /// LLM 提供者 ID (恢复时需要知道使用哪个模型)
    /// </summary>
    public Guid ProviderId { get; set; }

    /// <summary>
    /// 工作包列表
    /// </summary>
    public virtual ICollection<ContextWorkPackageRecord> Packages { get; set; } = new List<ContextWorkPackageRecord>();

    /// <summary>
    /// 软删除标识 (显式覆盖基类以满足接口要求)
    /// </summary>
    public new bool IsDeleted { get; set; }
}
