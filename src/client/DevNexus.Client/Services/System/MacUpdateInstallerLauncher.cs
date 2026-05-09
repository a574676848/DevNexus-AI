using DevNexus.Client.Shared.Abstractions;
using Microsoft.Extensions.Logging;

namespace DevNexus.Client.Services.System;

/// <summary>
/// Mac 桌面端更新包启动器。
/// 下载完成后交给系统打开安装包。
/// </summary>
public sealed class MacUpdateInstallerLauncher : IUpdateInstallerLauncher
{
    private readonly ILogger<MacUpdateInstallerLauncher> _logger;

    public MacUpdateInstallerLauncher(ILogger<MacUpdateInstallerLauncher> logger)
    {
        _logger = logger;
    }

    public async Task LaunchAsync(string packagePath, UpdateInfo update, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            throw new FileNotFoundException("未找到已下载的更新包。", packagePath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var request = new OpenFileRequest(
            string.IsNullOrWhiteSpace(update.FileName) ? Path.GetFileName(packagePath) : update.FileName,
            new ReadOnlyFile(packagePath));

        var opened = await Launcher.Default.OpenAsync(request);
        if (!opened)
        {
            throw new InvalidOperationException("系统未能打开更新安装包。");
        }

        _logger.LogInformation(
            "[MacUpdateInstallerLauncher] 已交由系统打开更新包 | File={PackagePath}",
            packagePath);
    }
}
