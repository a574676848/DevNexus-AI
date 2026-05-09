using System.Security;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using UpdateDecision = DevNexus.Shared.DTOs.UpdateDecision;
using UpdateExecutionStatus = DevNexus.Shared.DTOs.UpdateExecutionStatus;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 桌面端更新协调器。
/// </summary>
public sealed class UpdateCoordinator : IUpdateCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClientVersionService _clientVersionService;
    private readonly IClientEnvironmentService _clientEnvironmentService;
    private readonly IClientInstallationIdProvider _installationIdProvider;
    private readonly IUpdatePreferenceStore _preferenceStore;
    private readonly IUpdatePackageExecutor _packageExecutor;
    private readonly IUpdateInstallerLauncher _installerLauncher;
    private readonly IUpdateInstallResultStore _installResultStore;
    private readonly IUpdateStateStore _stateStore;
    private readonly ILogger<UpdateCoordinator> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateCoordinator(
        IServiceScopeFactory scopeFactory,
        IClientVersionService clientVersionService,
        IClientEnvironmentService clientEnvironmentService,
        IClientInstallationIdProvider installationIdProvider,
        IUpdatePreferenceStore preferenceStore,
        IUpdatePackageExecutor packageExecutor,
        IUpdateInstallerLauncher installerLauncher,
        IUpdateInstallResultStore installResultStore,
        IUpdateStateStore stateStore,
        ILogger<UpdateCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _clientVersionService = clientVersionService;
        _clientEnvironmentService = clientEnvironmentService;
        _installationIdProvider = installationIdProvider;
        _preferenceStore = preferenceStore;
        _packageExecutor = packageExecutor;
        _installerLauncher = installerLauncher;
        _installResultStore = installResultStore;
        _stateStore = stateStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public event Action<UpdateExecutionStatus>? StatusChanged;

    /// <inheritdoc />
    public event Action<UpdateInfo>? UpdateAvailable;

    /// <inheritdoc />
    public DateTime? LastCheckTime { get; private set; }

    /// <inheritdoc />
    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            await SetStatusAsync(UpdateExecutionStatus.Checking);
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
                await ClearStateAsync(UpdateExecutionStatus.Idle);
                return null;
            }

            if (response.Decision == UpdateDecision.Recommended &&
                await _preferenceStore.ShouldSkipVersionAsync(response.TargetRelease.Version))
            {
                await ClearStateAsync(UpdateExecutionStatus.Idle);
                return null;
            }

            var updateInfo = BuildUpdateInfo(response);
            await ReportEventAsync(await CreateEventAsync(updateInfo, UpdateClientEventType.UpdateAvailable, UpdateClientEventResult.Success));
            await SaveSnapshotAsync(UpdateExecutionStatus.Available, updateInfo);
            UpdateAvailable?.Invoke(updateInfo);

            _logger.LogInformation(
                "[UpdateCoordinator] 发现新版本 | Version={Version} IsCritical={IsCritical}",
                updateInfo.Version,
                updateInfo.IsCritical);

            return updateInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UpdateCoordinator] 检查更新失败");
            await SaveSnapshotAsync(UpdateExecutionStatus.Failed, null, null, ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DownloadAndInstallAsync(UpdateInfo update, Action<int>? progress = null)
    {
        try
        {
            await SaveSnapshotAsync(UpdateExecutionStatus.Downloading, update);
            await ReportEventAsync(await CreateEventAsync(update, UpdateClientEventType.DownloadStarted, UpdateClientEventResult.Success));
            var packagePath = await _packageExecutor.DownloadPackageAsync(update, progress);

            await ReportEventAsync(await CreateEventAsync(update, UpdateClientEventType.DownloadCompleted, UpdateClientEventResult.Success));
            await SaveSnapshotAsync(UpdateExecutionStatus.Verifying, update, packagePath);
            await _packageExecutor.VerifyPackageAsync(packagePath, update);

            await ReportEventAsync(await CreateEventAsync(update, UpdateClientEventType.VerifyCompleted, UpdateClientEventResult.Success));
            await SaveSnapshotAsync(UpdateExecutionStatus.ReadyToInstall, update, packagePath);
            await SaveSnapshotAsync(UpdateExecutionStatus.LaunchingUpdater, update, packagePath);
            await ReportEventAsync(await CreateEventAsync(update, UpdateClientEventType.UpdaterLaunched, UpdateClientEventResult.Success));
            await _installerLauncher.LaunchAsync(packagePath, update);

            await SaveSnapshotAsync(UpdateExecutionStatus.Restarting, update, packagePath);
        }
        catch (OperationCanceledException)
        {
            await SaveSnapshotAsync(UpdateExecutionStatus.Cancelled, update, null, "用户取消了更新流程");
            throw;
        }
        catch (SecurityException ex)
        {
            await SaveSnapshotAsync(UpdateExecutionStatus.Failed, update, null, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            await SaveSnapshotAsync(UpdateExecutionStatus.Failed, update, null, ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ResumePendingUpdateAsync()
    {
        var snapshot = await _stateStore.GetAsync();
        var installResult = await _installResultStore.GetAsync();

        if (installResult != null && snapshot?.UpdateInfo != null)
        {
            var isSuccess = string.Equals(installResult.Result, "success", StringComparison.OrdinalIgnoreCase);
            await ReportEventAsync(await CreateEventAsync(
                snapshot.UpdateInfo,
                isSuccess ? UpdateClientEventType.InstallCompleted : UpdateClientEventType.InstallFailed,
                isSuccess ? UpdateClientEventResult.Success : UpdateClientEventResult.Failed,
                isSuccess ? null : $"exit-code-{installResult.ExitCode}",
                installResult.ErrorMessage));

            await _installResultStore.ClearAsync();
            await _stateStore.ClearAsync();
            StatusChanged?.Invoke(isSuccess ? UpdateExecutionStatus.Completed : UpdateExecutionStatus.Failed);
            return;
        }

        if (snapshot?.UpdateInfo == null)
        {
            return;
        }

        if (IsVersionInstalled(snapshot.UpdateInfo.Version))
        {
            await ReportEventAsync(await CreateEventAsync(snapshot.UpdateInfo, UpdateClientEventType.InstallCompleted, UpdateClientEventResult.Success));
            await _stateStore.ClearAsync();
            StatusChanged?.Invoke(UpdateExecutionStatus.Completed);
            return;
        }

        switch (snapshot.Status)
        {
            case UpdateExecutionStatus.ReadyToInstall:
            case UpdateExecutionStatus.LaunchingUpdater:
            case UpdateExecutionStatus.Restarting:
                if (!string.IsNullOrWhiteSpace(snapshot.PackagePath) && File.Exists(snapshot.PackagePath))
                {
                    _logger.LogInformation(
                        "[UpdateCoordinator] 恢复未完成更新 | Version={Version} Status={Status}",
                        snapshot.UpdateInfo.Version,
                        snapshot.Status);

                    await SaveSnapshotAsync(UpdateExecutionStatus.LaunchingUpdater, snapshot.UpdateInfo, snapshot.PackagePath);
                    await _installerLauncher.LaunchAsync(snapshot.PackagePath, snapshot.UpdateInfo);
                }
                else
                {
                    await ReportEventAsync(await CreateEventAsync(
                        snapshot.UpdateInfo,
                        UpdateClientEventType.InstallFailed,
                        UpdateClientEventResult.Failed,
                        "package-missing",
                        "更新包不存在，无法恢复安装"));
                    await SaveSnapshotAsync(UpdateExecutionStatus.Failed, snapshot.UpdateInfo, null, "更新包不存在，无法恢复安装");
                }
                break;
            case UpdateExecutionStatus.Downloading:
            case UpdateExecutionStatus.Verifying:
                await ReportEventAsync(await CreateEventAsync(
                    snapshot.UpdateInfo,
                    UpdateClientEventType.InstallFailed,
                    UpdateClientEventResult.Failed,
                    "restart-interrupted",
                    "应用重启导致更新中断"));
                await SaveSnapshotAsync(UpdateExecutionStatus.Failed, snapshot.UpdateInfo, snapshot.PackagePath, "应用重启导致更新中断");
                break;
        }
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

    private bool IsVersionInstalled(string version)
    {
        return CompareVersions(_clientVersionService.CurrentVersion, version) >= 0;
    }

    private static int CompareVersions(string? left, string? right)
    {
        return ParseVersion(left).CompareTo(ParseVersion(right));
    }

    private static Version ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Version(0, 0, 0, 0);
        }

        var normalized = value.Trim().TrimStart('v', 'V');
        var parts = normalized.Split('.');
        normalized = parts.Length switch
        {
            1 => $"{parts[0]}.0.0.0",
            2 => $"{parts[0]}.{parts[1]}.0.0",
            3 => $"{parts[0]}.{parts[1]}.{parts[2]}.0",
            _ => normalized
        };

        return Version.TryParse(normalized, out var version) ? version : new Version(0, 0, 0, 0);
    }

    private Task ClearStateAsync(UpdateExecutionStatus status)
    {
        StatusChanged?.Invoke(status);
        return _stateStore.ClearAsync();
    }

    private async Task SetStatusAsync(UpdateExecutionStatus status)
    {
        StatusChanged?.Invoke(status);
        var snapshot = await _stateStore.GetAsync();
        if (snapshot != null)
        {
            snapshot.Status = status;
            snapshot.UpdatedAtUtc = DateTime.UtcNow;
            await _stateStore.SaveAsync(snapshot);
        }
    }

    private async Task SaveSnapshotAsync(UpdateExecutionStatus status, UpdateInfo? updateInfo, string? packagePath = null, string? error = null)
    {
        StatusChanged?.Invoke(status);
        await _stateStore.SaveAsync(new UpdateExecutionSnapshot
        {
            Status = status,
            UpdateInfo = updateInfo,
            PackagePath = packagePath,
            LastError = error,
            UpdatedAtUtc = DateTime.UtcNow
        });
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
