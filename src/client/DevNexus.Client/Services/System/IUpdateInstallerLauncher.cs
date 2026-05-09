using DevNexus.Client.Shared.Abstractions;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 安装器启动器。
/// </summary>
public interface IUpdateInstallerLauncher
{
    /// <summary>
    /// 启动安装流程。
    /// </summary>
    Task LaunchAsync(string packagePath, UpdateInfo update, CancellationToken cancellationToken = default);
}

/// <summary>
/// 更新协调器。
/// </summary>
public interface IUpdateCoordinator
{
    /// <summary>
    /// 更新状态变化事件。
    /// </summary>
    event Action<DevNexus.Shared.DTOs.UpdateExecutionStatus>? StatusChanged;

    /// <summary>
    /// 更新可用事件。
    /// </summary>
    event Action<UpdateInfo>? UpdateAvailable;

    /// <summary>
    /// 最近一次检查时间。
    /// </summary>
    DateTime? LastCheckTime { get; }

    /// <summary>
    /// 检查新版本。
    /// </summary>
    Task<UpdateInfo?> CheckForUpdateAsync();

    /// <summary>
    /// 下载并安装更新。
    /// </summary>
    Task DownloadAndInstallAsync(UpdateInfo update, Action<int>? progress = null);

    /// <summary>
    /// 恢复未完成的更新。
    /// </summary>
    Task ResumePendingUpdateAsync();
}
