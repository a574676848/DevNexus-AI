using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.UserAdminUseCases;

/// <summary>
/// 用户列表查询处理器接口。
/// </summary>
internal interface IGetUsersQueryHandler
{
    Task<UserListResponse> HandleAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理用户列表查询。
/// </summary>
internal sealed class GetUsersQueryHandler : IGetUsersQueryHandler
{
    private readonly IUserManagementService _userManagementService;

    public GetUsersQueryHandler(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    public Task<UserListResponse> HandleAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
        => _userManagementService.GetUsersAsync(page, pageSize, search, cancellationToken);
}
