using System.Diagnostics;
using DevNexus.Client.Shared.Abstractions;
using Microsoft.Extensions.Logging;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 桌面端安装器启动器。
/// </summary>
public sealed class UpdateInstallerLauncher : IUpdateInstallerLauncher
{
    private const string UpdaterExecutableName = "DevNexus.Client.Updater.exe";
    private readonly IWindowService _windowService;
    private readonly ILogger<UpdateInstallerLauncher> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateInstallerLauncher(IWindowService windowService, ILogger<UpdateInstallerLauncher> logger)
    {
        _windowService = windowService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LaunchAsync(string packagePath, UpdateInfo update, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(update.PackageType, "installer", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"当前仅支持 installer 类型更新包，实际类型为 {update.PackageType}。");
        }

        var updaterPath = ResolveUpdaterPath();
        if (!File.Exists(updaterPath))
        {
            throw new FileNotFoundException("未找到独立 updater 可执行文件。", updaterPath);
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = updaterPath,
            UseShellExecute = true,
            Arguments = BuildArguments(packagePath)
        };

        await _windowService.SetApplicationRestartState(true);
        var process = Process.Start(processStartInfo);
        if (process == null)
        {
            throw new InvalidOperationException("无法启动 updater 进程");
        }

        _logger.LogInformation("[UpdateInstallerLauncher] 已启动 updater 进程 | PID={ProcessId}", process.Id);
        await Task.Delay(2000, cancellationToken);
        _windowService.CloseApplication();
    }

    private static string ResolveUpdaterPath()
    {
        return Path.Combine(AppContext.BaseDirectory, UpdaterExecutableName);
    }

    private static string BuildArguments(string packagePath)
    {
        return $"--installer \"{packagePath}\" --parent-pid {Environment.ProcessId}";
    }
}
