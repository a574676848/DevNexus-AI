using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.AuthUseCases;

/// <summary>
/// 修改密码命令处理器接口。
/// </summary>
internal interface IChangePasswordCommandHandler
{
    Task<AuthResult> HandleAsync(
        Guid currentUserId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理修改密码命令。
/// </summary>
internal sealed class ChangePasswordCommandHandler : IChangePasswordCommandHandler
{
    private readonly IAuthService _authService;

    public ChangePasswordCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task<AuthResult> HandleAsync(
        Guid currentUserId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
        => _authService.ChangePasswordAsync(currentUserId, request, cancellationToken);
}
