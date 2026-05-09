using DevNexus.Client.Shared.Abstractions;
using Microsoft.Extensions.Logging;

using UpdateExecutionStatus = DevNexus.Shared.DTOs.UpdateExecutionStatus;

namespace DevNexus.Client.Services.System;

/// <summary>
/// MAUI 更新服务门面。
/// 负责编排版本检查、更新偏好判断以及更新包执行流程。
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private readonly IUpdateCoordinator _updateCoordinator;
    private readonly IUpdatePreferenceStore _preferenceStore;
    private readonly ILogger<UpdateService> _logger;

    /// <summary>
    /// 发生更新可用时触发。
    /// </summary>
    public event Action<UpdateInfo>? UpdateAvailable;

    /// <summary>
    /// 更新下载进度发生变化时触发。
    /// </summary>
    public event Action<string, int>? DownloadProgressChanged;

    /// <summary>
    /// 更新状态发生变化时触发。
    /// </summary>
    public event Action<UpdateExecutionStatus>? UpdateStatusChanged;

    /// <inheritdoc />
    public DateTime? LastCheckTime => _updateCoordinator.LastCheckTime;

    /// <inheritdoc />
    public UpdatePresentationMode PresentationMode => UpdatePresentationMode.Installer;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateService(
        IUpdateCoordinator updateCoordinator,
        IUpdatePreferenceStore preferenceStore,
        ILogger<UpdateService> logger)
    {
        _updateCoordinator = updateCoordinator;
        _preferenceStore = preferenceStore;
        _logger = logger;

        _updateCoordinator.UpdateAvailable += HandleUpdateAvailable;
        _updateCoordinator.StatusChanged += HandleStatusChanged;
    }

    /// <inheritdoc />
    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            return await _updateCoordinator.CheckForUpdateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UpdateService] 检查更新失败");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DownloadAndInstallAsync(UpdateInfo update, Action<int>? progress = null)
    {
        try
        {
            await _updateCoordinator.DownloadAndInstallAsync(
                update,
                percent =>
                {
                    progress?.Invoke(percent);
                    DownloadProgressChanged?.Invoke("下载进度", percent);
                });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UpdateService] 更新已取消");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UpdateService] 下载安装失败");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ResumePendingUpdateAsync()
    {
        try
        {
            await _updateCoordinator.ResumePendingUpdateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UpdateService] 恢复未完成更新失败");
        }
    }

    /// <inheritdoc />
    public Task IgnoreVersionAsync(string version)
    {
        return _preferenceStore.IgnoreVersionAsync(version);
    }

    /// <inheritdoc />
    public Task SnoozeVersionAsync(string version, TimeSpan duration)
    {
        return _preferenceStore.SnoozeVersionAsync(version, duration);
    }

    private void HandleUpdateAvailable(UpdateInfo updateInfo)
    {
        UpdateAvailable?.Invoke(updateInfo);
    }

    private void HandleStatusChanged(UpdateExecutionStatus status)
    {
        _logger.LogDebug("[UpdateService] 状态变更 | Status={Status}", status);
        UpdateStatusChanged?.Invoke(status);
    }
}
