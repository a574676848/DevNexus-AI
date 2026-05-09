using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.UserAdminUseCases;

/// <summary>
/// 创建用户命令处理器接口。
/// </summary>
internal interface ICreateUserCommandHandler
{
    Task<AuthResult> HandleAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理创建用户命令。
/// </summary>
internal sealed class CreateUserCommandHandler : ICreateUserCommandHandler
{
    private readonly IUserManagementService _userManagementService;

    public CreateUserCommandHandler(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    public Task<AuthResult> HandleAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
        => _userManagementService.CreateUserAsync(request, cancellationToken);
}
