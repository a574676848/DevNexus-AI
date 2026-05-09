namespace DevNexus.Client.Services.System;

/// <summary>
/// 更新版本偏好存储。
/// 负责管理忽略版本与稍后提醒状态。
/// </summary>
public interface IUpdatePreferenceStore
{
    /// <summary>
    /// 忽略指定版本。
    /// </summary>
    Task IgnoreVersionAsync(string version);

    /// <summary>
    /// 延后提醒指定版本。
    /// </summary>
    Task SnoozeVersionAsync(string version, TimeSpan duration);

    /// <summary>
    /// 判断版本是否应被跳过。
    /// </summary>
    Task<bool> ShouldSkipVersionAsync(string version);
}
