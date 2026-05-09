using DevNexus.Client.Shared.Abstractions;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 更新包处理器。
/// 负责下载与校验更新包，不直接拉起安装器。
/// </summary>
public interface IUpdatePackageExecutor
{
    /// <summary>
    /// 下载更新包。
    /// </summary>
    Task<string> DownloadPackageAsync(
        UpdateInfo update,
        Action<int>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 校验更新包。
    /// </summary>
    Task VerifyPackageAsync(
        string packagePath,
        UpdateInfo update,
        CancellationToken cancellationToken = default);
}
