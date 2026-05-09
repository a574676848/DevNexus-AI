using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.UserAdminUseCases;

/// <summary>
/// 用户详情查询处理器接口。
/// </summary>
internal interface IGetUserByIdQueryHandler
{
    Task<UserInfo?> HandleAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理用户详情查询。
/// </summary>
internal sealed class GetUserByIdQueryHandler : IGetUserByIdQueryHandler
{
    private readonly IUserManagementService _userManagementService;

    public GetUserByIdQueryHandler(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    public Task<UserInfo?> HandleAsync(Guid userId, CancellationToken cancellationToken = default)
        => _userManagementService.GetUserByIdAsync(userId, cancellationToken);
}
