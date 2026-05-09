using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.AuthUseCases;

/// <summary>
/// 登出命令处理器接口。
/// </summary>
internal interface ILogoutCommandHandler
{
    Task<AuthResult> HandleAsync(
        Guid currentUserId,
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理当前设备登出命令。
/// </summary>
internal sealed class LogoutCommandHandler : ILogoutCommandHandler
{
    private readonly IAuthService _authService;

    public LogoutCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<AuthResult> HandleAsync(
        Guid currentUserId,
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = currentUserId;

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new List<string> { "刷新令牌不能为空" }
            };
        }

        await _authService.LogoutAsync(request.RefreshToken, cancellationToken);

        return new AuthResult
        {
            Succeeded = true,
            Message = "登出成功"
        };
    }
}
