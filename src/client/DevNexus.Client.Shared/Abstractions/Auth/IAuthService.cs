namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 认证服务接口 - 管理 Token 和用户认证状态
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 是否已认证
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// 当前用户ID
    /// </summary>
    Guid? CurrentUserId { get; }

    /// <summary>
    /// 当前用户角色列表
    /// </summary>
    IReadOnlyList<string> CurrentUserRoles { get; }

    /// <summary>
    /// 认证状态变更事件
    /// </summary>
    event Action<bool>? OnAuthStateChanged;

    /// <summary>
    /// 获取 API 基础URL
    /// </summary>
    Task<string> GetApiBaseUrlAsync();

    /// <summary>
    /// 获取 Access Token
    /// </summary>
    Task<string?> GetAccessTokenAsync();

    /// <summary>
    /// 尝试刷新 Token
    /// </summary>
    Task<bool> TryRefreshTokenAsync();

    /// <summary>
    /// 登录
    /// </summary>
    Task<bool> LoginAsync(string username, string password);

    /// <summary>
    /// 登出
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// 尝试恢复会话 (从存储加载 Token)
    /// </summary>
    Task<bool> TryRestoreSessionAsync();
}

