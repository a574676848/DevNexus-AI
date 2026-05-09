using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.UserAdminUseCases;

/// <summary>
/// 删除用户命令处理器接口。
/// </summary>
internal interface IDeleteUserCommandHandler
{
    Task<AuthResult> HandleAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理删除用户命令。
/// </summary>
internal sealed class DeleteUserCommandHandler : IDeleteUserCommandHandler
{
    private readonly IUserManagementService _userManagementService;

    public DeleteUserCommandHandler(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    public Task<AuthResult> HandleAsync(Guid userId, CancellationToken cancellationToken = default)
        => _userManagementService.DeleteUserAsync(userId, cancellationToken);
}
