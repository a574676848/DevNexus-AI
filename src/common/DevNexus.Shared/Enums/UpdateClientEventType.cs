namespace DevNexus.Shared.Enums;

/// <summary>
/// 客户端更新事件类型。
/// </summary>
public enum UpdateClientEventType
{
    /// <summary>
    /// 未知事件。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 检查更新。
    /// </summary>
    Check = 1,

    /// <summary>
    /// 发现可用更新。
    /// </summary>
    UpdateAvailable = 2,

    /// <summary>
    /// 开始下载。
    /// </summary>
    DownloadStarted = 3,

    /// <summary>
    /// 下载完成。
    /// </summary>
    DownloadCompleted = 4,

    /// <summary>
    /// 校验完成。
    /// </summary>
    VerifyCompleted = 5,

    /// <summary>
    /// 已启动更新器。
    /// </summary>
    UpdaterLaunched = 6,

    /// <summary>
    /// 已打开安装器。
    /// </summary>
    InstallerOpened = 7,

    /// <summary>
    /// 安装完成。
    /// </summary>
    InstallCompleted = 8,

    /// <summary>
    /// 安装失败。
    /// </summary>
    InstallFailed = 9
}

/// <summary>
/// 客户端更新事件类型字符串协议扩展。
/// </summary>
public static class UpdateClientEventTypeExtensions
{
    /// <summary>
    /// 转换为前后端传输使用的字符串值。
    /// </summary>
    public static string ToWireValue(this UpdateClientEventType type)
    {
        return type switch
        {
            UpdateClientEventType.Check => "check",
            UpdateClientEventType.UpdateAvailable => "update_available",
            UpdateClientEventType.DownloadStarted => "download_started",
            UpdateClientEventType.DownloadCompleted => "download_completed",
            UpdateClientEventType.VerifyCompleted => "verify_completed",
            UpdateClientEventType.UpdaterLaunched => "updater_launched",
            UpdateClientEventType.InstallerOpened => "installer_opened",
            UpdateClientEventType.InstallCompleted => "install_completed",
            UpdateClientEventType.InstallFailed => "install_failed",
            _ => "unknown"
        };
    }

    /// <summary>
    /// 从字符串协议值解析为枚举。
    /// </summary>
    public static UpdateClientEventType Parse(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized switch
        {
            "check" => UpdateClientEventType.Check,
            "update_available" => UpdateClientEventType.UpdateAvailable,
            "download_started" => UpdateClientEventType.DownloadStarted,
            "download_completed" => UpdateClientEventType.DownloadCompleted,
            "verify_completed" => UpdateClientEventType.VerifyCompleted,
            "updater_launched" => UpdateClientEventType.UpdaterLaunched,
            "installer_opened" => UpdateClientEventType.InstallerOpened,
            "install_completed" => UpdateClientEventType.InstallCompleted,
            "install_failed" => UpdateClientEventType.InstallFailed,
            _ => UpdateClientEventType.Unknown
        };
    }
}