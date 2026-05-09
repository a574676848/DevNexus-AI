using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.UserAdminUseCases;

/// <summary>
/// 切换用户状态命令处理器接口。
/// </summary>
internal interface IToggleUserStatusCommandHandler
{
    Task<AuthResult> HandleAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理切换用户启用状态命令。
/// </summary>
internal sealed class ToggleUserStatusCommandHandler : IToggleUserStatusCommandHandler
{
    private readonly IUserManagementService _userManagementService;

    public ToggleUserStatusCommandHandler(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    public Task<AuthResult> HandleAsync(Guid userId, CancellationToken cancellationToken = default)
        => _userManagementService.ToggleUserStatusAsync(userId, cancellationToken);
}
