using DevNexus.Domain.Entities.Base;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 客户端更新发布物实体。
/// </summary>
public class UpdateReleaseArtifact : AuditableEntity
{
    /// <summary>
    /// 关联发布版本标识。
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
    /// 包类型。
    /// </summary>
    public string PackageType { get; set; } = "installer";

    /// <summary>
    /// 文件名。
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小。
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 校验和。
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// 签名。
    /// </summary>
    public string? Signature { get; set; }

    /// <summary>
    /// 下载地址。
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 存储键。
    /// </summary>
    public string? StorageKey { get; set; }

    /// <summary>
    /// 发布版本导航属性。
    /// </summary>
    public UpdateRelease? Release { get; set; }
}
