using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 认证服务接口
/// 注意：本系统不开放注册，管理员账户通过数据库种子创建
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <param name="ipAddress">客户端IP地址</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Token 响应</returns>
    Task<TokenResponse?> LoginAsync(
        LoginRequest request, 
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 刷新令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="ipAddress">客户端IP地址</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Token 响应</returns>
    Task<TokenResponse?> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 登出（撤销刷新令牌）
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 登出所有设备
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task LogoutAllDevicesAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 修改密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="request">修改密码请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>认证结果</returns>
    Task<AuthResult> ChangePasswordAsync(
        Guid userId, 
        ChangePasswordRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取用户信息
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户信息</returns>
    Task<UserInfo?> GetUserInfoAsync(Guid userId, CancellationToken cancellationToken = default);
}
