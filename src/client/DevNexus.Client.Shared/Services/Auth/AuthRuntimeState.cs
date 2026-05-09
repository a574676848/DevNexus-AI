namespace DevNexus.Client.Shared.Services.Auth;

/// <summary>
/// 认证运行时共享状态。
/// </summary>
public sealed class AuthRuntimeState
{
    /// <summary>
    /// 访问令牌。
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// 刷新令牌。
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// 访问令牌过期时间。
    /// </summary>
    public DateTime TokenExpiry { get; set; } = DateTime.MinValue;

    /// <summary>
    /// 当前用户 ID。
    /// </summary>
    public Guid? CurrentUserId { get; set; }

    /// <summary>
    /// 当前用户角色。
    /// </summary>
    public List<string> CurrentUserRoles { get; set; } = new();

    /// <summary>
    /// 会话恢复锁。
    /// </summary>
    public SemaphoreSlim RestoreLock { get; } = new(1, 1);

    /// <summary>
    /// Token 刷新锁。
    /// </summary>
    public SemaphoreSlim RefreshLock { get; } = new(1, 1);

    /// <summary>
    /// 会话恢复任务。
    /// </summary>
    public Task<bool>? RestoreSessionTask { get; set; }

    /// <summary>
    /// Token 刷新任务。
    /// </summary>
    public Task<bool>? RefreshTokenTask { get; set; }
}
