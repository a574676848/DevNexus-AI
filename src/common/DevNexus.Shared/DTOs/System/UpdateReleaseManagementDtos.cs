namespace DevNexus.Shared.DTOs;

/// <summary>
/// 发布中心展示 DTO。
/// </summary>
public class ReleaseDto
{
    /// <summary>
    /// 发布版本标识。
    /// </summary>
    public Guid ReleaseId { get; set; }

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
    public string Status { get; set; } = "draft";

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 发布物列表。
    /// </summary>
    public IList<ReleaseArtifactDto> Artifacts { get; set; } = new List<ReleaseArtifactDto>();
}

/// <summary>
/// 发布物 DTO。
/// </summary>
public class ReleaseArtifactDto
{
    /// <summary>
    /// 发布物标识。
    /// </summary>
    public Guid ArtifactId { get; set; }

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
}

/// <summary>
/// 保存发布版本请求。
/// </summary>
public class SaveReleaseRequest
{
    /// <summary>
    /// 发布版本标识。
    /// </summary>
    public Guid? ReleaseId { get; set; }

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
    /// 发布状态。
    /// </summary>
    public string Status { get; set; } = "draft";

    /// <summary>
    /// 发布物输入列表。
    /// </summary>
    public IList<SaveReleaseArtifactRequest> Artifacts { get; set; } = new List<SaveReleaseArtifactRequest>();
}

/// <summary>
/// 保存发布物请求。
/// </summary>
public class SaveReleaseArtifactRequest
{
    /// <summary>
    /// 发布物标识。
    /// </summary>
    public Guid? ArtifactId { get; set; }

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
}

/// <summary>
/// 投放规则 DTO。
/// </summary>
public class RolloutDto
{
    /// <summary>
    /// 投放规则标识。
    /// </summary>
    public Guid RolloutId { get; set; }

    /// <summary>
    /// 关联发布版本标识。
    /// </summary>
    public Guid ReleaseId { get; set; }

    /// <summary>
    /// 目标发布版本号。
    /// </summary>
    public string ReleaseVersion { get; set; } = string.Empty;

    /// <summary>
    /// 发布标题。
    /// </summary>
    public string ReleaseTitle { get; set; } = string.Empty;

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
    public DateTime StartsAt { get; set; }

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
    /// 是否开启全局熔断。
    /// </summary>
    public bool KillSwitchEnabled { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间。
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 保存投放规则请求。
/// </summary>
public class SaveRolloutRequest
{
    /// <summary>
    /// 投放规则标识。
    /// </summary>
    public Guid? RolloutId { get; set; }

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
    /// 是否开启全局熔断。
    /// </summary>
    public bool KillSwitchEnabled { get; set; }
}

/// <summary>
/// 更新观测摘要 DTO。
/// </summary>
public class UpdateObservabilitySummaryDto
{
    /// <summary>
    /// 草稿发布数量。
    /// </summary>
    public int DraftReleaseCount { get; set; }

    /// <summary>
    /// 已发布版本数量。
    /// </summary>
    public int PublishedReleaseCount { get; set; }

    /// <summary>
    /// 已归档版本数量。
    /// </summary>
    public int ArchivedReleaseCount { get; set; }

    /// <summary>
    /// 激活中的投放数量。
    /// </summary>
    public int ActiveRolloutCount { get; set; }

    /// <summary>
    /// 暂停中的投放数量。
    /// </summary>
    public int PausedRolloutCount { get; set; }

    /// <summary>
    /// 强制更新投放数量。
    /// </summary>
    public int MandatoryRolloutCount { get; set; }

    /// <summary>
    /// 已触发 kill switch 的投放数量。
    /// </summary>
    public int KillSwitchCount { get; set; }

    /// <summary>
    /// 发布物数量。
    /// </summary>
    public int ArtifactCount { get; set; }

    /// <summary>
    /// 检查事件数量。
    /// </summary>
    public int CheckCount { get; set; }

    /// <summary>
    /// 命中更新数量。
    /// </summary>
    public int UpdateAvailableCount { get; set; }

    /// <summary>
    /// 下载开始数量。
    /// </summary>
    public int DownloadStartedCount { get; set; }

    /// <summary>
    /// 下载完成数量。
    /// </summary>
    public int DownloadCompletedCount { get; set; }

    /// <summary>
    /// 安装完成数量。
    /// </summary>
    public int InstallCompletedCount { get; set; }

    /// <summary>
    /// 更新失败数量。
    /// </summary>
    public int FailedCount { get; set; }
}
