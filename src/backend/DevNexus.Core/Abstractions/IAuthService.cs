using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 认证服务接口。
/// 注意：本系统不开放注册，管理员账户通过数据库种子创建。
/// </summary>
public interface IAuthService
{
    Task<TokenResponse?> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    Task<TokenResponse?> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task LogoutAllDevicesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AuthResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<UserInfo?> GetUserInfoAsync(Guid userId, CancellationToken cancellationToken = default);
}
