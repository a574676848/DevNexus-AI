namespace DevNexus.Client.Shared;

/// <summary>
/// 更新服务配置选项
/// </summary>
public class UpdateOptions
{
    /// <summary>
    /// 检查更新间隔（小时）
    /// </summary>
    public int CheckIntervalHours { get; set; } = 24;

    /// <summary>
    /// 是否自动下载更新包
    /// </summary>
    public bool AutoDownload { get; set; } = false;

    /// <summary>
    /// 是否自动安装更新（下载完成后）
    /// </summary>
    public bool AutoInstall { get; set; } = false;

    /// <summary>
    /// 是否静默下载（不显示进度）
    /// </summary>
    public bool SilentDownload { get; set; } = false;

    /// <summary>
    /// 更新缓存目录路径
    /// </summary>
    public string? CacheDirectory { get; set; }
}
