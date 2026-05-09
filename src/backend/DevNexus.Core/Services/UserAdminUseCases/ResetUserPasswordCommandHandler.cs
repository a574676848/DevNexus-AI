using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.UserAdminUseCases;

/// <summary>
/// 管理员重置用户密码命令处理器接口。
/// </summary>
internal interface IResetUserPasswordCommandHandler
{
    Task<AuthResult> HandleAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理管理员重置用户密码命令。
/// </summary>
internal sealed class ResetUserPasswordCommandHandler : IResetUserPasswordCommandHandler
{
    private readonly IUserManagementService _userManagementService;

    public ResetUserPasswordCommandHandler(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    public Task<AuthResult> HandleAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default)
        => _userManagementService.AdminResetPasswordAsync(
            new AdminResetPasswordRequest
            {
                UserId = userId,
                NewPassword = newPassword
            },
            cancellationToken);
}
