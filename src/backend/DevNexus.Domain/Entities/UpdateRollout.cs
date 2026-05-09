using DevNexus.Domain.Entities.Base;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 客户端更新投放规则实体。
/// </summary>
public class UpdateRollout : AuditableEntity
{
    /// <summary>
    /// 目标发布版本标识。
    /// </summary>
    public Guid ReleaseId { get; set; }

    /// <summary>
    /// 平台。
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// 架构。
    /// </summary>
    public string Architecture { get; set; } = "any";

    /// <summary>
    /// 渠道。
    /// </summary>
    public string Channel { get; set; } = "stable";

    /// <summary>
    /// 最低支持版本。
    /// </summary>
    public string MinimumSupportedVersion { get; set; } = string.Empty;

    /// <summary>
    /// 是否强制更新。
    /// </summary>
    public bool ForceUpdate { get; set; }

    /// <summary>
    /// 灰度比例。
    /// </summary>
    public int RolloutPercent { get; set; } = 100;

    /// <summary>
    /// 目标人群规则。
    /// </summary>
    public string AudienceRule { get; set; } = "all";

    /// <summary>
    /// 生效开始时间。
    /// </summary>
    public DateTime StartsAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 生效结束时间。
    /// </summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>
    /// 优先级。
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否启用全局熔断。
    /// </summary>
    public bool KillSwitchEnabled { get; set; }

    /// <summary>
    /// 发布版本导航属性。
    /// </summary>
    public UpdateRelease? Release { get; set; }
}
