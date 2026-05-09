using DevNexus.Client.Shared.Abstractions;
using Microsoft.AspNetCore.Components;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;

using UpdateExecutionStatus = DevNexus.Shared.DTOs.UpdateExecutionStatus;
using UpdateDecision = DevNexus.Shared.DTOs.UpdateDecision;

namespace DevNexus.Client.Web.Services;

/// <summary>
/// Web 更新服务实现。
/// Web 端仅负责检测版本并提示用户刷新页面。
/// </summary>
public sealed class WebUpdateService : IUpdateService
{
    private readonly ISystemApiService _systemApiService;
    private readonly IClientVersionService _clientVersionService;
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<WebUpdateService> _logger;
    private DateTime? _lastCheckTime;

    /// <summary>
    /// 更新可用事件。
    /// </summary>
    public event Action<UpdateInfo>? UpdateAvailable;

    /// <summary>
    /// 下载进度事件。
    /// Web 端不执行下载，因此不会触发。
    /// </summary>
    public event Action<string, int>? DownloadProgressChanged;

    /// <summary>
    /// 更新状态事件。
    /// Web 端不执行安装，因此状态保持空闲。
    /// </summary>
    public event Action<UpdateExecutionStatus>? UpdateStatusChanged;

    /// <inheritdoc />
    public DateTime? LastCheckTime => _lastCheckTime;

    /// <inheritdoc />
    public UpdatePresentationMode PresentationMode => UpdatePresentationMode.RefreshOnly;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public WebUpdateService(
        ISystemApiService systemApiService,
        IClientVersionService clientVersionService,
        NavigationManager navigationManager,
        ILogger<WebUpdateService> logger)
    {
        _systemApiService = systemApiService;
        _clientVersionService = clientVersionService;
        _navigationManager = navigationManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            _lastCheckTime = DateTime.UtcNow;
            UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Checking);

            var currentVersion = _clientVersionService.CurrentVersion;
            var response = await _systemApiService.GetUpdateManifestAsync(new UpdateManifestRequest
            {
                Platform = "web",
                Architecture = "browser",
                Channel = "stable",
                CurrentVersion = currentVersion,
                OsVersion = Environment.OSVersion.VersionString,
                ClientCapabilities = new List<string> { "refresh-only" }
            });

            if (response == null ||
                response.Decision == UpdateDecision.None ||
                response.TargetRelease == null)
            {
                UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Idle);
                return null;
            }

            var artifact = response.Artifacts.FirstOrDefault();
            var updateInfo = new UpdateInfo
            {
                Version = response.TargetRelease.Version,
                ReleaseNotes = response.TargetRelease.ReleaseNotes,
                DownloadUrl = artifact?.DownloadUrl ?? string.Empty,
                FileName = artifact?.FileName ?? string.Empty,
                IsCritical = response.Mandatory,
                ReleaseDate = response.TargetRelease.PublishedAt,
                FileSize = artifact?.FileSize ?? 0,
                Checksum = artifact?.Checksum,
                SilentDownload = false,
                PackageType = artifact?.PackageType ?? "refresh",
                Channel = response.Channel,
                Architecture = response.Architecture,
                DecisionReason = response.Reason,
                ManifestVersion = response.ManifestVersion,
                ReleaseId = response.TargetRelease.ReleaseId,
                ArtifactId = artifact?.ArtifactId
            };

            UpdateAvailable?.Invoke(updateInfo);
            UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Available);

            _logger.LogInformation(
                "[WebUpdateService] 发现新版本 | Version={Version} IsCritical={IsCritical}",
                updateInfo.Version,
                updateInfo.IsCritical);

            return updateInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebUpdateService] 检查更新失败");
            UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Failed);
            return null;
        }
    }

    /// <inheritdoc />
    public Task DownloadAndInstallAsync(UpdateInfo update, Action<int>? progress = null)
    {
        progress?.Invoke(100);
        DownloadProgressChanged?.Invoke("Web 端请刷新页面以完成更新", 100);
        UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Completed);
        _navigationManager.NavigateTo(_navigationManager.Uri, forceLoad: true);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumePendingUpdateAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task IgnoreVersionAsync(string version)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SnoozeVersionAsync(string version, TimeSpan duration)
    {
        return Task.CompletedTask;
    }
}
