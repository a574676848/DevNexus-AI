using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Services.AuthUseCases;

/// <summary>
/// 当前用户查询处理器接口。
/// </summary>
internal interface IGetCurrentUserQueryHandler
{
    Task<UserInfo?> HandleAsync(Guid currentUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理当前用户查询。
/// </summary>
internal sealed class GetCurrentUserQueryHandler : IGetCurrentUserQueryHandler
{
    private readonly IAuthService _authService;

    public GetCurrentUserQueryHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task<UserInfo?> HandleAsync(Guid currentUserId, CancellationToken cancellationToken = default)
        => _authService.GetUserInfoAsync(currentUserId, cancellationToken);
}
