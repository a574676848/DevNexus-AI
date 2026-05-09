using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.AuthUseCases;

/// <summary>
/// 更新个人资料命令处理器接口。
/// </summary>
internal interface IUpdateProfileCommandHandler
{
    Task<AuthResult> HandleAsync(
        Guid currentUserId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理更新个人资料命令。
/// </summary>
internal sealed class UpdateProfileCommandHandler : IUpdateProfileCommandHandler
{
    private readonly IUserManagementService _userManagementService;

    public UpdateProfileCommandHandler(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    public Task<AuthResult> HandleAsync(
        Guid currentUserId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
        => _userManagementService.UpdateProfileAsync(currentUserId, request, cancellationToken);
}
