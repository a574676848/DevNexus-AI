using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 客户端更新事件实体。
/// </summary>
public class UpdateClientEvent : AuditableEntity
{
    /// <summary>
    /// 客户端安装标识。
    /// </summary>
    public string InstallationId { get; set; } = string.Empty;

    /// <summary>
    /// 平台。
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// 架构。
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// 渠道。
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// 当前版本。
    /// </summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>
    /// 目标版本。
    /// </summary>
    public string TargetVersion { get; set; } = string.Empty;

    /// <summary>
    /// 关联投放规则。
    /// </summary>
    public Guid? RolloutId { get; set; }

    /// <summary>
    /// 关联发布版本。
    /// </summary>
    public Guid? ReleaseId { get; set; }

    /// <summary>
    /// 关联发布物。
    /// </summary>
    public Guid? ArtifactId { get; set; }

    /// <summary>
    /// 事件类型。
    /// </summary>
    public UpdateClientEventType EventType { get; set; } = UpdateClientEventType.Unknown;

    /// <summary>
    /// 结果状态。
    /// </summary>
    public UpdateClientEventResult Result { get; set; } = UpdateClientEventResult.Unknown;

    /// <summary>
    /// 失败原因。
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 失败说明。
    /// </summary>
    public string? ErrorMessage { get; set; }
}
