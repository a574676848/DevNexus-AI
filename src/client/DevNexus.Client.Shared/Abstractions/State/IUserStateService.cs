using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 用户状态服务接口 - 管理全局用户信息状态，支持状态变更通知
/// </summary>
public interface IUserStateService
{
    /// <summary>
    /// 当前用户信息
    /// </summary>
    UserInfo? CurrentUser { get; }

    /// <summary>
    /// 用户信息变更事件
    /// </summary>
    event Func<UserInfo?, Task>? OnUserChanged;

    /// <summary>
    /// 更新用户信息（触发全局通知）
    /// </summary>
    Task UpdateUserAsync(UserInfo? user);

    /// <summary>
    /// 刷新用户信息（从 API 重新获取）
    /// </summary>
    Task RefreshUserAsync();

    /// <summary>
    /// 清除用户信息（登出时调用）
    /// </summary>
    void ClearUser();

    /// <summary>
    /// 获取头像完整 URL（委托给 IUrlService）
    /// </summary>
    string? GetAvatarUrl(string? avatarUrl);
}
