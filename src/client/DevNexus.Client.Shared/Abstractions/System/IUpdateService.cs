using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 更新服务接口
/// </summary>
public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync();
    Task DownloadAndInstallAsync(UpdateInfo update, Action<int>? progress = null);
    Task ResumePendingUpdateAsync();
    Task IgnoreVersionAsync(string version);
    Task SnoozeVersionAsync(string version, TimeSpan duration);
    DateTime? LastCheckTime { get; }
    UpdatePresentationMode PresentationMode { get; }
    event Action<UpdateInfo>? UpdateAvailable;
    event Action<string, int>? DownloadProgressChanged;
    event Action<UpdateExecutionStatus>? UpdateStatusChanged;
}

/// <summary>
/// 更新交互模式
/// </summary>
public enum UpdatePresentationMode
{
    /// <summary>
    /// 支持下载安装并重启
    /// </summary>
    Installer = 0,

    /// <summary>
    /// 下载更新包后交由系统打开
    /// </summary>
    ManualDownload = 1,

    /// <summary>
    /// 仅提示刷新页面
    /// </summary>
    RefreshOnly = 2
}

/// <summary>
/// 客户端版本服务接口。
/// 用于统一获取当前应用版本号。
/// </summary>
public interface IClientVersionService
{
    /// <summary>
    /// 获取当前客户端版本号。
    /// </summary>
    string CurrentVersion { get; }
}

/// <summary>
/// 更新信息
/// </summary>
public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
    public DateTime ReleaseDate { get; set; }
    public string Channel { get; set; } = "stable";
    public string Architecture { get; set; } = string.Empty;
    public string DecisionReason { get; set; } = string.Empty;
    public string ManifestVersion { get; set; } = "1.0";
    public Guid? ReleaseId { get; set; }
    public Guid? ArtifactId { get; set; }
    public Guid? RolloutId { get; set; }

    /// <summary>
    /// 文件大小 (字节)
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// SHA256 校验和
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// 是否直接下载 (静默下载)
    /// </summary>
    public bool SilentDownload { get; set; }

    /// <summary>
    /// 更新包类型 (installer, portable, diff)
    /// </summary>
    public string PackageType { get; set; } = "installer";
}

/// <summary>
/// 客户端更新执行快照。
/// </summary>
public class UpdateExecutionSnapshot
{
    /// <summary>
    /// 当前执行状态。
    /// </summary>
    public UpdateExecutionStatus Status { get; set; } = UpdateExecutionStatus.Idle;

    /// <summary>
    /// 当前处理的更新信息。
    /// </summary>
    public UpdateInfo? UpdateInfo { get; set; }

    /// <summary>
    /// 已下载包路径。
    /// </summary>
    public string? PackagePath { get; set; }

    /// <summary>
    /// 最近一次错误摘要。
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// 快照更新时间。
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
