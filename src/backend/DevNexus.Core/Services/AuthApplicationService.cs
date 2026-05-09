using DevNexus.Shared.DTOs.Auth;
using DevNexus.Core.Services.AuthUseCases;

namespace DevNexus.Core.Services;

/// <summary>
/// 认证应用层编排服务。
/// 负责为控制器聚合具体用例处理器，保持 API 层轻量。
/// </summary>
internal sealed class AuthApplicationService : IAuthApplicationService
{
    private readonly ILoginCommandHandler _loginCommandHandler;
    private readonly IRefreshTokenCommandHandler _refreshTokenCommandHandler;
    private readonly ILogoutCommandHandler _logoutCommandHandler;
    private readonly ILogoutAllDevicesCommandHandler _logoutAllDevicesCommandHandler;
    private readonly IChangePasswordCommandHandler _changePasswordCommandHandler;
    private readonly IGetCurrentUserQueryHandler _getCurrentUserQueryHandler;
    private readonly IUpdateProfileCommandHandler _updateProfileCommandHandler;

    public AuthApplicationService(
        ILoginCommandHandler loginCommandHandler,
        IRefreshTokenCommandHandler refreshTokenCommandHandler,
        ILogoutCommandHandler logoutCommandHandler,
        ILogoutAllDevicesCommandHandler logoutAllDevicesCommandHandler,
        IChangePasswordCommandHandler changePasswordCommandHandler,
        IGetCurrentUserQueryHandler getCurrentUserQueryHandler,
        IUpdateProfileCommandHandler updateProfileCommandHandler)
    {
        _loginCommandHandler = loginCommandHandler;
        _refreshTokenCommandHandler = refreshTokenCommandHandler;
        _logoutCommandHandler = logoutCommandHandler;
        _logoutAllDevicesCommandHandler = logoutAllDevicesCommandHandler;
        _changePasswordCommandHandler = changePasswordCommandHandler;
        _getCurrentUserQueryHandler = getCurrentUserQueryHandler;
        _updateProfileCommandHandler = updateProfileCommandHandler;
    }

    public Task<TokenResponse?> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
        => _loginCommandHandler.HandleAsync(request, ipAddress, userAgent, cancellationToken);

    public Task<TokenResponse?> RefreshTokenAsync(
        RefreshTokenRequest request,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
        => _refreshTokenCommandHandler.HandleAsync(request, ipAddress, cancellationToken);

    public Task<AuthResult> LogoutAsync(
        Guid currentUserId,
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
        => _logoutCommandHandler.HandleAsync(currentUserId, request, cancellationToken);

    public Task LogoutAllDevicesAsync(Guid currentUserId, CancellationToken cancellationToken = default)
        => _logoutAllDevicesCommandHandler.HandleAsync(currentUserId, cancellationToken);

    public Task<AuthResult> ChangePasswordAsync(
        Guid currentUserId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
        => _changePasswordCommandHandler.HandleAsync(currentUserId, request, cancellationToken);

    public Task<UserInfo?> GetCurrentUserAsync(Guid currentUserId, CancellationToken cancellationToken = default)
        => _getCurrentUserQueryHandler.HandleAsync(currentUserId, cancellationToken);

    public Task<AuthResult> UpdateProfileAsync(
        Guid currentUserId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
        => _updateProfileCommandHandler.HandleAsync(currentUserId, request, cancellationToken);
}
