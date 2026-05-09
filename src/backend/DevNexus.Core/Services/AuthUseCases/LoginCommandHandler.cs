using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.AuthUseCases;

/// <summary>
/// 登录命令处理器接口。
/// </summary>
internal interface ILoginCommandHandler
{
    Task<TokenResponse?> HandleAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理用户登录命令。
/// </summary>
internal sealed class LoginCommandHandler : ILoginCommandHandler
{
    private readonly IAuthService _authService;

    public LoginCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task<TokenResponse?> HandleAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
        => _authService.LoginAsync(request, ipAddress, userAgent, cancellationToken);
}
