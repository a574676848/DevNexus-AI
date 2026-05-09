namespace DevNexus.Shared.DTOs;

/// <summary>
/// 更新决策请求。
/// </summary>
public class UpdateManifestRequest
{
    public string Platform { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Channel { get; set; } = "stable";
    public string CurrentVersion { get; set; } = string.Empty;
    public string? InstallationId { get; set; }
    public string? UserIdHash { get; set; }
    public string? TenantId { get; set; }
    public string? OsVersion { get; set; }
    public IList<string> ClientCapabilities { get; set; } = new List<string>();
}

/// <summary>
/// 更新决策类型。
/// </summary>
public enum UpdateDecision
{
    None = 0,
    Recommended = 1,
    Required = 2
}

/// <summary>
/// 客户端更新执行状态。
/// </summary>
public enum UpdateExecutionStatus
{
    Idle = 0,
    Checking = 1,
    Available = 2,
    Downloading = 3,
    Verifying = 4,
    ReadyToInstall = 5,
    LaunchingUpdater = 6,
    Restarting = 7,
    Completed = 8,
    Failed = 9,
    RolledBack = 10,
    Cancelled = 11
}

/// <summary>
/// 更新 Manifest。
/// </summary>
public class UpdateManifestResponse
{
    public string ManifestVersion { get; set; } = "1.0";
    public string ClientPlatform { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Channel { get; set; } = "stable";
    public string CurrentVersion { get; set; } = string.Empty;
    public UpdateDecision Decision { get; set; } = UpdateDecision.None;
    public bool Mandatory { get; set; }
    public UpdateReleaseDto? TargetRelease { get; set; }
    public IList<UpdateArtifactDto> Artifacts { get; set; } = new List<UpdateArtifactDto>();
    public string Reason { get; set; } = string.Empty;
    public Guid? RolloutId { get; set; }
    public DateTimeOffset ServerTime { get; set; } = DateTimeOffset.UtcNow;
    public string? Signature { get; set; }
}

/// <summary>
/// 命中的目标发布版本。
/// </summary>
public class UpdateReleaseDto
{
    public Guid ReleaseId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Channel { get; set; } = "stable";
    public string Title { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// 可供客户端下载的发布物。
/// </summary>
public class UpdateArtifactDto
{
    public Guid ArtifactId { get; set; }
    public Guid ReleaseId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string PackageType { get; set; } = "installer";
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? Checksum { get; set; }
    public string? Signature { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string? StorageKey { get; set; }
}
