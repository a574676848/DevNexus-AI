using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.AuthUseCases;

/// <summary>
/// 刷新令牌命令处理器接口。
/// </summary>
internal interface IRefreshTokenCommandHandler
{
    Task<TokenResponse?> HandleAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理刷新令牌命令。
/// </summary>
internal sealed class RefreshTokenCommandHandler : IRefreshTokenCommandHandler
{
    private readonly IAuthService _authService;

    public RefreshTokenCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task<TokenResponse?> HandleAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
        => _authService.RefreshTokenAsync(request.RefreshToken, ipAddress, cancellationToken);
}
