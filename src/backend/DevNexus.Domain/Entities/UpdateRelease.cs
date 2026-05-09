using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 客户端更新发布版本实体。
/// </summary>
public class UpdateRelease : AuditableEntity
{
    /// <summary>
    /// 语义版本号。
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 发布轨道。
    /// </summary>
    public string Channel { get; set; } = "stable";

    /// <summary>
    /// 发布标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 发行说明。
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// 发布时间。
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// 发布状态。
    /// </summary>
    public UpdateReleaseStatus Status { get; set; } = UpdateReleaseStatus.Draft;

    /// <summary>
    /// 关联发布物列表。
    /// </summary>
    public ICollection<UpdateReleaseArtifact> Artifacts { get; set; } = new List<UpdateReleaseArtifact>();
}
