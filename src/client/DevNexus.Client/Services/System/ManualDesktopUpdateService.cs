using DevNexus.Client.Shared.Abstractions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using UpdateDecision = DevNexus.Shared.DTOs.UpdateDecision;
using UpdateExecutionStatus = DevNexus.Shared.DTOs.UpdateExecutionStatus;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 非 Windows 桌面端更新服务。
/// 负责检查、下载并打开安装包，不执行应用重启接管。
/// </summary>
public sealed class ManualDesktopUpdateService : IUpdateService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClientVersionService _clientVersionService;
    private readonly IClientEnvironmentService _clientEnvironmentService;
    private readonly IClientInstallationIdProvider _installationIdProvider;
    private readonly IUpdatePreferenceStore _preferenceStore;
    private readonly IUpdatePackageExecutor _packageExecutor;
    private readonly IUpdateInstallerLauncher _installerLauncher;
    private readonly ILogger<ManualDesktopUpdateService> _logger;

    public ManualDesktopUpdateService(
        IServiceScopeFactory scopeFactory,
        IClientVersionService clientVersionService,
        IClientEnvironmentService clientEnvironmentService,
        IClientInstallationIdProvider installationIdProvider,
        IUpdatePreferenceStore preferenceStore,
        IUpdatePackageExecutor packageExecutor,
        IUpdateInstallerLauncher installerLauncher,
        ILogger<ManualDesktopUpdateService> logger)
    {
        _scopeFactory = scopeFactory;
        _clientVersionService = clientVersionService;
        _clientEnvironmentService = clientEnvironmentService;
        _installationIdProvider = installationIdProvider;
        _preferenceStore = preferenceStore;
        _packageExecutor = packageExecutor;
        _installerLauncher = installerLauncher;
        _logger = logger;
    }

    public event Action<UpdateInfo>? UpdateAvailable;

    public event Action<string, int>? DownloadProgressChanged;

    public event Action<UpdateExecutionStatus>? UpdateStatusChanged;

    public DateTime? LastCheckTime { get; private set; }

    public UpdatePresentationMode PresentationMode => UpdatePresentationMode.ManualDownload;

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Checking);
            LastCheckTime = DateTime.UtcNow;

            await ReportEventAsync(new ReportUpdateClientEventRequest
            {
                InstallationId = await _installationIdProvider.GetInstallationIdAsync(),
                Platform = _clientEnvironmentService.UpdatePlatform,
                Architecture = _clientEnvironmentService.Architecture,
                Channel = "stable",
                CurrentVersion = _clientVersionService.CurrentVersion,
                EventType = UpdateClientEventType.Check.ToWireValue(),
                Result = UpdateClientEventResult.Success.ToWireValue()
            });

            var response = await RequestManifestAsync();
            if (response == null ||
                response.Decision == UpdateDecision.None ||
                response.TargetRelease == null)
            {
                UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Idle);
                return null;
            }

            if (response.Decision == UpdateDecision.Recommended &&
                await _preferenceStore.ShouldSkipVersionAsync(response.TargetRelease.Version))
            {
                UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Idle);
                return null;
            }

            var update = BuildUpdateInfo(response);
            if (string.IsNullOrWhiteSpace(update.DownloadUrl))
            {
                _logger.LogWarning("[ManualDesktopUpdateService] Manifest 未返回可下载安装包");
                UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Idle);
                return null;
            }

            await ReportEventAsync(await CreateEventAsync(update, UpdateClientEventType.UpdateAvailable, UpdateClientEventResult.Success));
            UpdateAvailable?.Invoke(update);
            UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Available);
            return update;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ManualDesktopUpdateService] 检查更新失败");
            UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Failed);
            return null;
        }
    }

    public async Task DownloadAndInstallAsync(UpdateInfo update, Action<int>? progress = null)
    {
        try
        {
            UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Downloading);
            await ReportEventAsync(await CreateEventAsync(update, UpdateClientEventType.DownloadStarted, UpdateClientEventResult.Success));

            var packagePath = await _packageExecutor.DownloadPackageAsync(
                update,
                percent =>
                {
                    progress?.Invoke(percent);
                    DownloadProgressChanged?.Invoke("下载进度", percent);
                });

            await ReportEventAsync(await CreateEventAsync(update, UpdateClientEventType.DownloadCompleted, UpdateClientEventResult.Success));
            UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Verifying);
            await _packageExecutor.VerifyPackageAsync(packagePath, update);
            await ReportEventAsync(await CreateEventAsync(update, UpdateClientEventType.VerifyCompleted, UpdateClientEventResult.Success));

            UpdateStatusChanged?.Invoke(UpdateExecutionStatus.ReadyToInstall);
            await _installerLauncher.LaunchAsync(packagePath, update);
            await ReportEventAsync(await CreateEventAsync(update, UpdateClientEventType.InstallerOpened, UpdateClientEventResult.Success));
            UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Cancelled);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ManualDesktopUpdateService] 下载更新失败");
            UpdateStatusChanged?.Invoke(UpdateExecutionStatus.Failed);
            await ReportEventAsync(await CreateEventAsync(update, UpdateClientEventType.InstallFailed, UpdateClientEventResult.Failed, "manual-update-failed", ex.Message));
            throw;
        }
    }

    public Task ResumePendingUpdateAsync()
    {
        return Task.CompletedTask;
    }

    public Task IgnoreVersionAsync(string version)
    {
        return _preferenceStore.IgnoreVersionAsync(version);
    }

    public Task SnoozeVersionAsync(string version, TimeSpan duration)
    {
        return _preferenceStore.SnoozeVersionAsync(version, duration);
    }

    private async Task<UpdateManifestResponse?> RequestManifestAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var systemApiService = scope.ServiceProvider.GetRequiredService<ISystemApiService>();
        return await systemApiService.GetUpdateManifestAsync(new UpdateManifestRequest
        {
            Platform = _clientEnvironmentService.UpdatePlatform,
            Architecture = _clientEnvironmentService.Architecture,
            Channel = "stable",
            CurrentVersion = _clientVersionService.CurrentVersion,
            InstallationId = await _installationIdProvider.GetInstallationIdAsync(),
            OsVersion = _clientEnvironmentService.OsVersion
        });
    }

    private static UpdateInfo BuildUpdateInfo(UpdateManifestResponse response)
    {
        var artifact = response.Artifacts.FirstOrDefault();
        return new UpdateInfo
        {
            Version = response.TargetRelease?.Version ?? string.Empty,
            ReleaseNotes = response.TargetRelease?.ReleaseNotes ?? string.Empty,
            DownloadUrl = artifact?.DownloadUrl ?? string.Empty,
            FileName = artifact?.FileName ?? string.Empty,
            IsCritical = response.Mandatory,
            ReleaseDate = response.TargetRelease?.PublishedAt ?? DateTime.UtcNow,
            FileSize = artifact?.FileSize ?? 0,
            Checksum = artifact?.Checksum,
            SilentDownload = false,
            PackageType = artifact?.PackageType ?? "installer",
            Channel = response.Channel,
            Architecture = response.Architecture,
            DecisionReason = response.Reason,
            ManifestVersion = response.ManifestVersion,
            RolloutId = response.RolloutId,
            ReleaseId = response.TargetRelease?.ReleaseId,
            ArtifactId = artifact?.ArtifactId
        };
    }

    private async Task<ReportUpdateClientEventRequest> CreateEventAsync(
        UpdateInfo updateInfo,
        UpdateClientEventType eventType,
        UpdateClientEventResult result,
        string? errorCode = null,
        string? errorMessage = null)
    {
        return new ReportUpdateClientEventRequest
        {
            InstallationId = await _installationIdProvider.GetInstallationIdAsync(),
            Platform = _clientEnvironmentService.UpdatePlatform,
            Architecture = string.IsNullOrWhiteSpace(updateInfo.Architecture)
                ? _clientEnvironmentService.Architecture
                : updateInfo.Architecture,
            Channel = string.IsNullOrWhiteSpace(updateInfo.Channel) ? "stable" : updateInfo.Channel,
            CurrentVersion = _clientVersionService.CurrentVersion,
            TargetVersion = updateInfo.Version,
            RolloutId = updateInfo.RolloutId,
            ReleaseId = updateInfo.ReleaseId,
            ArtifactId = updateInfo.ArtifactId,
            EventType = eventType.ToWireValue(),
            Result = result.ToWireValue(),
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    private async Task ReportEventAsync(ReportUpdateClientEventRequest request)
    {
        using var scope = _scopeFactory.CreateScope();
        var systemApiService = scope.ServiceProvider.GetRequiredService<ISystemApiService>();
        await systemApiService.ReportUpdateClientEventAsync(request);
    }
}
