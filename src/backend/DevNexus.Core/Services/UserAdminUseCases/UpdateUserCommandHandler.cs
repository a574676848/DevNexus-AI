using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.UserAdminUseCases;

/// <summary>
/// 更新用户命令处理器接口。
/// </summary>
internal interface IUpdateUserCommandHandler
{
    Task<AuthResult> HandleAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理更新用户命令。
/// </summary>
internal sealed class UpdateUserCommandHandler : IUpdateUserCommandHandler
{
    private readonly IUserManagementService _userManagementService;

    public UpdateUserCommandHandler(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    public Task<AuthResult> HandleAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
        => _userManagementService.UpdateUserAsync(userId, request, cancellationToken);
}
