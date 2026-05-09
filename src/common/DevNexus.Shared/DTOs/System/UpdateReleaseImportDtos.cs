namespace DevNexus.Shared.DTOs;

/// <summary>
/// 导入发布元数据请求。
/// </summary>
public class ImportReleaseMetadataRequest
{
    /// <summary>
    /// 版本号。
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 渠道。
    /// </summary>
    public string Channel { get; set; } = "stable";

    /// <summary>
    /// 标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 发行说明。
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// 发布物清单。
    /// </summary>
    public IList<ImportReleaseArtifactMetadata> Artifacts { get; set; } = new List<ImportReleaseArtifactMetadata>();

    /// <summary>
    /// 默认投放模板。
    /// </summary>
    public ImportReleaseRolloutTemplate? RolloutTemplate { get; set; }

    /// <summary>
    /// 导入后是否自动发布。
    /// </summary>
    public bool PublishRelease { get; set; } = true;

    /// <summary>
    /// 导入后是否自动创建投放规则。
    /// </summary>
    public bool CreateRollout { get; set; } = true;
}

/// <summary>
/// 导入发布物元数据。
/// </summary>
public class ImportReleaseArtifactMetadata
{
    public string Platform { get; set; } = string.Empty;
    public string Architecture { get; set; } = "any";
    public string PackageType { get; set; } = "installer";
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? Checksum { get; set; }
    public string? Signature { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string? StorageKey { get; set; }
}

/// <summary>
/// 导入投放模板。
/// </summary>
public class ImportReleaseRolloutTemplate
{
    public string Platform { get; set; } = string.Empty;
    public string Architecture { get; set; } = "any";
    public string Channel { get; set; } = "stable";
    public string MinimumSupportedVersion { get; set; } = string.Empty;
    public int RolloutPercent { get; set; } = 100;
    public string AudienceRule { get; set; } = "all";
    public bool ForceUpdate { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 导入发布元数据响应。
/// </summary>
public class ImportReleaseMetadataResult
{
    /// <summary>
    /// 导入后的发布版本。
    /// </summary>
    public ReleaseDto Release { get; set; } = new();

    /// <summary>
    /// 自动创建的投放规则。
    /// </summary>
    public RolloutDto? Rollout { get; set; }

    /// <summary>
    /// 是否已自动发布。
    /// </summary>
    public bool Published { get; set; }
}
