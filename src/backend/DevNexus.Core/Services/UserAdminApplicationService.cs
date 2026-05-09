using DevNexus.Shared.DTOs.Auth;
using DevNexus.Core.Services.UserAdminUseCases;

namespace DevNexus.Core.Services;

/// <summary>
/// 管理员用户管理应用层编排服务。
/// 负责把控制器请求路由到具体的用户管理用例处理器。
/// </summary>
internal sealed class UserAdminApplicationService : IUserAdminApplicationService
{
    private readonly IGetUsersQueryHandler _getUsersQueryHandler;
    private readonly IGetUserByIdQueryHandler _getUserByIdQueryHandler;
    private readonly ICreateUserCommandHandler _createUserCommandHandler;
    private readonly IUpdateUserCommandHandler _updateUserCommandHandler;
    private readonly IDeleteUserCommandHandler _deleteUserCommandHandler;
    private readonly IResetUserPasswordCommandHandler _resetUserPasswordCommandHandler;
    private readonly IToggleUserStatusCommandHandler _toggleUserStatusCommandHandler;

    public UserAdminApplicationService(
        IGetUsersQueryHandler getUsersQueryHandler,
        IGetUserByIdQueryHandler getUserByIdQueryHandler,
        ICreateUserCommandHandler createUserCommandHandler,
        IUpdateUserCommandHandler updateUserCommandHandler,
        IDeleteUserCommandHandler deleteUserCommandHandler,
        IResetUserPasswordCommandHandler resetUserPasswordCommandHandler,
        IToggleUserStatusCommandHandler toggleUserStatusCommandHandler)
    {
        _getUsersQueryHandler = getUsersQueryHandler;
        _getUserByIdQueryHandler = getUserByIdQueryHandler;
        _createUserCommandHandler = createUserCommandHandler;
        _updateUserCommandHandler = updateUserCommandHandler;
        _deleteUserCommandHandler = deleteUserCommandHandler;
        _resetUserPasswordCommandHandler = resetUserPasswordCommandHandler;
        _toggleUserStatusCommandHandler = toggleUserStatusCommandHandler;
    }

    public Task<UserListResponse> GetUsersAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken cancellationToken = default)
        => _getUsersQueryHandler.HandleAsync(page, pageSize, search, cancellationToken);

    public Task<UserInfo?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _getUserByIdQueryHandler.HandleAsync(userId, cancellationToken);

    public Task<AuthResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
        => _createUserCommandHandler.HandleAsync(request, cancellationToken);

    public Task<AuthResult> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
        => _updateUserCommandHandler.HandleAsync(userId, request, cancellationToken);

    public Task<AuthResult> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => _deleteUserCommandHandler.HandleAsync(userId, cancellationToken);

    public Task<AuthResult> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
        => _resetUserPasswordCommandHandler.HandleAsync(userId, newPassword, cancellationToken);

    public Task<AuthResult> ToggleUserStatusAsync(Guid userId, CancellationToken cancellationToken = default)
        => _toggleUserStatusCommandHandler.HandleAsync(userId, cancellationToken);
}
